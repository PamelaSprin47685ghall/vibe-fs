module VibeFs.Shell.ExecutorJavascript

open Fable.Core
open Fable.Core.JsInterop
open System.Text.RegularExpressions
open VibeFs.Kernel

[<Import("join", "node:path")>]
let private join (a: string) (b: string) : string = jsNative
[<Import("resolve", "node:path")>]
let private resolve (cwd: string) (p: string) : string = jsNative
[<Import("pathToFileURL", "node:url")>]
let private pathToFileURL (p: string) : obj = jsNative

[<Import("existsSync", "node:fs")>]
let private existsSync (p: string) : bool = jsNative
[<Import("readFileSync", "node:fs")>]
let private readFileSync (p: string) (encoding: string) : string = jsNative
[<Import("mkdirSync", "node:fs")>]
let private mkdirSync (dir: string) (opts: obj) : unit = jsNative
[<Import("writeFileSync", "node:fs")>]
let private writeFileSync (path: string) (content: string) (encoding: string) : unit = jsNative
[<Import("spawn", "node:child_process")>]
let private childSpawn (cmd: string) (args: string array) (opts: obj) : obj = jsNative

let private asPromise<'T> (o: obj) : JS.Promise<'T> = unbox<JS.Promise<'T>> o

/// Initialise the lexer and parse a program's static imports in one step.
let private parseImports (program: string) : JS.Promise<obj array> =
    async {
        let! module' = importDynamic<obj> "es-module-lexer" |> Async.AwaitPromise
        let init = Dyn.get module' "init"
        do! Dyn.call1 init (box ()) |> asPromise<unit> |> Async.AwaitPromise
        let parse = Dyn.get module' "parse"
        let! result = Dyn.call1 parse (box program) |> asPromise<obj[]> |> Async.AwaitPromise
        return unbox<obj[]> result.[0]
    }
    |> Async.StartAsPromise

/// Read the full package.json object, defaulting to `{ type:'module', dependencies:{} }`
/// and ensuring both fields exist (repairs a pre-existing file lacking type:module).
let private parsePkgJson (json: string) : obj =
    try
        let p = JS.JSON.parse(json)
        if Dyn.isNullish (Dyn.get p "dependencies") then p?("dependencies") <- createObj []
        if Dyn.isNullish (Dyn.get p "type") then p?("type") <- "module"
        p
    with _ ->
        createObj [ "type" ==> "module"; "dependencies" ==> createObj [] ]

let private setDep (deps: obj) (pkg: string) (value: string) : unit =
    deps?(pkg) <- value

[<Global>]
let private JSON : obj = jsNative

let private jsonStringify (value: obj) : string =
    JSON?stringify(value, Unchecked.defaultof<obj>, 2)

let private jsonEscape (s: string) : string =
    JS.JSON.stringify(s)

/// Force `type: "module"` onto a package object (type is reserved in F#).
let private setTypeModule (pkg: obj) : unit =
    pkg?("type") <- "module"

/// Spawn `npx npm install` in the project dir, resolving on a clean exit.
let private npmInstall (projectDir: string) (packages: string array) : JS.Promise<unit> =
    let args' = Array.append [| "--yes"; "npm@latest"; "install"; "--prefix"; projectDir |] packages
    let work =
        Async.FromContinuations(fun (resolve, reject, _) ->
            let c = childSpawn "npx" args' (box {| cwd = projectDir; stdio = "ignore" |})
            c?on("error", fun (e: obj) -> reject (e :?> exn)) |> ignore
            c?on("close", fun (code: obj) ->
                if unbox<int> code = 0 then resolve ()
                else reject (exn $"npm install exited with {code}")) |> ignore
        )
    work |> Async.StartAsPromise

/// Build the ESM prelude that gives a transpiled script require/__dirname/__filename.
let createJavascriptPrelude (cwd: string) : string =
    let requirePath = join cwd "__runner__.cjs"
    let filename = join cwd "__runner__.mjs"
    String.concat "\n"
        [ "import { createRequire } from \"node:module\";"
          $"const require = createRequire({jsonEscape requirePath});"
          $"const __dirname = {jsonEscape cwd};"
          $"const __filename = {jsonEscape filename};"
          "" ]

/// Resolve a relative module specifier (`./x`, `../x`) to a file:// URL.
let resolveJavascriptSpecifier (cwd: string) (specifier: string) : string =
    let match' = Regex.Match(specifier, @"^(\.{1,2}(?:/[^?#]*)?)([?#].*)?$")
    if not match'.Success then specifier
    else
        let basePath = match'.Groups.[1].Value
        let suffix = if match'.Groups.[2].Success then match'.Groups.[2].Value else ""
        $"{(pathToFileURL (resolve cwd basePath))?href}{suffix}"

/// Rewrite relative import specifiers in a JS program to absolute file:// URLs.
/// Uses es-module-lexer parse results to build output from non-overlapping segments
/// rather than string-index replacement, avoiding sourcemap/edge-case fragility.
let rewriteJavascriptModuleSpecifiers (program: string) (cwd: string) : JS.Promise<string> =
    async {
        let! imports = parseImports program |> Async.AwaitPromise
        if Dyn.isNullish imports || Array.isEmpty imports then return program
        else
            let segments = ResizeArray<string>()
            let mutable lastEnd = 0
            for imp in imports do
                let n = Dyn.get imp "n"
                if not (Dyn.isNullish n) then
                    let ns = string n
                    if Regex.IsMatch(ns, @"^\.\.?/") then
                        let d = Dyn.get imp "d"
                        let isDynamic = not (Dyn.isNullish d) && unbox<int> d <> -1
                        let s = unbox<int> (Dyn.get imp "s")
                        let e = unbox<int> (Dyn.get imp "e")
                        let sAdj = if isDynamic then s + 1 else s
                        let eAdj = if isDynamic then e - 1 else e
                        if sAdj > lastEnd then
                            segments.Add(program.[lastEnd .. sAdj - 1])
                        segments.Add(resolveJavascriptSpecifier cwd ns)
                        lastEnd <- eAdj
            if lastEnd < program.Length then
                segments.Add(program.[lastEnd ..])
            if segments.Count > 0 then return String.concat "" segments
            else return program
    }
    |> Async.StartAsPromise

/// Ensure the temp project dir is an ESM project with tsx + dependencies.
/// Always guarantees `type: "module"` is present (even when deps are satisfied),
/// matching the original "ensure is ESM project" semantics.
let ensureJavascriptProject (projectDir: string) (dependencies: string list) : JS.Promise<unit> =
    async {
        mkdirSync projectDir (box {| recursive = true |})
        let pkgPath = $"{projectDir}/package.json"
        let pkg = if existsSync pkgPath then parsePkgJson (readFileSync pkgPath "utf8") else box {| |}
        let deps = Dyn.get pkg "dependencies"
        let deps = if Dyn.isNullish deps then box {| |} else deps
        // Always (re)assert type:module so a pre-existing file lacking it is repaired.
        setTypeModule pkg
        let required = "tsx" :: dependencies |> List.distinct
        let toInstall = required |> List.filter (fun pkgName -> Dyn.isNullish (Dyn.get deps pkgName))
        for pkgName in toInstall do setDep deps pkgName "*"
        // Write back whenever there are packages to install OR the file is freshly created.
        if not (existsSync pkgPath) || not toInstall.IsEmpty then
            writeFileSync pkgPath $"{jsonStringify pkg}\n" "utf-8"
        if not toInstall.IsEmpty then
            do! npmInstall projectDir (Array.ofList toInstall) |> Async.AwaitPromise
    }
    |> Async.StartAsPromise
