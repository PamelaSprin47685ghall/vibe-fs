module Wanxiangshu.Tests.ToolArgsDecodeTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Runtime.ToolArgsDecode
open Wanxiangshu.Runtime.ToolHookRuntime
open Wanxiangshu.Tests.IntegrationToolSetup

module Dyn = Wanxiangshu.Runtime.Dyn

let decodeCoderBatchOk () =
    let args = createObj [ "intents", box [| sampleCoderIntent "fix" "a.ts" |] ]

    match decodeToolInvocation "coder" args with
    | Ok(CoderBatch [ one ]) ->
        check "coder objective" (one.objective = "fix")
        check "coder target file" (one.targets.Head.file = "a.ts")
    | _ -> check "coder batch ok" false

let decodeInspectorBatchOk () =
    let args = createObj [ "intents", box [| sampleInspectorIntent "trace flow" |] ]

    match decodeToolInvocation "inspector" args with
    | Ok(InspectorBatch [ one ]) -> check "inspector objective" (one.objective = "trace flow")
    | _ -> check "inspector batch ok" false

let decodeCoderMissingIntents () =
    let args = createObj []

    match decodeToolInvocation "coder" args with
    | Error(InvalidIntent("coder", "intents", _)) -> check "coder missing intents" true
    | _ -> check "coder missing intents" false

let decodeCoderInvalidIntentShape () =
    let args = createObj [ "intents", box [| createObj [ "objective", box "" ] |] ]

    match decodeToolInvocation "coder" args with
    | Error(ParseError("intents", _)) -> check "coder invalid intent" true
    | _ -> check "coder invalid intent" false

let decodeCoderEmptyIntentsArray () =
    let args = createObj [ "intents", box [||] ]

    match decodeToolInvocation "coder" args with
    | Error(ParseError("intents", _)) -> check "coder empty intents array" true
    | Ok(CoderBatch _) -> check "coder empty intents array" false
    | _ -> check "coder empty intents array" false

let decodeToolArgsRejectsCoderBatch () =
    let args = createObj [ "intents", box [| sampleCoderIntent "x" "y.ts" |] ]

    match decodeToolArgs "coder" args with
    | Error(InvalidIntent("coder", "tool", _)) -> check "decodeToolArgs rejects batch" true
    | _ -> check "decodeToolArgs rejects batch" false

let decodeExecutorOkShell () =
    let args =
        createObj
            [ "language", box "shell"
              "command", box "echo ok"
              "dependencies", box [| "dep-a" |]
              "timeout_type", box "long"
              "what_to_summarize", box "focus on exit codes and stderr"
              "max_bytes", box 8192 ]

    match decodeToolInvocation "executor" args with
    | Ok(Typed(Executor ex)) ->
        check "executor ok language" (ex.Language = Shell)
        check "executor ok program" (ex.Command = "echo ok")
        equal "executor ok deps count" 1 ex.Dependencies.Length
        check "executor ok timeout" (ex.TimeoutType = Long)
        check "executor ok what_to_summarize" (ex.WhatToSummarize = "focus on exit codes and stderr")
    | _ -> check "executor ok shell via decodeToolInvocation" false

let decodeExecutorMissingWhatToSummarize () =
    let args =
        createObj
            [ "language", box "shell"
              "command", box "echo ok"
              "timeout_type", box "long" ]

    match decodeToolInvocation "executor" args with
    | Error(InvalidIntent("executor", "what_to_summarize", "required")) ->
        check "executor missing what_to_summarize via decodeToolInvocation" true
    | _ -> check "executor missing what_to_summarize via decodeToolInvocation" false

let decodeApplyPatchMissingPatchText () =
    let args = createObj []

    match decodeToolInvocation "apply_patch" args with
    | Error(InvalidIntent("apply_patch", "patchText", _)) -> check "apply_patch missing patchText" true
    | _ -> check "apply_patch missing patchText" false

let decodeApplyPatchOk () =
    let args = createObj [ "patchText", box "@@\n-old\n+new" ]

    match decodeToolInvocation "apply_patch" args with
    | Ok(Typed(ApplyPatch { PatchText = t })) -> check "apply_patch ok text" (t = "@@\n-old\n+new")
    | _ -> check "apply_patch ok" false

let decodeSubmitReviewMissingReport () =
    let args = createObj [ "affectedFiles", box [| "a.fs" |] ]

    match decodeToolInvocation "submit_review" args with
    | Error(InvalidIntent("submit_review", "report", _)) -> check "submit_review missing report" true
    | _ -> check "submit_review missing report" false

let decodeSubmitReviewOk () =
    let args =
        createObj [ "report", box "review body"; "affectedFiles", box [| "src/x.fs" |] ]

    match decodeToolInvocation "submit_review" args with
    | Ok(Typed(SubmitReview sr)) ->
        check "submit_review ok report" (sr.Report = "review body")
        equal "submit_review ok files" 1 sr.AffectedFiles.Length
    | _ -> check "submit_review ok" false

let testSanitizeNullArgs () =
    let args =
        createObj
            [ "command", box "echo"
              "dependencies", box null
              "timeout_type", box "long"
              "mode", box "rw"
              "what_to_summarize", box (createObj []) // Empty object on required field, must be kept
              "max_bytes", box 8192
              "empty_obj", box (createObj []) // Empty object on optional field, must be deleted
              "non_empty_obj", box (createObj [ "a", box 1 ]) ] // Non-empty object on optional field, must be kept

    sanitizeNullArgs "executor" args

    let keys = Dyn.keys args
    check "dependencies deleted" (not (Array.contains "dependencies" keys))
    check "empty_obj deleted" (not (Array.contains "empty_obj" keys))
    check "command kept" (Array.contains "command" keys)
    check "timeout_type kept" (Array.contains "timeout_type" keys)
    check "mode kept" (Array.contains "mode" keys)
    check "what_to_summarize kept" (Array.contains "what_to_summarize" keys)
    check "non_empty_obj kept" (Array.contains "non_empty_obj" keys)

let run () =
    decodeCoderBatchOk ()
    decodeInspectorBatchOk ()
    decodeCoderMissingIntents ()
    decodeCoderInvalidIntentShape ()
    decodeCoderEmptyIntentsArray ()
    decodeToolArgsRejectsCoderBatch ()
    decodeExecutorOkShell ()
    decodeExecutorMissingWhatToSummarize ()
    decodeApplyPatchMissingPatchText ()
    decodeApplyPatchOk ()
    decodeSubmitReviewMissingReport ()
    decodeSubmitReviewOk ()
    testSanitizeNullArgs ()
