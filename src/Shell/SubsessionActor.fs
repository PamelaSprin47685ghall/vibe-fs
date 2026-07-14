module Wanxiangshu.Shell.SubsessionActor

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Decision
open Wanxiangshu.Shell.PromiseQueue

// ── Deferred reply ──

type Deferred<'T> =
    { Resolve: 'T -> unit
      Reject: exn -> unit
      Promise: JS.Promise<'T> }

let createDeferred<'T> () : Deferred<'T> =
    let mutable resolveFn = fun (_: 'T) -> ()
    let mutable rejectFn = fun (_: exn) -> ()

    let p =
        Promise.create (fun resolve reject ->
            resolveFn <- resolve
            rejectFn <- reject)

    { Resolve = resolveFn
      Reject = rejectFn
      Promise = p }

// ── Ports ──

/// Host adapter: dispatch / abort / read transcript only.
type ISubsessionHost =
    abstract Dispatch:
        sessionId: SessionId * turn: TurnPlan -> JS.Promise<Result<HostStartReceipt, DispatchFailure>>

    abstract Abort: sessionId: SessionId * turnId: TurnId -> JS.Promise<AbortResult>

    /// Cancel an in-flight PendingTurnReceipt / dispatch waiter for this turn.
    abstract CancelPendingDispatch: turnId: TurnId -> unit

    abstract QueryDispatchStatus: SessionId * TurnId -> JS.Promise<DispatchStatus>

/// Required event store — append failure is infrastructure failure.
/// Implementations MUST treat the events list as one atomic write.
type ISubsessionEventStore =
    abstract Append: sessionId: SessionId * events: SubsessionEvent list -> JS.Promise<unit>

type TimerHandle = int

// ── Actor ──

/// Queue only commits decide/state. Host effects run detached outside the queue.
type SubsessionActor
    (sessionId: SessionId, host: ISubsessionHost, eventStore: ISubsessionEventStore, ?onDispose: unit -> unit, ?initialState: SubsessionState)
    as this
    =
    let queue = SerialQueue()
    let mutable state: SubsessionState = defaultArg initialState (Available { SessionId = sessionId })
    let mutable pendingReplies: Map<RunId, Deferred<RunResult>> = Map.empty
    let mutable turnTimers: Map<TurnId, TimerHandle> = Map.empty
    let mutable abortTimers: Map<TurnId, TimerHandle> = Map.empty
    let mutable disposed = false

    let clearTurnTimer (turnId: TurnId) =
        match Map.tryFind turnId turnTimers with
        | Some handle ->
            JS.clearTimeout handle
            turnTimers <- Map.remove turnId turnTimers
        | None -> ()

    let clearAbortTimer (turnId: TurnId) =
        match Map.tryFind turnId abortTimers with
        | Some handle ->
            JS.clearTimeout handle
            abortTimers <- Map.remove turnId abortTimers
        | None -> ()

    let clearAllTimers () =
        for KeyValue(_, h) in turnTimers do
            JS.clearTimeout h

        for KeyValue(_, h) in abortTimers do
            JS.clearTimeout h

        turnTimers <- Map.empty
        abortTimers <- Map.empty

    let armTurnTimer (turnId: TurnId) =
        clearTurnTimer turnId

        let handle =
            JS.setTimeout (fun () -> this.Post(TurnDeadlineExpired turnId) |> ignore) 300_000

        turnTimers <- Map.add turnId handle turnTimers

    let armAbortTimer (turnId: TurnId) =
        clearAbortTimer turnId

        let handle =
            JS.setTimeout (fun () -> this.Post(AbortDeadlineExpired turnId) |> ignore) 60_000

        abortTimers <- Map.add turnId handle abortTimers

    let infrastructureError (ex: exn) : ErrorInput =
        { ErrorName = "InfrastructureFailure"
          DomainError = None
          Message = ex.Message
          StatusCode = None
          IsRetryable = Some false }

    let resolveAll (result: RunResult) =
        for KeyValue(_, reply) in pendingReplies do
            reply.Resolve result

        pendingReplies <- Map.empty

    let tryExtractActive (s: SubsessionState) : (RunContext * ActiveTurn) option =
        match s with
        | Dispatching(ctx, plan) -> Some(ctx, NotYetStarted plan)
        | CancellingDispatch(ctx, plan, _) -> Some(ctx, NotYetStarted plan)
        | ReconcilingUnknownDispatch(ctx, plan, _) -> Some(ctx, NotYetStarted plan)
        | Running(ctx, started, _) -> Some(ctx, Started started)
        | Draining(ctx, started, _) -> Some(ctx, Started started)
        | IssuingAbort(ctx, turn, _) -> Some(ctx, turn)
        | AwaitingAbortSettle(ctx, turn, _) -> Some(ctx, turn)
        | ReconcilingAbortSettle(ctx, turn, _) -> Some(ctx, turn)
        | Available _
        | Poisoned _ -> None

    /// Local / synchronous effects only — never awaits host or Post.
    let applyLocalEffect (effect: Effect) : unit =
        match effect with
        | ArmTurnDeadline turnId -> armTurnTimer turnId
        | CancelTurnDeadline turnId -> clearTurnTimer turnId
        | ArmAbortDeadline turnId -> armAbortTimer turnId
        | CancelAbortDeadline turnId -> clearAbortTimer turnId
        | CancelPendingDispatch turnId -> host.CancelPendingDispatch turnId
        | CompleteCaller(runId, result) ->
            match Map.tryFind runId pendingReplies with
            | Some reply ->
                pendingReplies <- Map.remove runId pendingReplies
                reply.Resolve result
            | None -> ()
        | RejectStart err -> ignore err
        | DisposeActor ->
            disposed <- true
            clearAllTimers ()
            onDispose |> Option.iter (fun f -> f ())
        | DispatchPrompt _
        | QueryDispatchStatus _
        | AbortHostSession _ -> ()

    /// Host effects: fire-and-forget; completion re-enters via Post (never awaited here).
    let launchHostEffect (effect: Effect) : unit =
        match effect with
        | DispatchPrompt plan ->
            host.Dispatch(sessionId, plan)
            |> Promise.map (function
                | Ok receipt -> this.Post(DispatchAccepted(plan.TurnId, receipt)) |> ignore
                | Error failure -> this.Post(DispatchRejected(plan.TurnId, failure)) |> ignore)
            |> Promise.catch (fun ex ->
                this.Post(DispatchRejected(plan.TurnId, DispatchFailure.HostAcceptanceUnknown(infrastructureError ex)))
                |> ignore)
            |> ignore

        | QueryDispatchStatus(sid, tid) ->
            host.QueryDispatchStatus(sid, tid)
            |> Promise.map (fun status -> this.Post(DispatchStatusResolved status) |> ignore)
            |> Promise.catch (fun _ -> this.Post(DispatchStatusResolved Unknown) |> ignore)
            |> ignore

        | AbortHostSession(sid, tid) ->
            host.Abort(sid, tid)
            |> Promise.map (function
                | ConfirmedStopped -> this.Post(AbortConfirmed tid) |> ignore
                | RequestAcceptedAwaitIdle -> this.Post(AbortHostAccepted tid) |> ignore
                | AbortUnavailable ->
                    this.Post(
                        AbortRequestFailed(
                            tid,
                            { ErrorName = "AbortUnavailable"
                              DomainError = None
                              Message = "host abort API unavailable"
                              StatusCode = None
                              IsRetryable = Some false }
                        )
                    )
                    |> ignore)
            |> Promise.catch (fun ex ->
                this.Post(AbortRequestFailed(tid, infrastructureError ex)) |> ignore)
            |> ignore

        | _ -> ()

    let isHostEffect =
        function
        | DispatchPrompt _
        | QueryDispatchStatus _
        | AbortHostSession _ -> true
        | _ -> false

    let isLocalEffect e = not (isHostEffect e)

    /// Build a fail-safe abort decision when event append fails while host may be running.
    let failSafeFromAppend (priorState: SubsessionState) (ex: exn) : Decision option =
        match tryExtractActive priorState with
        | None -> None
        | Some(ctx, turn) ->
            let tid =
                match turn with
                | NotYetStarted p -> p.TurnId
                | Started s -> s.Plan.TurnId

            let reason = EventStoreFailure ex.Message

            let abortCtx =
                { Reason = reason
                  AfterStop = FinishFailed(InfrastructureFailure("event store append failed: " + ex.Message)) }

            // Runtime-only recovery; do not append more domain events that would fail again.
            Some
                { NextState = IssuingAbort(ctx, turn, abortCtx)
                  Events = []
                  Effects =
                    [ AbortHostSession(ctx.SessionId, tid)
                      CancelTurnDeadline tid
                      CancelPendingDispatch tid
                      ArmAbortDeadline tid ] }

    /// Apply a decision that is already computed (used by BeginRun atomic path).
    let applyDecision (priorState: SubsessionState) (decision: Decision) : JS.Promise<Effect list * StartRunError option> =
        promise {
            let rejectOpt =
                decision.Effects
                |> List.tryPick (function
                    | RejectStart err -> Some err
                    | _ -> None)

            let hasCompleteCaller =
                decision.Effects
                |> List.exists (function
                    | CompleteCaller _ -> true
                    | _ -> false)

            try
                if not (List.isEmpty decision.Events) then
                    do! eventStore.Append(sessionId, decision.Events)

                state <- decision.NextState

                for e in decision.Effects do
                    if isLocalEffect e then
                        applyLocalEffect e

                let hostEffects = decision.Effects |> List.filter isHostEffect
                return hostEffects, rejectOpt
            with ex ->
                // Terminal append failure: never return original Succeeded/Cancelled.
                // Domain fact was not persisted → InfrastructureFailure + poison.
                if hasCompleteCaller then
                    let poisonReason = EventStoreCorrupt("terminal append failed: " + ex.Message)
                    state <- Poisoned poisonReason

                    // Rewrite CompleteCaller to InfrastructureFailure; still resolve parent.
                    for e in decision.Effects do
                        match e with
                        | CompleteCaller(runId, _) ->
                            applyLocalEffect (
                                CompleteCaller(
                                    runId,
                                    Failed(InfrastructureFailure("event store append failed: " + ex.Message))
                                )
                            )
                        | CancelTurnDeadline _
                        | CancelAbortDeadline _
                        | CancelPendingDispatch _
                        | DisposeActor -> applyLocalEffect e
                        | _ -> ()

                    return [], rejectOpt
                else
                    match failSafeFromAppend priorState ex with
                    | Some recovery ->
                        state <- recovery.NextState

                        for e in recovery.Effects do
                            if isLocalEffect e then
                                applyLocalEffect e

                        return recovery.Effects |> List.filter isHostEffect, None
                    | None ->
                        state <- Poisoned(HostProtocolBroken("event append failed: " + ex.Message))
                        resolveAll (Failed(InfrastructureFailure("event append failed: " + ex.Message)))
                        return [], None
        }

    /// Queue-bound: decide + append events + commit state + return effects.
    /// NEVER awaits host dispatch / Post.
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

                    | Error(IllegalTransition(s, c)) ->
                        match tryExtractActive priorState with
                        | Some(ctx, turn) ->
                            let tid =
                                match turn with
                                | NotYetStarted p -> p.TurnId
                                | Started s -> s.Plan.TurnId

                            let abortCtx =
                                { Reason = IllegalTransitionFailSafe(s + " + " + c)
                                  AfterStop =
                                    FinishFailed(InfrastructureFailure("illegal transition: " + s + " + " + c)) }

                            state <- IssuingAbort(ctx, turn, abortCtx)

                            let effects =
                                [ AbortHostSession(ctx.SessionId, tid)
                                  CancelTurnDeadline tid
                                  CancelPendingDispatch tid
                                  ArmAbortDeadline tid ]

                            for e in effects do
                                if isLocalEffect e then
                                    applyLocalEffect e

                            return effects |> List.filter isHostEffect, None
                        | None ->
                            state <- Poisoned(HostProtocolBroken("illegal: " + s + " + " + c))
                            resolveAll (Failed(InfrastructureFailure("illegal transition: " + s + " + " + c)))
                            return [], None

                    | Error(StaleTurnCommand _) -> return [], None
            })

    member _.SessionId = sessionId

    member _.GetState() = state

    member _.IsPoisoned =
        match state with
        | Poisoned _ -> true
        | _ -> false

    member _.IsDisposed = disposed

    /// Startup reconcile: unfinished NDJSON run → poison so StartRun is rejected
    /// until physical session is disposed.
    member _.MarkUnknownAfterRestart() : JS.Promise<unit> =
        queue.Enqueue(fun () ->
            promise {
                match state with
                | Available _ ->
                    state <- Poisoned SessionStateUnknownAfterRestart
                | Poisoned _ -> ()
                | _ ->
                    // Active host turn after restart is unsafe; poison and resolve callers.
                    state <- Poisoned SessionStateUnknownAfterRestart
                    resolveAll (Failed(InfrastructureFailure "session state unknown after restart"))
                    clearAllTimers ()

                return ()
            })

    /// Post a fact. Commits on the queue, then launches host effects detached.
    member _.Post(cmd: Command) : JS.Promise<unit> =
        promise {
            let! hostEffects, _ = commitCommand cmd

            for e in hostEffects do
                launchHostEffect e
        }

    /// Atomic BeginRun: register Deferred + decide StartRun + append + commit
    /// in ONE queue item so CancelRequested cannot insert between them.
    member this.BeginRun(request: StartRunRequest) : JS.Promise<RunResult> =
        promise {
            let deferred = createDeferred<RunResult> ()

            let! hostEffects, rejectOpt =
                queue.Enqueue(fun () ->
                    promise {
                        if disposed then
                            return [], Some(StartRunError.SessionPoisoned SessionStateUnknownAfterRestart)
                        else
                            // Register reply inside the same queue item as StartRun.
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
                                        StartRunError.SessionPoisoned(
                                            HostProtocolBroken("illegal: " + s + " + " + c)
                                        )
                                    )
                            | Error(StaleTurnCommand _) ->
                                pendingReplies <- Map.remove request.RunId pendingReplies
                                return [], None
                    })

            match rejectOpt with
            | Some err ->
                pendingReplies <- Map.remove request.RunId pendingReplies

                let result =
                    match err with
                    | AlreadyRunning -> Failed(ProtocolViolation "subagent session already running")
                    | StartRunError.SessionPoisoned reason ->
                        Failed(InfrastructureFailure("session poisoned: " + string reason))
                    | NoModelAvailable -> Failed NoModelConfigured

                deferred.Resolve result
            | None ->
                for e in hostEffects do
                    launchHostEffect e

            return! deferred.Promise
        }

    /// Back-compat alias for BeginRun (atomic).
    member this.StartRun(request: StartRunRequest) : JS.Promise<RunResult> = this.BeginRun request
