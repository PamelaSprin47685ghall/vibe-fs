module Wanxiangshu.Kernel.EventLog.Types

/// One persisted line in `[workspace]/.wanxiangshu.ndjson` (schema v1).
type WanEvent =
    { V: int
      Session: string
      Kind: string
      At: string
      Payload: Map<string, string> }

let eventKindAssistantCompleted = "assistant_completed"
let eventKindLoopActivated = "loop_activated"
let eventKindLoopCancelled = "loop_cancelled"
let eventKindReviewVerdict = "review_verdict"
let eventKindWorkBacklogCommitted = "work_backlog_committed"
let eventKindNudgeDispatched = "nudge_dispatched"
let eventKindSubmitReviewWipRecorded = "submit_review_wip_recorded"
let eventKindNudgeDedupCleared = "nudge_dedup_cleared"
let eventKindFallbackContinueInjected = "fallback_continue_injected"

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

let verdictAccepted = "accepted"
let verdictCancelled = "cancelled"
let verdictNeedsRevision = "needs_revision"
let verdictTerminated = "terminated"

let isEndVerdict (verdict: string) : bool =
    verdict = verdictAccepted
    || verdict = verdictCancelled
    || verdict = verdictTerminated
