module Wanxiangshu.Runtime.CommandProcessor

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Reactive
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Decision
open Wanxiangshu.Kernel.ResourcePlan
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.CommandProcessorBuild

let private createDeferred<'T> () : Deferred<'T> = SubsessionPorts.createDeferred<'T> ()

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

    let applier: DecisionApplier =
        { SessionId = sessionId
          EventStore = eventStore
          ReconcileResources = reconcileResources
          React = react
          GetState = fun () -> state
          SetState = fun s -> state <- s
          ApplyLocalEffect = applyLocalEffect
          ResolveAll = resolveAll }

    // ── Commit command ──

    let commitCommand (cmd: Command) : JS.Promise<Effect list * StartRunError option> =
        queue.Enqueue(fun () ->
            promise {
                if disposed then
                    return [], Some(StartRunError.SessionPoisoned SessionStateUnknownAfterRestart)
                else
                    let priorState = state

                    match decide state cmd with
                    | Ok(Decided decision) -> return! applyDecision applier priorState decision
                    | Ok(NoChange _) -> return [], None
                    | Error(IllegalTransition(s, c)) -> return! handleIllegal applier priorState s c
                    | Error(StaleTurnCommand _) -> return [], None
            })

    // ── Public API (all members after all let/do bindings) ──

    member _.AddCleanupCallback(cb: unit -> unit) =
        cleanupCallbacks <- cb :: cleanupCallbacks

    member _.SessionId = sessionId

    member _.GetState() : SubsessionState = state

    member _.IsDisposed: bool = disposed

    member _.IsPoisoned: bool = isPoisonedState state

    member _.GetCurrentTurn() : TurnId option = getCurrentTurnId state

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
                            | Ok(Decided decision) -> return! applyDecision applier priorState decision
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

                let result = buildStartRunErrorResult err

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
