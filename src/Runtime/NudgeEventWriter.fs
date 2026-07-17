module Wanxiangshu.Runtime.NudgeEventWriter

open Fable.Core
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventLogRuntimeStore

let appendNudgeDedupCleared (workspaceRoot: string) (sessionID: string) : JS.Promise<Result<unit, string>> =
    appendAndCache
        workspaceRoot
        (buildEvent sessionID eventKindNudgeDedupCleared Map.empty (getTimestampMs().ToString()))

let appendNudgeDedupClearedOrFail (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindNudgeDedupCleared Map.empty (getTimestampMs().ToString()))

let appendNudgeRequestedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (nudgeID: string)
    (action: string)
    (anchor: string)
    (sessionGen: int)
    (cancelGen: int)
    (humanTurnID: string)
    (nudgeOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "nudgeId", nudgeID
              "action", action
              "anchor", anchor
              "generation", sessionGen.ToString()
              "cancelGeneration", cancelGen.ToString()
              "humanTurnId", humanTurnID
              "nudgeOrdinal", nudgeOrdinal.ToString() ]

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindNudgeRequested payload (getTimestampMs().ToString()))

let appendNudgeDispatchedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (nudgeID: string)
    (action: string)
    (anchor: string)
    (nudgeOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "nudgeId", nudgeID
              "action", action
              "anchor", anchor
              "nudgeOrdinal", nudgeOrdinal.ToString() ]

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindNudgeDispatched payload (getTimestampMs().ToString()))

let appendNudgeFailedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (nudgeID: string)
    (errorMsg: string)
    (nudgeOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "nudgeId", nudgeID
              "error", errorMsg
              "nudgeOrdinal", nudgeOrdinal.ToString() ]

    appendAndCacheOrFail workspaceRoot (buildEvent sessionID eventKindNudgeFailed payload (getTimestampMs().ToString()))

let appendNudgeCancelledOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (nudgeID: string)
    (reason: string)
    (nudgeOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "nudgeId", nudgeID
              "reason", reason
              "nudgeOrdinal", nudgeOrdinal.ToString() ]

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindNudgeCancelled payload (getTimestampMs().ToString()))

let appendNudgeSettledOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (nudgeID: string)
    (status: string)
    (nudgeOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "nudgeId", nudgeID
              "status", status
              "nudgeOrdinal", nudgeOrdinal.ToString() ]

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindNudgeSettled payload (getTimestampMs().ToString()))
