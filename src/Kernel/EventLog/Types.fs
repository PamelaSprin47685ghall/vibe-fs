module Wanxiangshu.Kernel.EventLog.Types

/// One persisted line in `[workspace]/.wanxiangshu.ndjson` (schema v1).
type WanEvent = {
    V: int
    Session: string
    Kind: string
    At: string
    Payload: Map<string, string>
}

let eventKindLoopActivated = "loop_activated"
let eventKindLoopCancelled = "loop_cancelled"
let eventKindReviewVerdict = "review_verdict"
let eventKindWorkBacklogCommitted = "work_backlog_committed"
let eventKindNudgeDispatched = "nudge_dispatched"
let eventKindSubmitReviewWipRecorded = "submit_review_wip_recorded"
let eventKindNudgeDedupCleared = "nudge_dedup_cleared"

let verdictAccepted = "accepted"
let verdictCancelled = "cancelled"
let verdictRejected = "rejected"
let verdictTerminated = "terminated"

let isEndVerdict (verdict: string) : bool =
    verdict = verdictAccepted || verdict = verdictCancelled