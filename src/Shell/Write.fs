module VibeFs.Shell.Write

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.SyntaxTypes
open VibeFs.Shell.Path
open VibeFs.Shell.TreeSitterShell

[<Emit("process.cwd()")>]
let private processCwd () : string = jsNative

[<Emit("import('node:fs/promises')")>]
let private importFsPromises () : JS.Promise<obj> = jsNative

let private fsApi () = importFsPromises () |> Async.AwaitPromise

[<Emit("$0.mkdir($1, { recursive: true })")>]
let private mkdir (fs': obj) (p: string) : JS.Promise<unit> = jsNative

[<Emit("$0.writeFile($1, $2, 'utf-8')")>]
let private writeFile (fs': obj) (p: string) (content: string) : JS.Promise<unit> = jsNative

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
        let cwd' = defaultArg cwd (processCwd ())
        let resolved = resolve cwd' file_path
        let! api = fsApi ()
        let parent = dirname resolved
        if not (System.String.IsNullOrEmpty parent) then
            do! mkdir api parent |> Async.AwaitPromise
        do! writeFile api resolved content |> Async.AwaitPromise
        let! syntax = checkSyntax content resolved |> Async.AwaitPromise
        return formatSyntaxDiagnostics resolved syntax
    }