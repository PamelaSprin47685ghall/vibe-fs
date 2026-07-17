module Wanxiangshu.Runtime.ContinuationEventWriter

open Fable.Core
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.Clock

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
