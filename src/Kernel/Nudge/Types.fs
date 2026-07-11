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

/// Back-compat name for call sites migrating off bool triples.
let getSessionWorkState = workStateFromAxes

type SessionSnapshot =
    { todos: string list
      lastAssistantMessage: string
      workState: SessionWorkState
      blockStatus: NudgeBlockStatus
      nudgeAnchorKey: string
      agentFromMessage: string option
      modelFromMessage: string option }

type SendOutcome =
    | Delivered
    | Aborted
    | Busy
    | Failed
