module Wanxiangshu.Runtime.EventLogRuntime

open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.EventLogRuntimeSync
open Wanxiangshu.Runtime.EventLogRuntimeNudge
open Wanxiangshu.Runtime.ReviewEventWriter
open Wanxiangshu.Runtime.BacklogEventWriter
open Wanxiangshu.Runtime.SessionEventWriter
open Wanxiangshu.Runtime.ContinuationEventWriter
open Wanxiangshu.Runtime.NudgeEventWriter
open Wanxiangshu.Runtime.SubsessionEventWriter

let getStore = EventLogRuntimeStore.getStore

let syncAllSessionsFromEventLogDedicated =
    EventLogRuntimeSync.syncAllSessionsFromEventLogDedicated

let syncReviewFromEventLogDedicated =
    ReviewEventWriter.syncReviewFromEventLogDedicated

let syncBacklogFromEventLogDedicated =
    EventLogRuntimeSync.syncBacklogFromEventLogDedicated

let appendReviewVerdict = ReviewEventWriter.appendReviewVerdict
let appendSubmitReviewWipRecorded = ReviewEventWriter.appendSubmitReviewWipRecorded
let appendLoopActivated = ReviewEventWriter.appendLoopActivated
let appendLoopCancelled = ReviewEventWriter.appendLoopCancelled
let appendNudgeDedupCleared = NudgeEventWriter.appendNudgeDedupCleared
let appendWorkBacklogCommitted = BacklogEventWriter.appendWorkBacklogCommitted
let appendReviewVerdictOrFail = ReviewEventWriter.appendReviewVerdictOrFail

let appendSubmitReviewWipRecordedOrFail =
    ReviewEventWriter.appendSubmitReviewWipRecordedOrFail

let appendLoopActivatedOrFail = ReviewEventWriter.appendLoopActivatedOrFail
let appendLoopCancelledOrFail = ReviewEventWriter.appendLoopCancelledOrFail
let appendNudgeDedupClearedOrFail = NudgeEventWriter.appendNudgeDedupClearedOrFail

let appendWorkBacklogCommittedOrFail =
    BacklogEventWriter.appendWorkBacklogCommittedOrFail

let appendAssistantCompleted = SessionEventWriter.appendAssistantCompleted

let appendAssistantCompletedOrFail =
    SessionEventWriter.appendAssistantCompletedOrFail

let appendHumanTurnStartedOrFail = SessionEventWriter.appendHumanTurnStartedOrFail
let appendUserAbortObservedOrFail = SessionEventWriter.appendUserAbortObservedOrFail
let appendCompactionStartedOrFail = SessionEventWriter.appendCompactionStartedOrFail
let appendCompactionSettledOrFail = SessionEventWriter.appendCompactionSettledOrFail

let appendContextGenerationChangedOrFail =
    SessionEventWriter.appendContextGenerationChangedOrFail

let appendCompactionContextGenerationChangedOrFail =
    SessionEventWriter.appendCompactionContextGenerationChangedOrFail

let appendRouteObservedOrFail = SessionEventWriter.appendRouteObservedOrFail

let appendFallbackContinueInjected =
    ContinuationEventWriter.appendFallbackContinueInjected

let appendFallbackContinueInjectedOrFail =
    ContinuationEventWriter.appendFallbackContinueInjectedOrFail

let appendContinuationRequestedOrFail =
    ContinuationEventWriter.appendContinuationRequestedOrFail

let appendContinuationDispatchStartedOrFail =
    ContinuationEventWriter.appendContinuationDispatchStartedOrFail

let appendContinuationDispatchedOrFail =
    ContinuationEventWriter.appendContinuationDispatchedOrFail

let appendContinuationFailedOrFail =
    ContinuationEventWriter.appendContinuationFailedOrFail

let appendContinuationCancelledOrFail =
    ContinuationEventWriter.appendContinuationCancelledOrFail

let appendContinuationSettledOrFail =
    ContinuationEventWriter.appendContinuationSettledOrFail

let appendNudgeRequestedOrFail = NudgeEventWriter.appendNudgeRequestedOrFail
let appendNudgeDispatchedOrFail = NudgeEventWriter.appendNudgeDispatchedOrFail
let appendNudgeFailedOrFail = NudgeEventWriter.appendNudgeFailedOrFail
let appendNudgeCancelledOrFail = NudgeEventWriter.appendNudgeCancelledOrFail
let appendNudgeSettledOrFail = NudgeEventWriter.appendNudgeSettledOrFail

let appendSubagentSpawned = SubsessionEventWriter.appendSubagentSpawned
let appendSubagentSpawnedOrFail = SubsessionEventWriter.appendSubagentSpawnedOrFail
let appendSubagentContinued = SubsessionEventWriter.appendSubagentContinued

let appendSubagentContinuedOrFail =
    SubsessionEventWriter.appendSubagentContinuedOrFail

let appendSubsessionRunStartedOrFail =
    SubsessionEventWriter.appendSubsessionRunStartedOrFail

let appendSubsessionRunSettledOrFail =
    SubsessionEventWriter.appendSubsessionRunSettledOrFail

let appendSubsessionDomainEventOrFail =
    SubsessionEventWriter.appendSubsessionDomainEventOrFail

let appendSubsessionDomainEventsOrFail =
    SubsessionEventWriter.appendSubsessionDomainEventsOrFail

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
