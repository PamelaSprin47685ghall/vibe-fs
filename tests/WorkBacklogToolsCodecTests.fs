module Wanxiangshu.Tests.WorkBacklogToolsCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.WorkBacklogToolsCodec

let decodeTodoMissingCompletedWorkReport () =
    let args =
        createObj
            [ "ahaMoments", box ""
              "changesAndReasons", box ""
              "gotchas", box ""
              "lessonsAndConventions", box ""
              "plan", box ""
              "todos", box [| createObj [ "content", box "x"; "status", box "pending"; "priority", box "high" ] |]
              "select_methodology", box [| "first_principles" |] ]

    match decodeTodoWriteArgs false args with
    | Error(InvalidIntent("todowrite", "ahaMoments", _)) -> check "todo missing ahaMoments" true
    | _ -> check "todo missing ahaMoments" false

let decodeTodoOk () =
    let args =
        createObj
            [ "ahaMoments", box ("x".PadRight(1024, 'a'))
              "changesAndReasons", box ("x".PadRight(1024, 'b'))
              "gotchas", box ("x".PadRight(1024, 'c'))
              "lessonsAndConventions", box ("x".PadRight(1024, 'd'))
              "plan", box ("x".PadRight(1024, 'e'))
              "select_methodology", box [| "test_driven_reasoning"; "deduction" |]
              "todos",
              box
                  [| createObj
                         [ "content", box "实现 codec"
                           "status", box "in_progress"
                           "priority", box "high" ]
                     createObj
                         [ "content", box "接线 HostTools"
                           "status", box "pending"
                           "priority", box "medium" ] |] ]

    match decodeTodoWriteArgs false args with
    | Ok tw ->
        check "todo ok ahaMoments" (tw.AhaMoments = "x".PadRight(1024, 'a'))
        equal "todo ok todos count" 2 tw.Todos.Length
        check "todo ok first content" (tw.Todos.[0].Content = "实现 codec")
        check "todo ok first status" (tw.Todos.[0].Status = Wanxiangshu.Kernel.ToolArgs.TodoItemStatus.InProgress)
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
    | Error(InvalidIntent("todowrite", "toolCallId", "required")) -> check "todo opts missing toolCallId" true
    | _ -> check "todo opts missing toolCallId" false

let decodeTodoItemMissingAhaMoments () =
    let args =
        createObj
            [ "ahaMoments", box ""
              "changesAndReasons", box ""
              "gotchas", box ""
              "lessonsAndConventions", box ""
              "plan", box ""
              "todos", box [| createObj [ "status", box "pending"; "priority", box "high" ] |] ]

    match decodeTodoWriteArgs false args with
    | Error(InvalidIntent("todowrite", "ahaMoments", _)) -> check "todo item missing ahaMoments" true
    | _ -> check "todo item missing ahaMoments" false

let decodeTodoInvalidStatusOrPriority () =
    let args =
        createObj
            [ "ahaMoments", box ("x".PadRight(1024, 'a'))
              "changesAndReasons", box ("x".PadRight(1024, 'b'))
              "gotchas", box ("x".PadRight(1024, 'c'))
              "lessonsAndConventions", box ("x".PadRight(1024, 'd'))
              "plan", box ("x".PadRight(1024, 'e'))
              "select_methodology", box [| "test_driven_reasoning" |]
              "todos",
              box
                  [| createObj
                         [ "content", box "实现 codec"
                           "status", box "invalid-status"
                           "priority", box "high" ] |] ]

    match decodeTodoWriteArgs false args with
    | Error(InvalidIntent("todowrite", "todos", msg)) ->
        check "todo invalid status gets error" (msg.Contains("unknown status: invalid-status"))
    | _ -> check "todo invalid status gets error" false

let run () =
    decodeTodoMissingCompletedWorkReport ()
    decodeTodoOk ()
    decodeTodoToolOptsExtractsToolCallId ()
    decodeTodoToolOptsMissingToolCallId ()
    decodeTodoItemMissingAhaMoments ()
    decodeTodoInvalidStatusOrPriority ()
