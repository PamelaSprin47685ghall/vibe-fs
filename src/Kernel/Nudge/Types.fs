module Wanxiangshu.Kernel.Nudge.Types

/// Nudge priority axes as a single DU (no nested bool tuples).
[<RequireQualifiedAccess>]
type SessionWorkState =
    | Idle
    | BacklogOnly
    | LoopIdle
    | LoopWithBacklog
    | RunnerOnly
    | RunnerWithBacklog
    | RunnerWithLoop
    | AllAxes

type NudgeBlockStatus =
    | Blocked
    | Allowed

let workStateFromAxes (hasActiveRunner: bool) (isLoopActive: bool) (openTodos: string list) : SessionWorkState =
    let hasTodos = not openTodos.IsEmpty

    match hasActiveRunner, isLoopActive, hasTodos with
    | false, false, false -> SessionWorkState.Idle
    | false, false, true -> SessionWorkState.BacklogOnly
    | false, true, false -> SessionWorkState.LoopIdle
    | false, true, true -> SessionWorkState.LoopWithBacklog
    | true, false, false -> SessionWorkState.RunnerOnly
    | true, false, true -> SessionWorkState.RunnerWithBacklog
    | true, true, false -> SessionWorkState.RunnerWithLoop
    | true, true, true -> SessionWorkState.AllAxes

let hasActiveRunner (s: SessionWorkState) : bool =
    match s with
    | SessionWorkState.RunnerOnly
    | SessionWorkState.RunnerWithBacklog
    | SessionWorkState.RunnerWithLoop
    | SessionWorkState.AllAxes -> true
    | _ -> false

let isLoopActiveWorkState (s: SessionWorkState) : bool =
    match s with
    | SessionWorkState.LoopIdle
    | SessionWorkState.LoopWithBacklog
    | SessionWorkState.RunnerWithLoop
    | SessionWorkState.AllAxes -> true
    | _ -> false

let hasOpenTodos (s: SessionWorkState) : bool =
    match s with
    | SessionWorkState.BacklogOnly
    | SessionWorkState.LoopWithBacklog
    | SessionWorkState.RunnerWithBacklog
    | SessionWorkState.AllAxes -> true
    | _ -> false

let getSessionWorkState = workStateFromAxes

type ReviewLoopSnapshotInfo =
    { originalTask: string
      reviewLoopId: string
      currentRound: int
      latestVerdict: string option
      latestFeedback: string option }

[<RequireQualifiedAccess>]
type TerminalOrigin =
    | HumanTurnCompleted
    | HumanTurnAborted
    | FallbackContinuationCompleted
    | CompactionSummaryCompleted
    | CompactionContinuationCompleted
    | NudgeCompleted
    | TitleCompleted
    | ToolSubturnCompleted
    | Unknown

let isNaturalStop (origin: TerminalOrigin) : bool =
    match origin with
    | TerminalOrigin.HumanTurnCompleted -> true
    | _ -> false

type SessionSnapshot =
    { todos: string list
      lastAssistantMessage: string
      workState: SessionWorkState
      blockStatus: NudgeBlockStatus
      nudgeAnchorKey: string
      agentFromMessage: string option
      modelFromMessage: string option
      reviewLoop: ReviewLoopSnapshotInfo option
      humanTurnId: string option }

type SendOutcome =
    | Delivered
    | Aborted
    | Busy
    | Failed of error: string
    | TransportUnavailable of error: string
    | NotNeeded
    | SnapshotUnavailable of error: string
    | ClaimConflict
    | EventStoreFailure of error: string
