module Wanxiangshu.Runtime.BacklogEventWriter

open Fable.Core
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.WorkBacklogToolsCodec
open Thoth.Json

let private statusToString =
    function
    | TodoItemStatus.Todo -> "pending"
    | TodoItemStatus.InProgress -> "in_progress"
    | TodoItemStatus.Completed -> "completed"
    | TodoItemStatus.Cancelled -> "cancelled"

let private priorityToString =
    function
    | TodoItemPriority.Low -> "low"
    | TodoItemPriority.Medium -> "medium"
    | TodoItemPriority.High -> "high"

let private backlogPayload (args: TodoWriteArgs) =
    let mappedTodos =
        args.Todos
        |> Array.map (fun t ->
            {| Content = t.Content
               Status = statusToString t.Status
               Priority = priorityToString t.Priority |})

    Map
        [ "ahaMoments", args.AhaMoments
          "changesAndReasons", args.ChangesAndReasons
          "gotchas", args.Gotchas
          "lessonsAndConventions", args.LessonsAndConventions
          "plan", args.Plan
          "todosJson", Encode.Auto.toString (0, mappedTodos)
          "selectMethodologyJson", Encode.Auto.toString (0, args.SelectMethodology) ]

let appendWorkBacklogCommitted
    (workspaceRoot: string)
    (sessionID: string)
    (args: TodoWriteArgs)
    : JS.Promise<Result<unit, string>> =
    appendAndCache
        workspaceRoot
        (buildEvent sessionID eventKindWorkBacklogCommitted (backlogPayload args) (getTimestampMs().ToString()))

let appendWorkBacklogCommittedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (args: TodoWriteArgs)
    : JS.Promise<unit> =
    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindWorkBacklogCommitted (backlogPayload args) (getTimestampMs().ToString()))
