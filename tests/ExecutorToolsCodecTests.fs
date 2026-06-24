module VibeFs.Tests.ExecutorToolsCodecTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Executor
open VibeFs.Shell.ExecutorToolsCodec

let decodeExecutorInvalidLanguage () =
    let args =
        createObj [
            "language", box "ruby"
            "program", box "echo hi"
            "timeout_type", box "short"
            "mode", box "ro"
        ]
    match decodeExecutorArgs args with
    | Error (InvalidIntent ("executor", "language", "expected shell, python, or javascript")) ->
        check "executor invalid language" true
    | _ -> check "executor invalid language" false

let decodeExecutorMissingProgram () =
    let args = createObj [ "language", box "shell"; "mode", box "ro" ]
    match decodeExecutorArgs args with
    | Error (InvalidIntent ("executor", "program", "required")) -> check "executor missing program" true
    | _ -> check "executor missing program" false

let decodeExecutorOkShell () =
    let args =
        createObj [
            "language", box "shell"
            "program", box "echo ok"
            "dependencies", box [| "dep-a" |]
            "timeout_type", box "long"
            "mode", box "rw"
        ]
    match decodeExecutorArgs args with
    | Ok ex ->
        check "executor ok language" (ex.Language = Shell)
        check "executor ok program" (ex.Program = "echo ok")
        equal "executor ok deps count" 1 ex.Dependencies.Length
        check "executor ok timeout" (ex.TimeoutType = Long)
        check "executor ok mode" (ex.Mode = "rw")
        let opts = toExecuteOptions (Some "/tmp/ws") ex
        check "executor ok cwd" (opts.cwd = Some "/tmp/ws")
        check "executor ok opts language" (opts.language = Shell)
    | Error _ -> check "executor ok shell" false

let run () =
    decodeExecutorInvalidLanguage ()
    decodeExecutorMissingProgram ()
    decodeExecutorOkShell ()