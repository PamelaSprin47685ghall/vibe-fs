module Wanxiangshu.Runtime.EventLogRuntime

open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.EventLogRuntimeSync
open Wanxiangshu.Runtime.EventLogRuntimeAppend
open Wanxiangshu.Runtime.EventLogRuntimeNudge
open Wanxiangshu.Runtime.EventLogAppendReview
open Wanxiangshu.Runtime.EventLogAppendSession

let getStore = EventLogRuntimeStore.getStore

let syncAllSessionsFromEventLogDedicated =
    EventLogRuntimeSync.syncAllSessionsFromEventLogDedicated

let syncReviewFromEventLogDedicated =
    EventLogAppendReview.syncReviewFromEventLogDedicated

let syncBacklogFromEventLogDedicated =
    EventLogRuntimeSync.syncBacklogFromEventLogDedicated

let appendLoopActivated = EventLogAppendReview.appendLoopActivated
let appendLoopCancelled = EventLogAppendReview.appendLoopCancelled
let appendReviewVerdict = EventLogAppendReview.appendReviewVerdict

let appendSubmitReviewWipRecorded =
    EventLogAppendReview.appendSubmitReviewWipRecorded

let appendNudgeDedupCleared = EventLogAppendReview.appendNudgeDedupCleared
let appendWorkBacklogCommitted = EventLogRuntimeAppend.appendWorkBacklogCommitted
let appendLoopActivatedOrFail = EventLogAppendReview.appendLoopActivatedOrFail
let appendLoopCancelledOrFail = EventLogAppendReview.appendLoopCancelledOrFail
let appendReviewVerdictOrFail = EventLogAppendReview.appendReviewVerdictOrFail

let appendSubmitReviewWipRecordedOrFail =
    EventLogAppendReview.appendSubmitReviewWipRecordedOrFail

let appendNudgeDedupClearedOrFail =
    EventLogAppendReview.appendNudgeDedupClearedOrFail

let appendWorkBacklogCommittedOrFail =
    EventLogRuntimeAppend.appendWorkBacklogCommittedOrFail

let appendAssistantCompleted = EventLogAppendSession.appendAssistantCompleted

let appendAssistantCompletedOrFail =
    EventLogAppendSession.appendAssistantCompletedOrFail

let appendHumanTurnStartedOrFail =
    EventLogAppendSession.appendHumanTurnStartedOrFail

let appendUserAbortObservedOrFail =
    EventLogAppendSession.appendUserAbortObservedOrFail

let appendContinuationRequestedOrFail =
    EventLogAppendSession.appendContinuationRequestedOrFail

let appendContinuationDispatchStartedOrFail =
    EventLogAppendSession.appendContinuationDispatchStartedOrFail

let appendContinuationDispatchedOrFail =
    EventLogAppendSession.appendContinuationDispatchedOrFail

let appendContinuationFailedOrFail =
    EventLogAppendSession.appendContinuationFailedOrFail

let appendContinuationCancelledOrFail =
    EventLogAppendSession.appendContinuationCancelledOrFail

let appendContinuationSettledOrFail =
    EventLogAppendSession.appendContinuationSettledOrFail

let appendCompactionStartedOrFail =
    EventLogAppendSession.appendCompactionStartedOrFail

let appendCompactionSettledOrFail =
    EventLogAppendSession.appendCompactionSettledOrFail

let appendContextGenerationChangedOrFail =
    EventLogAppendSession.appendContextGenerationChangedOrFail

let appendCompactionContextGenerationChangedOrFail =
    EventLogAppendSession.appendCompactionContextGenerationChangedOrFail

let appendRouteObservedOrFail = EventLogAppendSession.appendRouteObservedOrFail

let appendNudgeRequestedOrFail = EventLogAppendReview.appendNudgeRequestedOrFail
let appendNudgeDispatchedOrFail = EventLogAppendReview.appendNudgeDispatchedOrFail
let appendNudgeFailedOrFail = EventLogAppendReview.appendNudgeFailedOrFail
let appendNudgeCancelledOrFail = EventLogAppendReview.appendNudgeCancelledOrFail
let appendNudgeSettledOrFail = EventLogAppendReview.appendNudgeSettledOrFail

let appendSubagentSpawned = EventLogRuntimeAppend.appendSubagentSpawned
let appendSubagentSpawnedOrFail = EventLogRuntimeAppend.appendSubagentSpawnedOrFail
let appendSubagentContinued = EventLogRuntimeAppend.appendSubagentContinued

let appendSubagentContinuedOrFail =
    EventLogRuntimeAppend.appendSubagentContinuedOrFail

let isLoopActiveFromEventLog = EventLogRuntimeNudge.isLoopActiveFromEventLog
let nudgeBlockedForTurn = EventLogRuntimeNudge.nudgeBlockedForTurn
let tryClaimNudgeDispatch = EventLogRuntimeNudge.tryClaimNudgeDispatch
let getNudgeSnapshotFromEventLog = EventLogRuntimeNudge.getNudgeSnapshotFromEventLog

let verdictStringFromReviewResult
    (result: Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult)
    : string * string option =
    match result with
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Accepted fb -> (verdictAccepted, Some fb)
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.NeedsRevision fb -> (verdictNeedsRevision, Some fb)
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Terminated -> (verdictTerminated, None)
