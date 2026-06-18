module VibeFs.Shell.Write

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Shell.Path
open VibeFs.Shell.TreeSitterShell

[<Global("process")>]
let private nodeProcess : obj = jsNative

[<Import("promises", "node:fs")>]
let private fsPromises : obj = jsNative

[<Import("access", "node:fs/promises")>]
let private accessAsync (p: string) (mode: int) : JS.Promise<unit> = jsNative

let private asPromise<'T> (o: obj) : JS.Promise<'T> = unbox<JS.Promise<'T>> o

let private mkdir (p: string) : JS.Promise<unit> =
    fsPromises?mkdir(p, {| recursive = true |}) |> asPromise<unit>

let private writeFile (p: string) (content: string) : JS.Promise<unit> =
    fsPromises?writeFile(p, content, "utf-8") |> asPromise<unit>

let private fileExistsAsync (p: string) : Async<bool> =
    async {
        try
            do! accessAsync p 0 |> Async.AwaitPromise
            return true
        with _ ->
            return false
    }

let private formatSyntaxDiagnostics (filePath: string) (result: SyntaxCheckResult) : string =
    match result with
    | Ok (lang, [||]) ->
        $"Successfully wrote to {filePath}"
    | Ok (lang, errors) ->
        let header = $"[syntax-check] Syntax check failed for {filePath} ({lang}):"
        let body =
            errors
            |> Array.map (fun e -> $"  line {e.line}, col {e.column}: {e.message}")
            |> String.concat "\n"
        header + "\n" + body
    | Failed (lang, reason) ->
        $"[syntax-check] Syntax check failed for {filePath} ({lang}): {reason}"

/// Write `content` to `file_path` (creates parent dirs), then run tree-sitter syntax check on the result.
let write (cwd: string option) (file_path: string) (content: string) : Async<string> =
    async {
        let cwd' = defaultArg cwd (nodeProcess?cwd())
        let resolved = resolve cwd' file_path
        let parent = dirname resolved
        if not (System.String.IsNullOrEmpty parent) then
            do! mkdir parent |> Async.AwaitPromise
        do! writeFile resolved content |> Async.AwaitPromise
        let! syntax = checkSyntax content resolved |> Async.AwaitPromise
        return formatSyntaxDiagnostics resolved syntax
    }
