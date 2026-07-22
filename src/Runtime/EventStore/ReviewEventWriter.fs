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

let appendSubmitReviewWipRecorded
    (workspaceRoot: string)
    (sessionID: string)
    (report: string)
    : JS.Promise<Result<unit, string>> =
    let payload =
        if report.Trim() <> "" then
            Map [ "report", report.Trim() ]
        else
            Map.empty

    appendAndCache
        workspaceRoot
        (buildEvent sessionID eventKindSubmitReviewWipRecorded payload (getTimestampMs().ToString()))

let appendSubmitReviewWipRecordedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (report: string)
    : JS.Promise<unit> =
    let payload =
        if report.Trim() <> "" then
            Map [ "report", report.Trim() ]
        else
            Map.empty

    appendAndCacheOrFail
        workspaceRoot
        (buildEvent sessionID eventKindSubmitReviewWipRecorded payload (getTimestampMs().ToString()))

/// Append submit_review_reports_consumed event to signal that accumulated
/// WIP reports have been consumed in a final submission.
let appendSubmitReviewReportsConsumedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (count: int)
    : JS.Promise<unit> =
    appendAndCacheOrFail
        workspaceRoot
        (buildEvent
            sessionID
            eventKindSubmitReviewReportsConsumed
            (Map [ "count", string count ])
            (getTimestampMs().ToString()))

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

let verdictStringFromReviewResult
    (result: Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult)
    : string * string option =
    let joined (items: string list) =
        match items with
        | [] -> None
        | xs -> Some(String.concat "\n" xs)

    match result with
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Accepted fb -> (verdictAccepted, joined fb)
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.NeedsRevision fb -> (verdictNeedsRevision, joined fb)
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Terminated -> (verdictTerminated, None)
