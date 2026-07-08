module Wanxiangshu.Kernel.ReviewSession

type ReviewState = Wanxiangshu.Kernel.ReviewSession.Types.ReviewState
type ReviewCommand = Wanxiangshu.Kernel.ReviewSession.Types.ReviewCommand
type ReviewEvent = Wanxiangshu.Kernel.ReviewSession.Types.ReviewEvent
type ReviewResult = Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult
type ReviewSession = Wanxiangshu.Kernel.ReviewSession.Types.ReviewSession
type RoundOutcome = Wanxiangshu.Kernel.ReviewSession.Types.RoundOutcome
type LoopDecision = Wanxiangshu.Kernel.ReviewSession.Types.LoopDecision
type RegistryAction = Wanxiangshu.Kernel.ReviewSession.Registry.RegistryAction
type Registry = Wanxiangshu.Kernel.ReviewSession.Registry.Registry
type SessionEffects = Wanxiangshu.Kernel.ReviewSession.Effects.SessionEffects

[<AutoOpen>]
module Types =
    type ReviewState = Wanxiangshu.Kernel.ReviewSession.Types.ReviewState
    type ReviewCommand = Wanxiangshu.Kernel.ReviewSession.Types.ReviewCommand
    type ReviewEvent = Wanxiangshu.Kernel.ReviewSession.Types.ReviewEvent
    type ReviewResult = Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult
    type ReviewSession = Wanxiangshu.Kernel.ReviewSession.Types.ReviewSession
    type RoundOutcome = Wanxiangshu.Kernel.ReviewSession.Types.RoundOutcome
    type LoopDecision = Wanxiangshu.Kernel.ReviewSession.Types.LoopDecision
    type RegistryAction = Wanxiangshu.Kernel.ReviewSession.Registry.RegistryAction
    type Registry = Wanxiangshu.Kernel.ReviewSession.Registry.Registry
    type SessionEffects = Wanxiangshu.Kernel.ReviewSession.Effects.SessionEffects

let empty = Wanxiangshu.Kernel.ReviewSession.Types.empty
let withTask = Wanxiangshu.Kernel.ReviewSession.Types.withTask
let withFeedback = Wanxiangshu.Kernel.ReviewSession.Types.withFeedback
let addChild = Wanxiangshu.Kernel.ReviewSession.Types.addChild
let transition = Wanxiangshu.Kernel.ReviewSession.StateMachine.transition
let isActive = Wanxiangshu.Kernel.ReviewSession.StateMachine.isActive
let initialState = Wanxiangshu.Kernel.ReviewSession.StateMachine.initialState
let applyCommand = Wanxiangshu.Kernel.ReviewSession.StateMachine.applyCommand

let decideAfterRound =
    Wanxiangshu.Kernel.ReviewSession.StateMachine.decideAfterRound

let promptParts = Wanxiangshu.Kernel.ReviewSession.StateMachine.promptParts
let emptyRegistry = Wanxiangshu.Kernel.ReviewSession.Registry.emptyRegistry
let reduce = Wanxiangshu.Kernel.ReviewSession.Registry.reduce
let actionFor = Wanxiangshu.Kernel.ReviewSession.Registry.actionFor

let hasActiveReviewState =
    Wanxiangshu.Kernel.ReviewSession.Query.hasActiveReviewState

let taskOf = Wanxiangshu.Kernel.ReviewSession.Query.taskOf
let stateOf = Wanxiangshu.Kernel.ReviewSession.Query.stateOf
let canTransition = Wanxiangshu.Kernel.ReviewSession.Query.canTransition
let versionOf = Wanxiangshu.Kernel.ReviewSession.Query.versionOf

let reduceIfVersionMatches =
    Wanxiangshu.Kernel.ReviewSession.Query.reduceIfVersionMatches

let emptyEffects = Wanxiangshu.Kernel.ReviewSession.Effects.emptyEffects
let setPending = Wanxiangshu.Kernel.ReviewSession.Effects.setPending
let resolvePending = Wanxiangshu.Kernel.ReviewSession.Effects.resolvePending
let disposeSessionTree = Wanxiangshu.Kernel.ReviewSession.Effects.disposeSessionTree
