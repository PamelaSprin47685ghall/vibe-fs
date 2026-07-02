module Wanxiangshu.Kernel.NudgeState

[<AutoOpen>]
module Types =
    type NudgeShellState = Wanxiangshu.Kernel.Nudge.Types.NudgeShellState
    type SessionSnapshot = Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot
    type NudgeDecision = Wanxiangshu.Kernel.Nudge.Types.NudgeDecision
    type SendOutcome = Wanxiangshu.Kernel.Nudge.Types.SendOutcome
    type NudgeHostEvent = Wanxiangshu.Kernel.Nudge.Types.NudgeHostEvent

type NudgeShellState = Wanxiangshu.Kernel.Nudge.Types.NudgeShellState
type SessionSnapshot = Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot
type NudgeDecision = Wanxiangshu.Kernel.Nudge.Types.NudgeDecision
type SendOutcome = Wanxiangshu.Kernel.Nudge.Types.SendOutcome
type NudgeHostEvent = Wanxiangshu.Kernel.Nudge.Types.NudgeHostEvent

let emptyState = Wanxiangshu.Kernel.Nudge.Types.emptyState
