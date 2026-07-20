module Wanxiangshu.Runtime.ExecutorJavascriptHelper

open Fable.Core
open Fable.Core.JsInterop
open System.Text.RegularExpressions
open Thoth.Json

[<Import("join", "node:path")>]
let join (a: string) (b: string) : string = jsNative

[<Import("resolve", "node:path")>]
let resolve (cwd: string) (p: string) : string = jsNative

[<Import("pathToFileURL", "node:url")>]
let pathToFileURL (p: string) : obj = jsNative

[<Import("existsSync", "node:fs")>]
let existsSync (p: string) : bool = jsNative

[<Import("readFileSync", "node:fs")>]
let readFileSync (p: string) (encoding: string) : string = jsNative

[<Import("mkdirSync", "node:fs")>]
let mkdirSync (dir: string) (opts: obj) : unit = jsNative

[<Import("writeFileSync", "node:fs")>]
let writeFileSync (path: string) (content: string) (encoding: string) : unit = jsNative

[<Import("spawn", "node:child_process")>]
let childSpawn (cmd: string) (args: string array) (opts: obj) : obj = jsNative

/// Initialise the lexer and parse a program's static imports in one step.
let parseImports (program: string) : JS.Promise<obj array> =
    promise {
        let! module' = importDynamic<obj> "es-module-lexer"
        do! Dyn.get module' "init" |> unbox<JS.Promise<unit>>
        let parseResult = Dyn.call1 (Dyn.get module' "parse") (box program)
        let imports = (parseResult :?> obj array).[0]
        return unbox<obj array> imports
    }

/// Read the full package.json object, defaulting to `{ type:'module', dependencies:{} }`
/// and ensuring both fields exist.
let parsePkgJson (json: string) : obj =
    try
        match Decode.Auto.fromString<obj> json with
        | Ok p ->
            if Dyn.isNullish (Dyn.get p "dependencies") then
                p?("dependencies") <- createObj []

            if Dyn.isNullish (Dyn.get p "type") then
                p?("type") <- "module"

            p
        | Error _ -> createObj [ "type" ==> "module"; "dependencies" ==> createObj [] ]
    with _ ->
        createObj [ "type" ==> "module"; "dependencies" ==> createObj [] ]

let setDep (deps: obj) (pkg: string) (value: string) : unit = deps?(pkg) <- value

[<Global>]
let private JSON: obj = jsNative

let jsonStringify (value: obj) : string =
    JSON?stringify(value, Unchecked.defaultof<obj>, 2)

let jsonEscape (s: string) : string = Encode.Auto.toString (0, s)

/// Spawn `npx npm install` in the project dir, resolving on a clean exit.
let npmInstall (projectDir: string) (packages: string array) : JS.Promise<unit> =
    let args' =
        Array.append [| "--yes"; "npm@latest"; "install"; "--prefix"; projectDir |] packages

    Promise.create (fun resolve reject ->
        let c = childSpawn "npx" args' (box {| cwd = projectDir; stdio = "ignore" |})
        c?on ("error", (fun (e: obj) -> reject (e :?> exn))) |> ignore

        c?on (
            "close",
            fun (code: obj) ->
                if unbox<int> code = 0 then
                    resolve ()
                else
                    reject (exn $"npm install exited with {code}")
        )
        |> ignore)
