module Wanxiangshu.Kernel.Nudge.Types

/// Nudge priority axes as a single DU (no nested bool tuples).
[<RequireQualifiedAccess>]
type SessionWorkState =
    | Idle
    | TodosOnly
    | LoopIdle
    | LoopWithTodos
    | RunnerOnly
    | RunnerWithTodos
    | RunnerWithLoop
    | AllAxes

type NudgeBlockStatus =
    | Blocked
    | Allowed

let workStateFromAxes (hasActiveRunner: bool) (isLoopActive: bool) (openTodos: string list) : SessionWorkState =
    let hasTodos = not openTodos.IsEmpty

    match hasActiveRunner, isLoopActive, hasTodos with
    | false, false, false -> SessionWorkState.Idle
    | false, false, true -> SessionWorkState.TodosOnly
    | false, true, false -> SessionWorkState.LoopIdle
    | false, true, true -> SessionWorkState.LoopWithTodos
    | true, false, false -> SessionWorkState.RunnerOnly
    | true, false, true -> SessionWorkState.RunnerWithTodos
    | true, true, false -> SessionWorkState.RunnerWithLoop
    | true, true, true -> SessionWorkState.AllAxes

let hasActiveRunner (s: SessionWorkState) : bool =
    match s with
    | SessionWorkState.RunnerOnly
    | SessionWorkState.RunnerWithTodos
    | SessionWorkState.RunnerWithLoop
    | SessionWorkState.AllAxes -> true
    | _ -> false

let isLoopActiveWorkState (s: SessionWorkState) : bool =
    match s with
    | SessionWorkState.LoopIdle
    | SessionWorkState.LoopWithTodos
    | SessionWorkState.RunnerWithLoop
    | SessionWorkState.AllAxes -> true
    | _ -> false

let hasOpenTodos (s: SessionWorkState) : bool =
    match s with
    | SessionWorkState.TodosOnly
    | SessionWorkState.LoopWithTodos
    | SessionWorkState.RunnerWithTodos
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
    | AcceptanceUnknown of error: string
