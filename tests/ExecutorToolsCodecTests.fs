module Wanxiangshu.Tests.ExecutorToolsCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Shell.ExecutorToolsCodec

let decodeExecutorInvalidLanguage () =
    let args =
        createObj [
            "language", box "ruby"
            "program", box "echo hi"
            "timeout_type", box "short"
            "mode", box "ro"
            "warn", box "it-is-not-possible-to-do-it-using-other-tools"
        ]
    match decodeExecutorArgs args with
    | Error (InvalidIntent ("executor", "language", "expected shell, python, or javascript")) ->
        check "executor invalid language" true
    | _ -> check "executor invalid language" false

let decodeExecutorMissingProgram () =
    let args = createObj [ "language", box "shell"; "mode", box "ro"; "warn", box "it-is-not-possible-to-do-it-using-other-tools" ]
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

let decodeExecutorMissingMode () =
    let args =
        createObj [
            "language", box "shell"
            "program", box "echo ok"
            "timeout_type", box "short"
        ]
    match decodeExecutorArgs args with
    | Error (InvalidIntent ("executor", "mode", "required")) -> check "executor missing mode" true
    | _ -> check "executor missing mode" false

let run () =
    decodeExecutorInvalidLanguage ()
    decodeExecutorMissingProgram ()
    decodeExecutorOkShell ()
    decodeExecutorMissingMode ()