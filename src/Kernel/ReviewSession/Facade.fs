module VibeFs.Kernel.ReviewSession

type ReviewState = VibeFs.Kernel.ReviewSession.Types.ReviewState
type ReviewCommand = VibeFs.Kernel.ReviewSession.Types.ReviewCommand
type ReviewEvent = VibeFs.Kernel.ReviewSession.Types.ReviewEvent
type ReviewResult = VibeFs.Kernel.ReviewSession.Types.ReviewResult
type ReviewSession = VibeFs.Kernel.ReviewSession.Types.ReviewSession
type RoundOutcome = VibeFs.Kernel.ReviewSession.Types.RoundOutcome
type LoopDecision = VibeFs.Kernel.ReviewSession.Types.LoopDecision
type RegistryAction = VibeFs.Kernel.ReviewSession.Registry.RegistryAction
type Registry = VibeFs.Kernel.ReviewSession.Registry.Registry
type SessionEffects = VibeFs.Kernel.ReviewSession.Effects.SessionEffects

[<AutoOpen>]
module Types =
    type ReviewState = VibeFs.Kernel.ReviewSession.Types.ReviewState
    type ReviewCommand = VibeFs.Kernel.ReviewSession.Types.ReviewCommand
    type ReviewEvent = VibeFs.Kernel.ReviewSession.Types.ReviewEvent
    type ReviewResult = VibeFs.Kernel.ReviewSession.Types.ReviewResult
    type ReviewSession = VibeFs.Kernel.ReviewSession.Types.ReviewSession
    type RoundOutcome = VibeFs.Kernel.ReviewSession.Types.RoundOutcome
    type LoopDecision = VibeFs.Kernel.ReviewSession.Types.LoopDecision
    type RegistryAction = VibeFs.Kernel.ReviewSession.Registry.RegistryAction
    type Registry = VibeFs.Kernel.ReviewSession.Registry.Registry
    type SessionEffects = VibeFs.Kernel.ReviewSession.Effects.SessionEffects

let empty = VibeFs.Kernel.ReviewSession.Types.empty
let withTask = VibeFs.Kernel.ReviewSession.Types.withTask
let withFeedback = VibeFs.Kernel.ReviewSession.Types.withFeedback
let addChild = VibeFs.Kernel.ReviewSession.Types.addChild
let transition = VibeFs.Kernel.ReviewSession.StateMachine.transition
let isActive = VibeFs.Kernel.ReviewSession.StateMachine.isActive
let initialState = VibeFs.Kernel.ReviewSession.StateMachine.initialState
let applyCommand = VibeFs.Kernel.ReviewSession.StateMachine.applyCommand
let decideAfterRound = VibeFs.Kernel.ReviewSession.StateMachine.decideAfterRound
let promptParts = VibeFs.Kernel.ReviewSession.StateMachine.promptParts
let emptyRegistry = VibeFs.Kernel.ReviewSession.Registry.emptyRegistry
let reduce = VibeFs.Kernel.ReviewSession.Registry.reduce
let actionFor = VibeFs.Kernel.ReviewSession.Registry.actionFor
let sessionIsActive = VibeFs.Kernel.ReviewSession.Query.sessionIsActive
let taskOf = VibeFs.Kernel.ReviewSession.Query.taskOf
let stateOf = VibeFs.Kernel.ReviewSession.Query.stateOf
let canTransition = VibeFs.Kernel.ReviewSession.Query.canTransition
let versionOf = VibeFs.Kernel.ReviewSession.Query.versionOf
let reduceIfVersionMatches = VibeFs.Kernel.ReviewSession.Query.reduceIfVersionMatches
let emptyEffects = VibeFs.Kernel.ReviewSession.Effects.emptyEffects
let setPending = VibeFs.Kernel.ReviewSession.Effects.setPending
let resolvePending = VibeFs.Kernel.ReviewSession.Effects.resolvePending
let disposeSessionTree = VibeFs.Kernel.ReviewSession.Effects.disposeSessionTree