module Wanxiangshu.Kernel.Nudge.Types

type SessionSnapshot =
    { todos: string list
      lastAssistantMessage: string
      isLoopActive: bool
      nudgeBlockedForTurn: bool
      nudgeAnchorKey: string
      agentFromMessage: string option
      hasActiveRunner: bool }

type NudgeDecision =
    | StandDown
    | Send of promptText: string * agentOpt: string option

type SendOutcome =
    | Delivered
    | Aborted
    | Busy
    | Failed
