module VibeFs.Tests.WorkBacklogToolsCodecTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Domain
open VibeFs.Shell.WorkBacklogToolsCodec

let decodeTodoMissingCompletedWorkReport () =
    let args =
        createObj [
            "todos", box [| createObj [ "content", box "x"; "status", box "pending"; "priority", box "high" ] |]
            "select_methodology", box [| "first_principles" |]
        ]
    match decodeTodoWriteArgs args with
    | Error (InvalidIntent ("todowrite", "completedWorkReport", _)) ->
        check "todo missing completedWorkReport" true
    | _ -> check "todo missing completedWorkReport" false

let decodeTodoOk () =
    let args =
        createObj [
            "completedWorkReport", box "本轮完成 codec 测试骨架"
            "select_methodology", box [| "test_driven_reasoning"; "deduction" |]
            "todos", box [|
                createObj [ "content", box "实现 codec"; "status", box "in_progress"; "priority", box "high" ]
                createObj [ "content", box "接线 HostTools"; "status", box "pending"; "priority", box "medium" ]
            |]
        ]
    match decodeTodoWriteArgs args with
    | Ok tw ->
        check "todo ok report" (tw.CompletedWorkReport = "本轮完成 codec 测试骨架")
        equal "todo ok todos count" 2 tw.Todos.Length
        check "todo ok first content" (tw.Todos.[0].Content = "实现 codec")
        check "todo ok first status" (tw.Todos.[0].Status = "in_progress")
        equal "todo ok methodology count" 2 tw.SelectMethodology.Length
        check "todo ok methodology head" (tw.SelectMethodology.[0] = "test_driven_reasoning")
    | Error _ -> check "todo ok" false

let decodeTodoToolOptsExtractsToolCallId () =
    let opts = createObj [ "toolCallId", box "call-abc-42"; "sessionId", box "sess-1" ]
    match decodeTodoToolOpts opts with
    | Ok o -> check "todo opts toolCallId" (o.ToolCallId = "call-abc-42")
    | Error _ -> check "todo opts toolCallId" false

let decodeTodoToolOptsMissingToolCallId () =
    let opts = createObj [ "sessionId", box "sess-1" ]
    match decodeTodoToolOpts opts with
    | Error (InvalidIntent ("todowrite", "toolCallId", "required")) ->
        check "todo opts missing toolCallId" true
    | _ -> check "todo opts missing toolCallId" false

let decodeTodoItemMissingContent () =
    let args =
        createObj [
            "completedWorkReport", box "report"
            "todos", box [| createObj [ "status", box "pending"; "priority", box "high" ] |]
        ]
    match decodeTodoWriteArgs args with
    | Error (InvalidIntent ("todowrite", "todos", msg)) ->
        check "todo item missing content" (msg.Contains "content")
    | _ -> check "todo item missing content" false

let run () =
    decodeTodoMissingCompletedWorkReport ()
    decodeTodoOk ()
    decodeTodoToolOptsExtractsToolCallId ()
    decodeTodoToolOptsMissingToolCallId ()
    decodeTodoItemMissingContent ()