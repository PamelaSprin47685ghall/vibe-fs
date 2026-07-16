module Wanxiangshu.Runtime.EventLogAppendSession

open Fable.Core
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.Clock
open Thoth.Json

let appendAssistantCompleted
    (workspaceRoot: string)
    (sessionID: string)
    (assistantMessage: string)
    (agent: string option)
    (model: string option)
    (turnId: string)
    (openTodos: string list)
    : JS.Promise<Result<unit, string>> =
    appendAndCache
        workspaceRoot
        (buildEvent
            sessionID
            eventKindAssistantCompleted
            (assistantPayload assistantMessage agent model turnId openTodos)
            (getTimestampMs().ToString()))

let appendAssistantCompletedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (assistantMessage: string)
    (agent: string option)
    (model: string option)
    (turnId: string)
    (openTodos: string list)
    : JS.Promise<unit> =
    appendAndCacheOrFail
        workspaceRoot
        (buildEvent
            sessionID
            eventKindAssistantCompleted
            (assistantPayload assistantMessage agent model turnId openTodos)
            (getTimestampMs().ToString()))

let appendHumanTurnStartedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (turnId: string)
    (provider: string)
    (model: string)
    (variant: string)
    (agent: string)
    (humanTurnOrdinal: int)
    (messageId: string)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "turnId", turnId
              "provider", provider
              "model", model
              "variant", variant
              "agent", agent
              "humanTurnOrdinal", humanTurnOrdinal.ToString()
              "messageId", messageId ]

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindHumanTurnStarted payload (getTimestampMs().ToString()))

let appendUserAbortObservedOrFail (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindUserAbortObserved Map.empty (getTimestampMs().ToString()))

let appendCompactionStartedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (compactionId: string)
    (generationAtStart: int)
    (humanTurnId: string)
    (compactionOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "compactionId", compactionId
              "generationAtStart", generationAtStart.ToString()
              "humanTurnId", humanTurnId
              "compactionOrdinal", compactionOrdinal.ToString() ]

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindCompactionStarted payload (getTimestampMs().ToString()))

let appendCompactionSettledOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (compactionId: string)
    (status: string)
    (compactionOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "compactionId", compactionId
              "status", status
              "compactionOrdinal", compactionOrdinal.ToString() ]

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindCompactionSettled payload (getTimestampMs().ToString()))

let appendContextGenerationChangedOrFail (workspaceRoot: string) (sessionID: string) (newGen: int) : JS.Promise<unit> =
    let payload = Map [ "generation", newGen.ToString() ]

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindContextGenerationChanged payload (getTimestampMs().ToString()))

let appendCompactionContextGenerationChangedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (newGen: int)
    (compactionId: string)
    (compactionOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "generation", newGen.ToString()
              "compactionId", compactionId
              "compactionOrdinal", compactionOrdinal.ToString() ]

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindContextGenerationChanged payload (getTimestampMs().ToString()))

let appendRouteObservedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (provider: string)
    (model: string)
    (variant: string)
    (agent: string)
    : JS.Promise<unit> =
    let payload =
        Map [ "provider", provider; "model", model; "variant", variant; "agent", agent ]

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindRouteObserved payload (getTimestampMs().ToString()))

let private fallbackInjectionPayload (modelStr: string) (agentStr: string) (atMs: int64) =
    Map [ "model", modelStr; "agent", agentStr; "at", atMs.ToString() ]

let appendFallbackContinueInjected
    (workspaceRoot: string)
    (sessionID: string)
    (modelStr: string)
    (agentStr: string)
    (atMs: int64)
    : JS.Promise<Result<unit, string>> =
    appendAndCache
        workspaceRoot
        (buildEvent
            sessionID
            eventKindFallbackContinueInjected
            (fallbackInjectionPayload modelStr agentStr atMs)
            (getTimestampMs().ToString()))

let appendFallbackContinueInjectedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (modelStr: string)
    (agentStr: string)
    (atMs: int64)
    : JS.Promise<unit> =
    appendAndCacheOrFail
        workspaceRoot
        (buildEvent
            sessionID
            eventKindFallbackContinueInjected
            (fallbackInjectionPayload modelStr agentStr atMs)
            (getTimestampMs().ToString()))

let appendContinuationRequestedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (continuationID: string)
    (modelStr: string)
    (agentStr: string)
    (atMs: int64)
    (generation: int)
    (cancelGeneration: int)
    (humanTurnID: string)
    (owner: string)
    (continuationOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "continuationId", continuationID
              "model", modelStr
              "agent", agentStr
              "at", atMs.ToString()
              "generation", generation.ToString()
              "cancelGeneration", cancelGeneration.ToString()
              "humanTurnId", humanTurnID
              "owner", owner
              "continuationOrdinal", continuationOrdinal.ToString() ]

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindContinuationRequested payload (getTimestampMs().ToString()))

let appendContinuationDispatchStartedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (continuationID: string)
    (continuationOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "continuationId", continuationID
              "continuationOrdinal", continuationOrdinal.ToString() ]

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindContinuationDispatchStarted payload (getTimestampMs().ToString()))

let appendContinuationDispatchedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (continuationID: string)
    (modelStr: string)
    (agentStr: string)
    (atMs: int64)
    (continuationOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "continuationId", continuationID
              "model", modelStr
              "agent", agentStr
              "at", atMs.ToString()
              "continuationOrdinal", continuationOrdinal.ToString() ]

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindContinuationDispatched payload (getTimestampMs().ToString()))

let appendContinuationFailedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (continuationID: string)
    (errorMsg: string)
    (continuationOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "continuationId", continuationID
              "error", errorMsg
              "continuationOrdinal", continuationOrdinal.ToString() ]

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindContinuationFailed payload (getTimestampMs().ToString()))

let appendContinuationCancelledOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (continuationID: string)
    (reason: string)
    (continuationOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "continuationId", continuationID
              "reason", reason
              "continuationOrdinal", continuationOrdinal.ToString() ]

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindContinuationCancelled payload (getTimestampMs().ToString()))

let appendContinuationSettledOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (continuationID: string)
    (humanTurnID: string)
    (generation: int)
    (status: string)
    (continuationOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "continuationId", continuationID
              "humanTurnId", humanTurnID
              "generation", generation.ToString()
              "status", status
              "continuationOrdinal", continuationOrdinal.ToString() ]

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindContinuationSettled payload (getTimestampMs().ToString()))
