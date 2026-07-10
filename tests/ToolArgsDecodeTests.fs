module Wanxiangshu.Tests.ToolArgsDecodeTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Shell.ToolArgsDecode
open Wanxiangshu.Shell.ToolHookRuntime
open Wanxiangshu.Tests.IntegrationToolSetup

module Dyn = Wanxiangshu.Shell.Dyn

let decodeCoderBatchOk () =
    let args = createObj [ "intents", box [| sampleCoderIntent "fix" "a.ts" |] ]

    match decodeToolInvocation "coder" args with
    | Ok(CoderBatch [ one ]) ->
        check "coder objective" (one.objective = "fix")
        check "coder target file" (one.targets.Head.file = "a.ts")
    | _ -> check "coder batch ok" false

let decodeInvestigatorBatchOk () =
    let args = createObj [ "intents", box [| sampleInvestigatorIntent "trace flow" |] ]

    match decodeToolInvocation "investigator" args with
    | Ok(InvestigatorBatch [ one ]) -> check "investigator objective" (one.objective = "trace flow")
    | _ -> check "investigator batch ok" false

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

let decodeWebsearchMissingWhatToSummarize () =
    let args = createObj [ "query", box "q" ]

    match decodeToolInvocation "websearch" args with
    | Error(InvalidIntent("websearch", "what_to_summarize", "required")) ->
        check "websearch missing what_to_summarize via decodeToolInvocation" true
    | _ -> check "websearch missing what_to_summarize via decodeToolInvocation" false

let decodeExecutorOkShell () =
    let args =
        createObj
            [ "language", box "shell"
              "program", box "echo ok"
              "dependencies", box [| "dep-a" |]
              "timeout_type", box "long"
              "mode", box "rw"
              "what_to_summarize", box "focus on exit codes and stderr"
              "warn", box "it-is-not-possible-to-do-it-using-other-tools" ]

    match decodeToolInvocation "executor" args with
    | Ok(Typed(Executor ex)) ->
        check "executor ok language" (ex.Language = Shell)
        check "executor ok program" (ex.Program = "echo ok")
        equal "executor ok deps count" 1 ex.Dependencies.Length
        check "executor ok timeout" (ex.TimeoutType = Long)
        check "executor ok mode" (ex.Mode = "rw")
        check "executor ok what_to_summarize" (ex.WhatToSummarize = "focus on exit codes and stderr")
    | _ -> check "executor ok shell via decodeToolInvocation" false

let decodeExecutorMissingWhatToSummarize () =
    let args =
        createObj
            [ "language", box "shell"
              "program", box "echo ok"
              "timeout_type", box "long"
              "mode", box "rw"
              "warn", box "it-is-not-possible-to-do-it-using-other-tools" ]

    match decodeToolInvocation "executor" args with
    | Error(InvalidIntent("executor", "what_to_summarize", "required")) ->
        check "executor missing what_to_summarize via decodeToolInvocation" true
    | _ -> check "executor missing what_to_summarize via decodeToolInvocation" false

let decodeTodowriteMissingCompletedWorkReport () =
    let args =
        createObj
            [ "ahaMoments", box ""
              "changesAndReasons", box ""
              "gotchas", box ""
              "lessonsAndConventions", box ""
              "plan", box ""
              "todos", box [||] ]

    match decodeToolInvocation "todowrite" args with
    | Error(InvalidIntent("todowrite", "ahaMoments", _)) -> check "todowrite missing ahaMoments" true
    | _ -> check "todowrite missing ahaMoments" false

let decodeTodowriteOk () =
    let args =
        createObj
            [ "ahaMoments", box (System.String('a', 1024))
              "changesAndReasons", box (System.String('b', 1024))
              "gotchas", box (System.String('c', 1024))
              "lessonsAndConventions", box (System.String('d', 1024))
              "plan", box (System.String('e', 1024))
              "select_methodology", box [| "deduction" |]
              "todos", box [| createObj [ "content", box "a"; "status", box "pending"; "priority", box "high" ] |] ]

    match decodeToolInvocation "todowrite" args with
    | Ok(Typed(TodoWrite tw)) ->
        check "todowrite ok ahaMoments" (tw.AhaMoments = System.String('a', 1024))
        equal "todowrite ok todos" 1 tw.Todos.Length
        check "todowrite ok methodology" (tw.SelectMethodology = [ "deduction" ])
    | _ -> check "todowrite ok" false

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

