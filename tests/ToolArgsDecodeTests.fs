module Wanxiangshu.Tests.ToolArgsDecodeTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Shell.ToolArgsDecode
open Wanxiangshu.Tests.IntegrationToolSetup

let decodeCoderBatchOk () =
    let args = createObj [ "intents", box [| sampleCoderIntent "fix" "a.ts" |] ]
    match decodeToolInvocation "coder" args with
    | Ok (CoderBatch [ one ]) ->
        check "coder objective" (one.objective = "fix")
        check "coder target file" (one.targets.Head.file = "a.ts")
    | _ -> check "coder batch ok" false

let decodeInvestigatorBatchOk () =
    let args = createObj [ "intents", box [| sampleInvestigatorIntent "trace flow" |] ]
    match decodeToolInvocation "investigator" args with
    | Ok (InvestigatorBatch [ one ]) -> check "investigator objective" (one.objective = "trace flow")
    | _ -> check "investigator batch ok" false

let decodeCoderMissingIntents () =
    let args = createObj []
    match decodeToolInvocation "coder" args with
    | Error (InvalidIntent ("coder", "intents", _)) -> check "coder missing intents" true
    | _ -> check "coder missing intents" false

let decodeCoderInvalidIntentShape () =
    let args = createObj [ "intents", box [| createObj [ "objective", box "" ] |] ]
    match decodeToolInvocation "coder" args with
    | Error (ParseError ("intents", _)) -> check "coder invalid intent" true
    | _ -> check "coder invalid intent" false

let decodeCoderEmptyIntentsArray () =
    let args = createObj [ "intents", box [||] ]
    match decodeToolInvocation "coder" args with
    | Error (ParseError ("intents", _)) -> check "coder empty intents array" true
    | Ok (CoderBatch _) -> check "coder empty intents array" false
    | _ -> check "coder empty intents array" false

let decodeToolArgsRejectsCoderBatch () =
    let args = createObj [ "intents", box [| sampleCoderIntent "x" "y.ts" |] ]
    match decodeToolArgs "coder" args with
    | Error (InvalidIntent ("coder", "tool", _)) -> check "decodeToolArgs rejects batch" true
    | _ -> check "decodeToolArgs rejects batch" false

let decodeWebsearchMissingWhatToSummarize () =
    let args = createObj [ "query", box "q" ]
    match decodeToolInvocation "websearch" args with
    | Error (InvalidIntent ("websearch", "what_to_summarize", "required")) ->
        check "websearch missing what_to_summarize via decodeToolInvocation" true
    | _ -> check "websearch missing what_to_summarize via decodeToolInvocation" false

let decodeExecutorOkShell () =
    let args =
        createObj [
            "language", box "shell"
            "program", box "echo ok"
            "dependencies", box [| "dep-a" |]
            "timeout_type", box "long"
            "mode", box "rw"
            "warn", box "it-is-not-possible-to-do-it-using-other-tools"
        ]
    match decodeToolInvocation "executor" args with
    | Ok (Typed (Executor ex)) ->
        check "executor ok language" (ex.Language = Shell)
        check "executor ok program" (ex.Program = "echo ok")
        equal "executor ok deps count" 1 ex.Dependencies.Length
        check "executor ok timeout" (ex.TimeoutType = Long)
        check "executor ok mode" (ex.Mode = "rw")
    | _ -> check "executor ok shell via decodeToolInvocation" false

let decodeTodowriteMissingCompletedWorkReport () =
    let args = createObj [ "todos", box [||] ]
    match decodeToolInvocation "todowrite" args with
    | Error (InvalidIntent ("todowrite", "completedWorkReport", _)) ->
        check "todowrite missing completedWorkReport" true
    | _ -> check "todowrite missing completedWorkReport" false

let decodeTodowriteOk () =
    let args =
        createObj [
            "completedWorkReport", box "done slice"
            "select_methodology", box [| "deduction" |]
            "todos", box [| createObj [ "content", box "a"; "status", box "pending"; "priority", box "high" ] |]
        ]
    match decodeToolInvocation "todowrite" args with
    | Ok (Typed (TodoWrite tw)) ->
        check "todowrite ok report" (tw.CompletedWorkReport = "done slice")
        equal "todowrite ok todos" 1 tw.Todos.Length
        check "todowrite ok methodology" (tw.SelectMethodology = [ "deduction" ])
    | _ -> check "todowrite ok" false

let decodeApplyPatchMissingPatchText () =
    let args = createObj []
    match decodeToolInvocation "apply_patch" args with
    | Error (InvalidIntent ("apply_patch", "patchText", _)) ->
        check "apply_patch missing patchText" true
    | _ -> check "apply_patch missing patchText" false

let decodeApplyPatchOk () =
    let args = createObj [ "patchText", box "@@\n-old\n+new" ]
    match decodeToolInvocation "apply_patch" args with
    | Ok (Typed (ApplyPatch { PatchText = t })) -> check "apply_patch ok text" (t = "@@\n-old\n+new")
    | _ -> check "apply_patch ok" false

let decodeSubmitReviewMissingReport () =
    let args = createObj [ "affectedFiles", box [| "a.fs" |] ]
    match decodeToolInvocation "submit_review" args with
    | Error (InvalidIntent ("submit_review", "report", _)) ->
        check "submit_review missing report" true
    | _ -> check "submit_review missing report" false

let decodeSubmitReviewOk () =
    let args = createObj [ "report", box "review body"; "affectedFiles", box [| "src/x.fs" |] ]
    match decodeToolInvocation "submit_review" args with
    | Ok (Typed (SubmitReview sr)) ->
        check "submit_review ok report" (sr.Report = "review body")
        equal "submit_review ok files" 1 sr.AffectedFiles.Length
    | _ -> check "submit_review ok" false

let run () =
    decodeCoderBatchOk ()
    decodeInvestigatorBatchOk ()
    decodeCoderMissingIntents ()
    decodeCoderInvalidIntentShape ()
    decodeCoderEmptyIntentsArray ()
    decodeToolArgsRejectsCoderBatch ()
    decodeWebsearchMissingWhatToSummarize ()
    decodeExecutorOkShell ()
    decodeTodowriteMissingCompletedWorkReport ()
    decodeTodowriteOk ()
    decodeApplyPatchMissingPatchText ()
    decodeApplyPatchOk ()
    decodeSubmitReviewMissingReport ()
    decodeSubmitReviewOk ()