module Wanxiangshu.Kernel.Nudge.Types

type SessionSnapshot =
    { todos: string list
      lastAssistantMessage: string
      isLoopActive: bool
      nudgeBlockedForTurn: bool
      nudgeAnchorKey: string
      agentFromMessage: string option
      hasActiveRunner: bool }

type SendOutcome =
    | Delivered
    | Aborted
    | Busy
    | Failed
