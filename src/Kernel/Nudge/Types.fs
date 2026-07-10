module Wanxiangshu.Kernel.Nudge.Types

type SessionWorkState =
    | Idle
    | RunnerActive of hasTodos: bool * loopActive: bool
    | LoopActive of hasTodos: bool
    | BacklogActive of openTodos: string list

type NudgeBlockStatus =
    | Blocked
    | Allowed

let getSessionWorkState (hasActiveRunner: bool) (isLoopActive: bool) (openTodos: string list) : SessionWorkState =
    if hasActiveRunner then
        RunnerActive(hasTodos = (not openTodos.IsEmpty), loopActive = isLoopActive)
    elif isLoopActive then
        LoopActive(hasTodos = (not openTodos.IsEmpty))
    elif not openTodos.IsEmpty then
        BacklogActive(openTodos = openTodos)
    else
        Idle

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
