module Wanxiangshu.Runtime.ReviewEventWriter

open Fable.Core
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
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
