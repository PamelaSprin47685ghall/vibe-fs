module Wanxiangshu.Runtime.CommandProcessorBuild

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Reactive
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Decision
open Wanxiangshu.Kernel.ResourcePlan
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime.SubsessionPorts

/// Callbacks and services required to apply a Decision to mutable runtime state.
type DecisionApplier =
    { SessionId: SessionId
      EventStore: ISubsessionEventStore
      ReconcileResources: SubsessionState -> unit
      React: IReactivePort
      GetState: unit -> SubsessionState
      SetState: SubsessionState -> unit
      ApplyLocalEffect: Effect -> unit
      ResolveAll: RunResult -> unit }

let isPoisonedState (state: SubsessionState) : bool =
    match state with
    | Poisoned _ -> true
    | _ -> false

let getCurrentTurnId (state: SubsessionState) : TurnId option =
    match state with
    | Dispatching(_, plan, _, _) -> Some plan.TurnId
    | CancellingDispatch(_, plan, _, _) -> Some plan.TurnId
    | ReconcilingUnknownDispatch(_, plan, _, _, _, _) -> Some plan.TurnId
    | ClosingUnknownDispatch(_, plan, _, _, _) -> Some plan.TurnId
    | Running(_, started, _, _) -> Some started.Plan.TurnId
    | Draining(_, started, _, _, _) -> Some started.Plan.TurnId
    | IssuingAbort(_, turn, _, _, _) -> Some(activeTid turn)
    | AwaitingAbortSettle(_, turn, _, _) -> Some(activeTid turn)
    | ReconcilingAbortSettle(_, turn, _, _) -> Some(activeTid turn)
    | Available _
    | Poisoned _ -> None

let buildStartRunErrorResult (err: StartRunError) : RunResult =
    match err with
    | AlreadyRunning -> Failed(ProtocolViolation "subagent session already running")
    | StartRunError.SessionPoisoned reason -> Failed(InfrastructureFailure("session poisoned: " + string reason))
    | NoModelAvailable -> Failed NoModelConfigured

let handleApplyException
    (applier: DecisionApplier)
    (priorState: SubsessionState)
    (decision: Decision)
    (hasComplete: bool)
    (reject: StartRunError option)
    (ex: exn)
    : Effect list * StartRunError option =
    if hasComplete then
        let poison = EventStoreCorrupt("terminal append failed: " + ex.Message)
        applier.SetState(Poisoned poison)

        for e in decision.Effects do
            match e with
            | CompleteCaller(runId, _) ->
                applier.ApplyLocalEffect(
                    CompleteCaller(runId, Failed(InfrastructureFailure("event store append failed: " + ex.Message)))
                )
            | CancelPendingDispatch _
            | DisposeActor -> applier.ApplyLocalEffect e
            | _ -> ()

        [], reject
    else
        match buildFailSafe priorState ex.Message with
        | Some recovery ->
            applier.SetState(recovery.NextState)

            for e in recovery.Effects do
                applier.ApplyLocalEffect e

            filterHostEffects recovery.Effects, None
        | None ->
            applier.SetState(Poisoned(HostProtocolBroken("event append failed: " + ex.Message)))
            applier.ResolveAll(Failed(InfrastructureFailure("event append failed: " + ex.Message)))
            [], None

let applyDecision
    (applier: DecisionApplier)
    (priorState: SubsessionState)
    (decision: Decision)
    : JS.Promise<Effect list * StartRunError option> =
    promise {
        let reject = rejectOpt decision.Effects
        let hasComplete = hasCompleteCaller decision.Effects

        try
            // Step 4: Persist events
            if not (List.isEmpty decision.Events) then
                do! applier.EventStore.Append(applier.SessionId, decision.Events)

            // Step 5: Commit state
            applier.SetState(decision.NextState)

            // Step 6: Reconcile durable resources
            applier.ReconcileResources(applier.GetState())

            // Step 7: Local handlers
            for e in decision.Effects do
                applier.ApplyLocalEffect e

            // Step 8: Emit committed progress (fire-and-forget, never blocks)
            let progress = CommittedProgress.fromEvents decision.Events

            if not (List.isEmpty progress) then
                applier.React.OnCommitted progress

            // Return host effects for async dispatch
            return filterHostEffects decision.Effects, reject

        with ex ->
            return handleApplyException applier priorState decision hasComplete reject ex
    }

let handleIllegal
    (applier: DecisionApplier)
    (priorState: SubsessionState)
    (s: string)
    (c: string)
    : JS.Promise<Effect list * StartRunError option> =
    promise {
        match tryExtract priorState with
        | Some(subCtx, turn) ->
            let tid = activeTid turn

            let abortCtx =
                { Reason = IllegalTransitionFailSafe(s + " + " + c)
                  AfterStop = FinishFailed(InfrastructureFailure("illegal transition: " + s + " + " + c)) }

            let nowMs = int64 (JS.Constructors.Date.now ())
            let abortDeadlineAtMs = nowMs + 60_000L
            applier.SetState(IssuingAbort(subCtx, turn, abortCtx, false, abortDeadlineAtMs))
            applier.ReconcileResources(applier.GetState())
            let effects = [ AbortHostSession(subCtx.SessionId, tid); CancelPendingDispatch tid ]

            for e in effects do
                applier.ApplyLocalEffect e

            return filterHostEffects effects, None
        | None ->
            applier.SetState(Poisoned(HostProtocolBroken("illegal: " + s + " + " + c)))
            applier.ResolveAll(Failed(InfrastructureFailure("illegal transition: " + s + " + " + c)))
            return [], None
    }
