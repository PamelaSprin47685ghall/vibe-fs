module Wanxiangshu.Tests.ExecutorToolsCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Runtime.ExecutorToolsCodec

let decodeExecutorInvalidLanguage () =
    let args =
        createObj
            [ "language", box "ruby"
              "command", box "echo hi"
              "timeout_type", box "short" ]

    match decodeExecutorArgs args with
    | Error(InvalidIntent("executor", "language", "expected shell, python, or javascript")) ->
        check "executor invalid language" true
    | _ -> check "executor invalid language" false

let decodeExecutorMissingProgram () =
    let args = createObj [ "language", box "shell" ]

    match decodeExecutorArgs args with
    | Error(InvalidIntent("executor", "command", "required")) -> check "executor missing program" true
    | _ -> check "executor missing program" false

let decodeExecutorOkShell () =
    let args =
        createObj
            [ "language", box "shell"
              "command", box "echo ok"
              "dependencies", box [| "dep-a" |]
              "timeout_type", box "long"
              "what_to_summarize", box "summarize exit codes and stderr only"
              "max_bytes", box 8192 ]

    match decodeExecutorArgs args with
    | Ok ex ->
        check "executor ok language" (ex.Language = Shell)
        check "executor ok program" (ex.Command = "echo ok")
        equal "executor ok deps count" 1 ex.Dependencies.Length
        check "executor ok timeout" (ex.TimeoutType = Long)
        check "executor ok what_to_summarize" (ex.WhatToSummarize = "summarize exit codes and stderr only")
        let opts = toExecuteOptions (Some "/tmp/ws") ex
        check "executor ok cwd" (opts.cwd = Some "/tmp/ws")
        check "executor ok opts language" (opts.language = Shell)
    | Error _ -> check "executor ok shell" false

let decodeExecutorMissingWhatToSummarize () =
    let args =
        createObj
            [ "language", box "shell"
              "command", box "echo ok"
              "timeout_type", box "long" ]

    match decodeExecutorArgs args with
    | Error(InvalidIntent("executor", "what_to_summarize", "required")) ->
        check "executor missing what_to_summarize" true
    | _ -> check "executor missing what_to_summarize" false

let run () =
    decodeExecutorInvalidLanguage ()
    decodeExecutorMissingProgram ()
    decodeExecutorOkShell ()
    decodeExecutorMissingWhatToSummarize ()
