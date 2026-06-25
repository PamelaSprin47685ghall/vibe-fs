module VibeFs.Kernel.NudgeState

type FreshAssistantSnapshot = VibeFs.Kernel.Nudge.Types.FreshAssistantSnapshot
type NudgeShellState = VibeFs.Kernel.Nudge.Types.NudgeShellState
type SessionSnapshot = VibeFs.Kernel.Nudge.Types.SessionSnapshot
type NudgeDecision = VibeFs.Kernel.Nudge.Types.NudgeDecision
type SendOutcome = VibeFs.Kernel.Nudge.Types.SendOutcome
type MessageOutcome = VibeFs.Kernel.Nudge.Types.MessageOutcome
type PartOutcome = VibeFs.Kernel.Nudge.Types.PartOutcome
type StepFailOutcome = VibeFs.Kernel.Nudge.Types.StepFailOutcome
type ToolFailOutcome = VibeFs.Kernel.Nudge.Types.ToolFailOutcome
type SessionErrorOutcome = VibeFs.Kernel.Nudge.Types.SessionErrorOutcome
type NudgeHostEvent = VibeFs.Kernel.Nudge.Types.NudgeHostEvent

[<AutoOpen>]
module Types =
    type FreshAssistantSnapshot = VibeFs.Kernel.Nudge.Types.FreshAssistantSnapshot
    type NudgeShellState = VibeFs.Kernel.Nudge.Types.NudgeShellState
    type SessionSnapshot = VibeFs.Kernel.Nudge.Types.SessionSnapshot
    type NudgeDecision = VibeFs.Kernel.Nudge.Types.NudgeDecision
    type SendOutcome = VibeFs.Kernel.Nudge.Types.SendOutcome
    type MessageOutcome = VibeFs.Kernel.Nudge.Types.MessageOutcome
    type PartOutcome = VibeFs.Kernel.Nudge.Types.PartOutcome
    type StepFailOutcome = VibeFs.Kernel.Nudge.Types.StepFailOutcome
    type ToolFailOutcome = VibeFs.Kernel.Nudge.Types.ToolFailOutcome
    type SessionErrorOutcome = VibeFs.Kernel.Nudge.Types.SessionErrorOutcome
    type NudgeHostEvent = VibeFs.Kernel.Nudge.Types.NudgeHostEvent

let emptyState = VibeFs.Kernel.Nudge.Types.emptyState
let resumeSession = VibeFs.Kernel.Nudge.Transitions.resumeSession
let rememberAgent = VibeFs.Kernel.Nudge.Transitions.rememberAgent
let storeFreshAssistantSnapshot = VibeFs.Kernel.Nudge.Transitions.storeFreshAssistantSnapshot
let takeFreshAssistantSnapshot = VibeFs.Kernel.Nudge.Transitions.takeFreshAssistantSnapshot
let stopSession = VibeFs.Kernel.Nudge.Transitions.stopSession
let clearSession = VibeFs.Kernel.Nudge.Transitions.clearSession
let addRetryPendingSession = VibeFs.Kernel.Nudge.Transitions.addRetryPendingSession
let deleteRetryPendingSession = VibeFs.Kernel.Nudge.Transitions.deleteRetryPendingSession
let deleteNudgedSession = VibeFs.Kernel.Nudge.Transitions.deleteNudgedSession
let tryClaimNudge = VibeFs.Kernel.Nudge.Transitions.tryClaimNudge
let recordSend = VibeFs.Kernel.Nudge.Transitions.recordSend
let tryRecordSend = VibeFs.Kernel.Nudge.Transitions.tryRecordSend
let handleSessionBusy = VibeFs.Kernel.Nudge.Transitions.handleSessionBusy
let decideNudge = VibeFs.Kernel.Nudge.Decision.decideNudge
let handleSessionNextStepEnded = VibeFs.Kernel.Nudge.EventHandler.handleSessionNextStepEnded
let handleEvent = VibeFs.Kernel.Nudge.EventHandler.handleEvent