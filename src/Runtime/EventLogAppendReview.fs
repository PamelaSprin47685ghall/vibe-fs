module Wanxiangshu.Runtime.EventLogAppendReview

open Fable.Core
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.ReviewRuntime

let private verdictPayload (verdict: string) (feedback: string option) =
    match feedback with
    | Some f when f <> "" -> Map.add "feedback" f (Map [ "verdict", verdict ])
    | _ -> Map [ "verdict", verdict ]

let appendReviewVerdict
    (workspaceRoot: string)
    (sessionID: string)
    (verdict: string)
    (feedback: string option)
    : JS.Promise<Result<unit, string>> =
    appendAndCache
        workspaceRoot
        (buildEvent sessionID eventKindReviewVerdict (verdictPayload verdict feedback) (getTimestampMs().ToString()))

let appendReviewVerdictOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (verdict: string)
    (feedback: string option)
    : JS.Promise<unit> =
    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindReviewVerdict (verdictPayload verdict feedback) (getTimestampMs().ToString()))

let appendSubmitReviewWipRecorded (workspaceRoot: string) (sessionID: string) : JS.Promise<Result<unit, string>> =
    appendAndCache
        workspaceRoot
        (buildEvent sessionID eventKindSubmitReviewWipRecorded Map.empty (getTimestampMs().ToString()))

let appendSubmitReviewWipRecordedOrFail (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindSubmitReviewWipRecorded Map.empty (getTimestampMs().ToString()))

let appendLoopActivated (workspaceRoot: string) (sessionID: string) (task: string) : JS.Promise<Result<unit, string>> =
    appendAndCache
        workspaceRoot
        (buildEvent sessionID eventKindLoopActivated (Map [ "task", task ]) (getTimestampMs().ToString()))

let appendLoopActivatedOrFail (workspaceRoot: string) (sessionID: string) (task: string) : JS.Promise<unit> =
    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindLoopActivated (Map [ "task", task ]) (getTimestampMs().ToString()))

let appendLoopCancelled (workspaceRoot: string) (sessionID: string) : JS.Promise<Result<unit, string>> =
    appendAndCache workspaceRoot (buildEvent sessionID eventKindLoopCancelled Map.empty (getTimestampMs().ToString()))

let appendLoopCancelledOrFail (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindLoopCancelled Map.empty (getTimestampMs().ToString()))

let syncReviewFromEventLogDedicated
    (store: ReviewStore)
    (workspaceRoot: string)
    (sessionID: string)
    : JS.Promise<unit> =
    promise {
        try
            if sessionID = "" || workspaceRoot = "" then
                ()
            else
                let! exists = directoryExists workspaceRoot

                if exists then
                    let! state = getStore(workspaceRoot).GetSessionState(sessionID)
                    syncReviewProjection store sessionID state.ReviewTask
        with _ ->
            ()
    }

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
