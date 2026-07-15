module Wanxiangshu.Shell.EventLogRuntime

open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Shell.EventLogRuntimeStore
open Wanxiangshu.Shell.EventLogRuntimeSync
open Wanxiangshu.Shell.EventLogRuntimeAppend
open Wanxiangshu.Shell.EventLogRuntimeNudge

let getStore = EventLogRuntimeStore.getStore

let syncAllSessionsFromEventLogDedicated =
    EventLogRuntimeSync.syncAllSessionsFromEventLogDedicated

let syncReviewFromEventLogDedicated =
    EventLogRuntimeSync.syncReviewFromEventLogDedicated

let syncBacklogFromEventLogDedicated =
    EventLogRuntimeSync.syncBacklogFromEventLogDedicated

let appendLoopActivated = EventLogRuntimeAppend.appendLoopActivated
let appendLoopCancelled = EventLogRuntimeAppend.appendLoopCancelled
let appendReviewVerdict = EventLogRuntimeAppend.appendReviewVerdict

let appendSubmitReviewWipRecorded =
    EventLogRuntimeAppend.appendSubmitReviewWipRecorded

let appendNudgeDedupCleared = EventLogRuntimeAppend.appendNudgeDedupCleared
let appendWorkBacklogCommitted = EventLogRuntimeAppend.appendWorkBacklogCommitted
let appendLoopActivatedOrFail = EventLogRuntimeAppend.appendLoopActivatedOrFail
let appendLoopCancelledOrFail = EventLogRuntimeAppend.appendLoopCancelledOrFail
let appendReviewVerdictOrFail = EventLogRuntimeAppend.appendReviewVerdictOrFail

let appendSubmitReviewWipRecordedOrFail =
    EventLogRuntimeAppend.appendSubmitReviewWipRecordedOrFail

let appendNudgeDedupClearedOrFail =
    EventLogRuntimeAppend.appendNudgeDedupClearedOrFail

let appendWorkBacklogCommittedOrFail =
    EventLogRuntimeAppend.appendWorkBacklogCommittedOrFail

let appendAssistantCompleted = EventLogRuntimeAppend.appendAssistantCompleted

let appendAssistantCompletedOrFail =
    EventLogRuntimeAppend.appendAssistantCompletedOrFail

let appendHumanTurnStartedOrFail =
    EventLogRuntimeAppend.appendHumanTurnStartedOrFail

let appendUserAbortObservedOrFail =
    EventLogRuntimeAppend.appendUserAbortObservedOrFail

let appendContinuationRequestedOrFail =
    EventLogRuntimeAppend.appendContinuationRequestedOrFail

let appendContinuationDispatchStartedOrFail =
    EventLogRuntimeAppend.appendContinuationDispatchStartedOrFail

let appendContinuationDispatchedOrFail =
    EventLogRuntimeAppend.appendContinuationDispatchedOrFail

let appendContinuationFailedOrFail =
    EventLogRuntimeAppend.appendContinuationFailedOrFail

let appendContinuationCancelledOrFail =
    EventLogRuntimeAppend.appendContinuationCancelledOrFail

let appendContinuationSettledOrFail =
    EventLogRuntimeAppend.appendContinuationSettledOrFail

let appendCompactionStartedOrFail =
    EventLogRuntimeAppend.appendCompactionStartedOrFail

let appendCompactionSettledOrFail =
    EventLogRuntimeAppend.appendCompactionSettledOrFail

let appendContextGenerationChangedOrFail =
    EventLogRuntimeAppend.appendContextGenerationChangedOrFail

let appendCompactionContextGenerationChangedOrFail =
    EventLogRuntimeAppend.appendCompactionContextGenerationChangedOrFail

let appendRouteObservedOrFail = EventLogRuntimeAppend.appendRouteObservedOrFail

let appendNudgeRequestedOrFail = EventLogRuntimeAppend.appendNudgeRequestedOrFail
let appendNudgeDispatchedOrFail = EventLogRuntimeAppend.appendNudgeDispatchedOrFail
let appendNudgeFailedOrFail = EventLogRuntimeAppend.appendNudgeFailedOrFail
let appendNudgeCancelledOrFail = EventLogRuntimeAppend.appendNudgeCancelledOrFail
let appendNudgeSettledOrFail = EventLogRuntimeAppend.appendNudgeSettledOrFail

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
