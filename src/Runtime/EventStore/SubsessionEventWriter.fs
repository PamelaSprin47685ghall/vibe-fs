module Wanxiangshu.Runtime.SubsessionEventWriter

open Fable.Core
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.Clock

let appendSubagentSpawned
    (workspaceRoot: string)
    (sessionID: string)
    (childId: string)
    (agent: string)
    (title: string)
    : JS.Promise<Result<unit, string>> =
    appendAndCache
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
    appendAndCacheOrFail
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
    appendAndCache
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
    appendAndCacheOrFail
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

    appendAndCacheOrFail
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

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindSubsessionRunSettled payload (getTimestampMs().ToString()))

let appendSubsessionDomainEventOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (kind: string)
    (payload: Map<string, string>)
    : JS.Promise<unit> =
    appendAndCacheOrFail workspaceRoot (buildEvent sessionID kind payload (getTimestampMs().ToString()))

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
