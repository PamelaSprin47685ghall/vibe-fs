module Wanxiangshu.Kernel.NudgeState

type FreshAssistantSnapshot = Wanxiangshu.Kernel.Nudge.Types.FreshAssistantSnapshot
type NudgeShellState = Wanxiangshu.Kernel.Nudge.Types.NudgeShellState
type SessionSnapshot = Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot
type NudgeDecision = Wanxiangshu.Kernel.Nudge.Types.NudgeDecision
type SendOutcome = Wanxiangshu.Kernel.Nudge.Types.SendOutcome
type MessageOutcome = Wanxiangshu.Kernel.Nudge.Types.MessageOutcome
type PartOutcome = Wanxiangshu.Kernel.Nudge.Types.PartOutcome
type StepFailOutcome = Wanxiangshu.Kernel.Nudge.Types.StepFailOutcome
type ToolFailOutcome = Wanxiangshu.Kernel.Nudge.Types.ToolFailOutcome
type SessionErrorOutcome = Wanxiangshu.Kernel.Nudge.Types.SessionErrorOutcome
type NudgeHostEvent = Wanxiangshu.Kernel.Nudge.Types.NudgeHostEvent

[<AutoOpen>]
module Types =
    type FreshAssistantSnapshot = Wanxiangshu.Kernel.Nudge.Types.FreshAssistantSnapshot
    type NudgeShellState = Wanxiangshu.Kernel.Nudge.Types.NudgeShellState
    type SessionSnapshot = Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot
    type NudgeDecision = Wanxiangshu.Kernel.Nudge.Types.NudgeDecision
    type SendOutcome = Wanxiangshu.Kernel.Nudge.Types.SendOutcome
    type MessageOutcome = Wanxiangshu.Kernel.Nudge.Types.MessageOutcome
    type PartOutcome = Wanxiangshu.Kernel.Nudge.Types.PartOutcome
    type StepFailOutcome = Wanxiangshu.Kernel.Nudge.Types.StepFailOutcome
    type ToolFailOutcome = Wanxiangshu.Kernel.Nudge.Types.ToolFailOutcome
    type SessionErrorOutcome = Wanxiangshu.Kernel.Nudge.Types.SessionErrorOutcome
    type NudgeHostEvent = Wanxiangshu.Kernel.Nudge.Types.NudgeHostEvent

let emptyState = Wanxiangshu.Kernel.Nudge.Types.emptyState
let resumeSession = Wanxiangshu.Kernel.Nudge.Transitions.resumeSession
let rememberAgent = Wanxiangshu.Kernel.Nudge.Transitions.rememberAgent
let storeFreshAssistantSnapshot = Wanxiangshu.Kernel.Nudge.Transitions.storeFreshAssistantSnapshot
let takeFreshAssistantSnapshot = Wanxiangshu.Kernel.Nudge.Transitions.takeFreshAssistantSnapshot
let stopSession = Wanxiangshu.Kernel.Nudge.Transitions.stopSession
let clearSession = Wanxiangshu.Kernel.Nudge.Transitions.clearSession
let addRetryPendingSession = Wanxiangshu.Kernel.Nudge.Transitions.addRetryPendingSession
let deleteRetryPendingSession = Wanxiangshu.Kernel.Nudge.Transitions.deleteRetryPendingSession
let deleteNudgedSession = Wanxiangshu.Kernel.Nudge.Transitions.deleteNudgedSession
let tryClaimNudge = Wanxiangshu.Kernel.Nudge.Transitions.tryClaimNudge
let recordSend = Wanxiangshu.Kernel.Nudge.Transitions.recordSend
let tryRecordSend = Wanxiangshu.Kernel.Nudge.Transitions.tryRecordSend
let handleSessionBusy = Wanxiangshu.Kernel.Nudge.Transitions.handleSessionBusy
let decideNudge = Wanxiangshu.Kernel.Nudge.Decision.decideNudge
let handleSessionNextStepEnded = Wanxiangshu.Kernel.Nudge.EventHandler.handleSessionNextStepEnded
let handleEvent = Wanxiangshu.Kernel.Nudge.EventHandler.handleEvent