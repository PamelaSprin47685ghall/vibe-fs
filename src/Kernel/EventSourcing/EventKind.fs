module Wanxiangshu.Kernel.EventSourcing.EventKind

let eventKindAssistantCompleted = "assistant_completed"
let eventKindLoopActivated = "loop_activated"
let eventKindLoopCancelled = "loop_cancelled"
let eventKindReviewVerdict = "review_verdict"
let eventKindWorkBacklogCommitted = "work_backlog_committed"
let eventKindNudgeDispatched = "nudge_dispatched"
let eventKindNudgeRequested = "nudge_requested"
let eventKindNudgeFailed = "nudge_failed"
let eventKindNudgeCancelled = "nudge_cancelled"
let eventKindNudgeSettled = "nudge_settled"
let eventKindSubmitReviewWipRecorded = "submit_review_wip_recorded"
let eventKindNudgeDedupCleared = "nudge_dedup_cleared"
let eventKindFallbackContinueInjected = "fallback_continue_injected"

let eventKindHumanTurnStarted = "human_turn_started"
let eventKindUserAbortObserved = "user_abort_observed"
let eventKindContinuationRequested = "continuation_requested"
let eventKindContinuationDispatchStarted = "continuation_dispatch_started"
let eventKindContinuationDispatched = "continuation_dispatched"
let eventKindContinuationFailed = "continuation_failed"
let eventKindContinuationCancelled = "continuation_cancelled"
let eventKindContinuationSettled = "continuation_settled"
let eventKindCompactionStarted = "compaction_started"
let eventKindContinuationDispatchClaimed = "continuation_dispatch_claimed"
let eventKindContinuationHostAccepted = "continuation_host_accepted"
let eventKindContinuationRunStarted = "continuation_run_started"
let eventKindContinuationSuperseded = "continuation_superseded"
let eventKindContinuationAssistantObserved = "continuation_assistant_observed"
let eventKindCompactionSettled = "compaction_settled"
let eventKindContextGenerationChanged = "context_generation_changed"
let eventKindRouteObserved = "route_observed"

let eventKindSubagentSpawned = "subagent_spawned"
let eventKindSubagentContinued = "subagent_continued"

let eventKindSquadCreated = "squad_created"
let eventKindTasksCreated = "tasks_created"
let eventKindTaskStarted = "task_started"
let eventKindTaskSubmitted = "task_submitted"
let eventKindTaskMerged = "task_merged"
let eventKindTaskDone = "task_done"
let eventKindTaskError = "task_error"
let eventKindSquadCancelled = "squad_cancelled"

let eventKindSubsessionRunStarted = "subsession_run_started"
let eventKindSubsessionRunSettled = "subsession_run_settled"
let eventKindSubsessionTurnDispatchRequested = "subsession_turn_dispatch_requested"
let eventKindSubsessionTurnStarted = "subsession_turn_started"
let eventKindSubsessionTurnOutcomeObserved = "subsession_turn_outcome_observed"
let eventKindSubsessionTurnFinished = "subsession_turn_finished"
let eventKindSubsessionAbortRequested = "subsession_abort_requested"
let eventKindSubsessionSessionPoisoned = "subsession_session_poisoned"
let eventKindSubsessionPhysicalSessionClosed = "subsession_physical_session_closed"
let eventKindSubsessionDecisionCommitted = "subsession_decision_committed"

let verdictAccepted = Wanxiangshu.Kernel.Review.ReviewVerdictWire.accepted
let verdictCancelled = Wanxiangshu.Kernel.Review.ReviewVerdictWire.cancelled
let verdictNeedsRevision = Wanxiangshu.Kernel.Review.ReviewVerdictWire.needsRevision
let verdictTerminated = Wanxiangshu.Kernel.Review.ReviewVerdictWire.terminated
let isEndVerdict = Wanxiangshu.Kernel.Review.ReviewVerdictWire.isEndVerdict
