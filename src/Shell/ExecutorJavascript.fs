module Wanxiangshu.Shell.ExecutorJavascript

open Fable.Core
open Fable.Core.JsInterop
open System.Text.RegularExpressions
open Wanxiangshu.Kernel

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

/// Initialise the lexer and parse a program's static imports in one step.
/// es-module-lexer v2 API (confirmed from types/lexer.d.ts):
///   - `init` is a `Promise<void>` (a value you AWAIT), NOT a function — never call it.
///   - `parse` is a SYNCHRONOUS function returning [imports, exports, facade, hasModuleSyntax].
/// The `init` promise MUST resolve before `parse` is called, or the WASM lexer
/// isn't ready. Both the old `Dyn.call1 init ()` and the `Promise.lift` wrapper
/// were broken (init isn't callable; Promise.lift wouldn't await a nested promise).
let private parseImports (program: string) : JS.Promise<obj array> =
    promise {
        let! module' = importDynamic<obj> "es-module-lexer"
        do! Dyn.get module' "init" |> unbox<JS.Promise<unit>>
        let parseResult = Dyn.call1 (Dyn.get module' "parse") (box program)
        let imports = (parseResult :?> obj array).[0]
        return unbox<obj array> imports
    }

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

/// Spawn `npx npm install` in the project dir, resolving on a clean exit.
let private npmInstall (projectDir: string) (packages: string array) : JS.Promise<unit> =
    let args' = Array.append [| "--yes"; "npm@latest"; "install"; "--prefix"; projectDir |] packages
    Promise.create (fun resolve reject ->
        let c = childSpawn "npx" args' (box {| cwd = projectDir; stdio = "ignore" |})
        c?on("error", fun (e: obj) -> reject (e :?> exn)) |> ignore
        c?on("close", fun (code: obj) ->
            if unbox<int> code = 0 then resolve ()
            else reject (exn $"npm install exited with {code}")) |> ignore)

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
    promise {
        let! imports = parseImports program
        if Dyn.isNullish imports || Array.isEmpty imports then return program
        else
            let lastEnd, segmentsRev =
                imports |> Array.fold (fun (lastEnd, segmentsRev) imp ->
                    let n = Dyn.get imp "n"
                    if Dyn.isNullish n then (lastEnd, segmentsRev)
                    else
                        let ns = string n
                        if not (Regex.IsMatch(ns, @"^\.\.?/")) then (lastEnd, segmentsRev)
                        else
                            let d = Dyn.get imp "d"
                            let isDynamic = not (Dyn.isNullish d) && unbox<int> d <> -1
                            let s = unbox<int> (Dyn.get imp "s")
                            let e = unbox<int> (Dyn.get imp "e")
                            let sAdj = if isDynamic then s + 1 else s
                            let eAdj = if isDynamic then e - 1 else e
                            let withPrefix =
                                if sAdj > lastEnd then program.[lastEnd .. sAdj - 1] :: segmentsRev
                                else segmentsRev
                            (eAdj, resolveJavascriptSpecifier cwd ns :: withPrefix))
                    (0, [])
            let segmentsRev =
                if lastEnd < program.Length then program.[lastEnd ..] :: segmentsRev else segmentsRev
            if List.isEmpty segmentsRev then return program
            else return String.concat "" (List.rev segmentsRev)
    }

/// Ensure the temp project dir is an ESM project with tsx + dependencies.
/// Always guarantees `type: "module"` is present, repairing a pre-existing
/// package.json that lacks it even when no dependencies need installation.
let ensureJavascriptProject (projectDir: string) (dependencies: string list) : JS.Promise<unit> =
    promise {
        mkdirSync projectDir (box {| recursive = true |})
        let pkgPath = $"{projectDir}/package.json"
        let pkg =
            if existsSync pkgPath then parsePkgJson (readFileSync pkgPath "utf8")
            else parsePkgJson "{}"
        let deps = Dyn.get pkg "dependencies"
        let required = "tsx" :: dependencies |> List.distinct
        let toInstall = required |> List.filter (fun pkgName -> Dyn.isNullish (Dyn.get deps pkgName))
        for pkgName in toInstall do setDep deps pkgName "*"
        writeFileSync pkgPath $"{jsonStringify pkg}\n" "utf-8"
        if not toInstall.IsEmpty then
            do! npmInstall projectDir (Array.ofList toInstall)
    }
