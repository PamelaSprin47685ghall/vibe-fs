module VibeFs.Shell.ExecutorPaths

open Fable.Core
open Fable.Core.JsInterop
open System.Text.RegularExpressions

[<Import("tmpdir", "node:os")>]
let private tmpdir () : string = jsNative
[<Import("join", "node:path")>]
let private join (a: string) (b: string) : string = jsNative
[<Import("mkdirSync", "node:fs")>]
let private mkdirSync (dir: string) (opts: obj) : unit = jsNative

let executorLogDir : string = join (tmpdir ()) "omp-kunwei-executor"

let private sanitizeId (id: string) : string = Regex.Replace(id, "/", "-")

/// Per-session project directory under the executor log dir, created on demand.
let getExecutorProjectDir (sessionId: string option) : string =
    match sessionId with
    | None ->
        let dir = join executorLogDir "executor"
        mkdirSync dir (box {| recursive = true |}); dir
    | Some sid ->
        let dir = join executorLogDir $"executor-{sanitizeId sid}"
        mkdirSync dir (box {| recursive = true |}); dir

let getExecutorTempScriptPath (sessionId: string) (extension: string) : string =
    $"{getExecutorProjectDir (Some sessionId)}/script.{extension}"