let testCoerceUnknownToolArgsOk () =
    registerToolParameterTypes
        [ ("my_custom_tool", "count", SchemaType.SNumber)
          ("my_custom_tool", "verbose", SchemaType.SBoolean)
          ("my_custom_tool", "items", SchemaType.SArray)
          ("my_custom_tool", "config", SchemaType.SObject)
          ("my_custom_tool", "name", SchemaType.SString) ]

    let args =
        createObj
            [ "count", box "42"
              "verbose", box "true"
              "items", box """["a","b"]"""
              "config", box """{"key":"val"}"""
              "name", box "hello" ]

    coerceArgsTypes "my_custom_tool" args

    let count = Dyn.get args "count"
    check "custom number coerced" (not (Dyn.typeIs count "string"))
    check "custom number value" (unbox<int> count = 42)

    let verbose = Dyn.get args "verbose"
    check "custom boolean coerced" (not (Dyn.typeIs verbose "string"))
    check "custom boolean value" (unbox<bool> verbose = true)

    let items = Dyn.get args "items"
    check "custom array coerced" (not (Dyn.typeIs items "string"))
    check "custom array is array" (Dyn.isArray items)

    let config = Dyn.get args "config"
    check "custom object coerced" (not (Dyn.typeIs config "string"))
    check "custom object is object" (Dyn.typeIs config "object")

    let name = Dyn.get args "name"
    check "string field not coerced" (Dyn.typeIs name "string")
    check "string field preserved" (unbox<string> name = "hello")

let testSanitizeNullArgs () =
    let args =
        createObj
            [ "program", box "echo"
              "dependencies", box null
              "timeout_type", box "long"
              "mode", box "rw"
              "what_to_summarize", box (createObj []) // Empty object on required field, must be kept
              "empty_obj", box (createObj []) // Empty object on optional field, must be deleted
              "non_empty_obj", box (createObj [ "a", box 1 ]) ] // Non-empty object on optional field, must be kept

    sanitizeNullArgs "executor" args

    let keys = Dyn.keys args
    check "dependencies deleted" (not (Array.contains "dependencies" keys))
    check "empty_obj deleted" (not (Array.contains "empty_obj" keys))
    check "program kept" (Array.contains "program" keys)
    check "timeout_type kept" (Array.contains "timeout_type" keys)
    check "mode kept" (Array.contains "mode" keys)
    check "what_to_summarize kept" (Array.contains "what_to_summarize" keys)
    check "non_empty_obj kept" (Array.contains "non_empty_obj" keys)

let testCoerceArgsTypesOk () =
    let readArgs =
        createObj [ "path", box "a.txt"; "offset", box "123"; "limit", box "456" ]

    coerceArgsTypes "read" readArgs

    let fetchArgs = createObj [ "url", box "http://localhost"; "timeout", box "15" ]

    coerceArgsTypes "webfetch" fetchArgs

    let intentsJson =
        """[{"objective":"fix bug","background":"test","targets":[{"file":"a.ts","guide":"fix it"}]}]"""

    let coderArgs =
        createObj
            [ "intents", box intentsJson
              "tdd", box "red"
              "warn_tdd", box "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles-and-kept-todo-updated" ]

    coerceArgsTypes "coder" coderArgs

    match decodeToolInvocation "read" readArgs with
    | Ok(Typed(ToolArgs.Read r)) ->
        check "read offset coerced" (r.Offset = Some 123)
        check "read limit coerced" (r.Limit = Some 456)
    | _ -> check "read coerced failed" false

    match decodeToolInvocation "webfetch" fetchArgs with
    | Ok(Typed(ToolArgs.Webfetch w)) -> check "webfetch timeout coerced" (w.Timeout = Some 15)
    | _ -> check "webfetch coerced failed" false

    match decodeToolInvocation "coder" coderArgs with
    | Ok(CoderBatch [ intent ]) ->
        check "coder intents coerced objective" (intent.objective = "fix bug")
        check "coder intents coerced file" (intent.targets.Head.file = "a.ts")
    | _ -> check "coder coerced failed" false

let run () =
    decodeCoderBatchOk ()
    decodeInvestigatorBatchOk ()
    decodeCoderMissingIntents ()
    decodeCoderInvalidIntentShape ()
    decodeCoderEmptyIntentsArray ()
    decodeToolArgsRejectsCoderBatch ()
    decodeWebsearchMissingWhatToSummarize ()
    decodeExecutorOkShell ()
    decodeExecutorMissingWhatToSummarize ()
    decodeTodowriteMissingCompletedWorkReport ()
    decodeTodowriteOk ()
    decodeApplyPatchMissingPatchText ()
    decodeApplyPatchOk ()
    decodeSubmitReviewMissingReport ()
    decodeSubmitReviewOk ()
    testSanitizeNullArgs ()
    testCoerceArgsTypesOk ()
    testCoerceUnknownToolArgsOk ()
