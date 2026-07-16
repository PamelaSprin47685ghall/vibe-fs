module Wanxiangshu.Runtime.EventLogRuntimeAppend

open Fable.Core
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.WorkBacklogToolsCodec
open Thoth.Json

let private statusToString =
    function
    | ToolArgs.Todo -> "pending"
    | ToolArgs.InProgress -> "in_progress"
    | ToolArgs.Completed -> "completed"
    | ToolArgs.Cancelled -> "cancelled"

let private priorityToString =
    function
    | ToolArgs.Low -> "low"
    | ToolArgs.Medium -> "medium"
    | ToolArgs.High -> "high"

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

let private append (workspaceRoot: string) (e: WanEvent) : JS.Promise<Result<unit, string>> =
    appendAndCache workspaceRoot e

let private appendOrFail (workspaceRoot: string) (e: WanEvent) : JS.Promise<unit> = appendAndCacheOrFail workspaceRoot e

let appendWorkBacklogCommitted
    (workspaceRoot: string)
    (sessionID: string)
    (args: TodoWriteArgs)
    : JS.Promise<Result<unit, string>> =
    append
        workspaceRoot
        (buildEvent sessionID eventKindWorkBacklogCommitted (backlogPayload args) (getTimestampMs().ToString()))

let appendWorkBacklogCommittedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (args: TodoWriteArgs)
    : JS.Promise<unit> =
    appendOrFail
        workspaceRoot
        (buildEvent sessionID eventKindWorkBacklogCommitted (backlogPayload args) (getTimestampMs().ToString()))

let appendSubagentSpawned
    (workspaceRoot: string)
    (sessionID: string)
    (childId: string)
    (agent: string)
    (title: string)
    : JS.Promise<Result<unit, string>> =
    append
        workspaceRoot
        (buildEvent
            sessionID
            eventKindSubagentSpawned
            (Map [ "childId", childId; "agent", agent; "title", title ])
            (getTimestampMs().ToString()))

let appendSubagentSpawnedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (childId: string)
    (agent: string)
    (title: string)
    : JS.Promise<unit> =
    appendOrFail
        workspaceRoot
        (buildEvent
            sessionID
            eventKindSubagentSpawned
            (Map [ "childId", childId; "agent", agent; "title", title ])
            (getTimestampMs().ToString()))

let appendSubagentContinued
    (workspaceRoot: string)
    (sessionID: string)
    (childId: string)
    (prompt: string)
    : JS.Promise<Result<unit, string>> =
    append
        workspaceRoot
        (buildEvent
            sessionID
            eventKindSubagentContinued
            (Map [ "childId", childId; "prompt", prompt ])
            (getTimestampMs().ToString()))

let appendSubagentContinuedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (childId: string)
    (prompt: string)
    : JS.Promise<unit> =
    appendOrFail
        workspaceRoot
        (buildEvent
            sessionID
            eventKindSubagentContinued
            (Map [ "childId", childId; "prompt", prompt ])
            (getTimestampMs().ToString()))

let appendSubsessionRunStartedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (childId: string)
    (parentSessionId: string)
    (runId: string)
    : JS.Promise<unit> =
    let payload =
        Map [ "childId", childId; "parentSessionId", parentSessionId; "runId", runId ]

    appendOrFail
        workspaceRoot
        (buildEvent sessionID eventKindSubsessionRunStarted payload (getTimestampMs().ToString()))

let appendSubsessionRunSettledOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (childId: string)
    (runId: string)
    (status: string)
    : JS.Promise<unit> =
    let payload = Map [ "childId", childId; "runId", runId; "status", status ]

    appendOrFail
        workspaceRoot
        (buildEvent sessionID eventKindSubsessionRunSettled payload (getTimestampMs().ToString()))

let appendSubsessionDomainEventOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (kind: string)
    (payload: Map<string, string>)
    : JS.Promise<unit> =
    appendOrFail workspaceRoot (buildEvent sessionID kind payload (getTimestampMs().ToString()))

/// Atomic multi-event append for one Subsession Decision.
let appendSubsessionDomainEventsOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (items: (string * Map<string, string>) list)
    : JS.Promise<unit> =
    if List.isEmpty items then
        Promise.lift ()
    else
        let at = getTimestampMs().ToString()

        let events =
            items |> List.map (fun (kind, payload) -> buildEvent sessionID kind payload at)

        appendEventsAndCacheOrFail workspaceRoot events
