module Wanxiangshu.Kernel.Nudge.NudgeSnapshotSource

open Wanxiangshu.Kernel.EventLog.ReviewLoopFold
open Wanxiangshu.Kernel.Nudge.Types

[<RequireQualifiedAccess>]
type RunnerPresence =
    | Absent
    | Active

/// Shell boundary input for nudge derivation (no parallel bool axes).
type NudgeSnapshotSource =
    { openTodos: string list
      lastAssistantText: string
      agentFromMessage: string option
      modelFromMessage: string option
      reviewLoop: ReviewLoopFold
      runnerPresence: RunnerPresence
      blockStatus: NudgeBlockStatus
      turnId: string }

let nudgeAnchorKeyForSource (source: NudgeSnapshotSource) : string =
    let body = source.lastAssistantText.Trim()
    let tid = source.turnId.Trim()
    if tid = "" then body else tid + "\u001e" + body

let workStateFromSource (source: NudgeSnapshotSource) : SessionWorkState =
    let hasRunner =
        match source.runnerPresence with
        | RunnerPresence.Active -> true
        | RunnerPresence.Absent -> false

    workStateFromAxes hasRunner (isLoopActive source.reviewLoop) source.openTodos
