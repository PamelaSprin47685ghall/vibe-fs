module Wanxiangshu.Runtime.CommandProcessor

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Reactive
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Decision
open Wanxiangshu.Kernel.ResourcePlan
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime.SubsessionPorts
let private createDeferred<'T> () : Deferred<'T> =
    SubsessionPorts.createDeferred<'T>()

// ── CommandProcessor ──

/// The serial commit pipeline for a single subsession.
type CommandProcessor
    (
        sessionId: SessionId,
        host: ISubsessionHost,
        eventStore: ISubsessionEventStore,
        reconcileResources: SubsessionState -> unit,
        ?reactivePort: IReactivePort,
        ?initialState: SubsessionState
    ) =
    let queue = SerialQueue()
    let mutable pendingReplies: Map<RunId, Deferred<RunResult>> = Map.empty
    let mutable disposed = false

    let mutable state: SubsessionState =
        defaultArg initialState (Available { SessionId = sessionId })

    // Reactive port (fire-and-forget progress/telemetry)
    let react = defaultArg reactivePort (NullReactivePort() :> IReactivePort)

    // Cleanup callbacks for DisposeActor (registered by SubsessionActor for ResourceScope cleanup)
    let mutable cleanupCallbacks: (unit -> unit) list = []

    // ── Helpers (all let bindings before all members, per F# class ordering rule) ──

    let resolveAll (result: RunResult) =
        for KeyValue(_, reply) in pendingReplies do
            reply.Resolve result

        pendingReplies <- Map.empty

    // ── Local effects ──

    let applyLocalEffect (effect: Effect) : unit =
        match effect with
        | CancelPendingDispatch turnId -> host.CancelPendingDispatch turnId
        | CompleteCaller(runId, result) ->
            match Map.tryFind runId pendingReplies with
            | Some reply ->
                pendingReplies <- Map.remove runId pendingReplies
                reply.Resolve result
            | None -> ()
        | RejectStart _ -> ()
        | DisposeActor ->
            disposed <- true
            resolveAll Cancelled

            for cb in cleanupCallbacks do
                cb ()
        | _ -> ()

    // ── Apply decision (steps 4-7) ──

    let handleApplyException (priorState: SubsessionState) (decision: Decision) (hasComplete: bool) (reject: StartRunError option) (ex: exn) =
        if hasComplete then
            let poison = EventStoreCorrupt("terminal append failed: " + ex.Message)
            state <- Poisoned poison
            for e in decision.Effects do
                match e with
                | CompleteCaller(runId, _) ->
                    applyLocalEffect (
                        CompleteCaller(
                            runId,
                            Failed(InfrastructureFailure("event store append failed: " + ex.Message))
                        )
                    )
                | CancelPendingDispatch _
                | DisposeActor -> applyLocalEffect e
                | _ -> ()
            [], reject
        else
            match buildFailSafe priorState ex.Message with
            | Some recovery ->
                state <- recovery.NextState
                for e in recovery.Effects do
                    applyLocalEffect e
                filterHostEffects recovery.Effects, None
            | None ->
                state <- Poisoned(HostProtocolBroken("event append failed: " + ex.Message))
                resolveAll (Failed(InfrastructureFailure("event append failed: " + ex.Message)))
                [], None

    let applyDecision
        (priorState: SubsessionState)
        (decision: Decision)
        : JS.Promise<Effect list * StartRunError option> =
        promise {
            let reject = rejectOpt decision.Effects
            let hasComplete = hasCompleteCaller decision.Effects

            try
                // Step 4: Persist events
                if not (List.isEmpty decision.Events) then
                    do! eventStore.Append(sessionId, decision.Events)

                // Step 5: Commit state
                state <- decision.NextState

                // Step 6: Reconcile durable resources
                reconcileResources state

                // Step 7: Local handlers
                for e in decision.Effects do
                    applyLocalEffect e

                // Step 8: Emit committed progress (fire-and-forget, never blocks)
                let progress = CommittedProgress.fromEvents decision.Events

                if not (List.isEmpty progress) then
                    react.OnCommitted progress

                // Return host effects for async dispatch
                return filterHostEffects decision.Effects, reject

            with ex ->
                return handleApplyException priorState decision hasComplete reject ex
        }

    // ── Handle illegal transition ──

    let handleIllegal
        (priorState: SubsessionState)
        (s: string)
        (c: string)
        : JS.Promise<Effect list * StartRunError option> =
        promise {
            match tryExtract priorState with
            | Some(ctx, turn) ->
                let tid = activeTid turn

                let abortCtx =
                    { Reason = IllegalTransitionFailSafe(s + " + " + c)
                      AfterStop = FinishFailed(InfrastructureFailure("illegal transition: " + s + " + " + c)) }

                state <- IssuingAbort(ctx, turn, abortCtx, false)
                reconcileResources state
                let effects = [ AbortHostSession(ctx.SessionId, tid); CancelPendingDispatch tid ]

                for e in effects do
                    applyLocalEffect e

                return filterHostEffects effects, None
            | None ->
                state <- Poisoned(HostProtocolBroken("illegal: " + s + " + " + c))
                resolveAll (Failed(InfrastructureFailure("illegal transition: " + s + " + " + c)))
                return [], None
        }

    // ── Commit command ──

    let commitCommand (cmd: Command) : JS.Promise<Effect list * StartRunError option> =
        queue.Enqueue(fun () ->
            promise {
                if disposed then
                    return [], Some(StartRunError.SessionPoisoned SessionStateUnknownAfterRestart)
                else
                    let priorState = state

                    match decide state cmd with
                    | Ok(Decided decision) -> return! applyDecision priorState decision
                    | Ok(NoChange _) -> return [], None
                    | Error(IllegalTransition(s, c)) -> return! handleIllegal priorState s c
                    | Error(StaleTurnCommand _) -> return [], None
            })

    // ── Public API (all members after all let/do bindings) ──

    member _.AddCleanupCallback(cb: unit -> unit) =
        cleanupCallbacks <- cb :: cleanupCallbacks

    member _.SessionId = sessionId

    member _.GetState() : SubsessionState = state

    member _.IsDisposed: bool = disposed

    member _.IsPoisoned: bool =
        match state with
        | Poisoned _ -> true
        | _ -> false

    member _.GetCurrentTurn() : TurnId option =
        match state with
        | Dispatching(_, plan, _) -> Some plan.TurnId
        | CancellingDispatch(_, plan, _) -> Some plan.TurnId
        | ReconcilingUnknownDispatch(_, plan, _, _) -> Some plan.TurnId
        | ClosingUnknownDispatch(_, plan, _) -> Some plan.TurnId
        | Running(_, started, _) -> Some started.Plan.TurnId
        | Draining(_, started, _, _) -> Some started.Plan.TurnId
        | IssuingAbort(_, turn, _, _) -> Some(activeTid turn)
        | AwaitingAbortSettle(_, turn, _) -> Some(activeTid turn)
        | ReconcilingAbortSettle(_, turn, _) -> Some(activeTid turn)
        | Available _
        | Poisoned _ -> None

    member _.Post(cmd: Command) : JS.Promise<Effect list * StartRunError option> = commitCommand cmd

    /// Commit StartRun and return the caller completion promise separately from
    /// the host effects that must make that promise settle.
    member this.BeginRun(request: StartRunRequest) : JS.Promise<JS.Promise<RunResult> * Effect list> =
        promise {
            let deferred = createDeferred<RunResult> ()

            let! hostEffects, rejectErr =
                queue.Enqueue(fun () ->
                    promise {
                        if disposed then
                            return [], Some(StartRunError.SessionPoisoned SessionStateUnknownAfterRestart)
                        else
                            pendingReplies <- Map.add request.RunId deferred pendingReplies
                            let priorState = state

                            match decide state (StartRun request) with
                            | Ok(Decided decision) -> return! applyDecision priorState decision
                            | Ok(NoChange _) ->
                                pendingReplies <- Map.remove request.RunId pendingReplies
                                return [], None
                            | Error(IllegalTransition(s, c)) ->
                                pendingReplies <- Map.remove request.RunId pendingReplies
                                state <- Poisoned(HostProtocolBroken("illegal: " + s + " + " + c))

                                return
                                    [],
                                    Some(
                                        StartRunError.SessionPoisoned(HostProtocolBroken("illegal: " + s + " + " + c))
                                    )
                            | Error(StaleTurnCommand _) ->
                                pendingReplies <- Map.remove request.RunId pendingReplies
                                return [], None
                    })

            match rejectErr with
            | Some err ->
                pendingReplies <- Map.remove request.RunId pendingReplies

                let result =
                    match err with
                    | AlreadyRunning -> Failed(ProtocolViolation "subagent session already running")
                    | StartRunError.SessionPoisoned reason ->
                        Failed(InfrastructureFailure("session poisoned: " + string reason))
                    | NoModelAvailable -> Failed NoModelConfigured

                deferred.Resolve result
                return deferred.Promise, []
            | None -> return deferred.Promise, hostEffects
        }

    member _.MarkUnknownAfterRestart() : JS.Promise<unit> =
        queue.Enqueue(fun () ->
            promise {
                match state with
                | Available _ -> state <- Poisoned SessionStateUnknownAfterRestart
                | Poisoned _ -> ()
                | _ ->
                    state <- Poisoned SessionStateUnknownAfterRestart
                    resolveAll (Failed(InfrastructureFailure "session state unknown after restart"))

                return ()
            })
