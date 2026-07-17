module Wanxiangshu.Runtime.SubsessionPorts

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types

// ── Port types (shared by CommandProcessor, EffectSupervisor, SubsessionActor) ──

/// Host adapter: dispatch / abort / read transcript only.
type ISubsessionHost =
    abstract Dispatch: sessionId: SessionId * turn: TurnPlan -> JS.Promise<Result<HostStartReceipt, DispatchFailure>>

    abstract Abort: sessionId: SessionId * turnId: TurnId -> JS.Promise<AbortResult>

    /// Cancel an in-flight PendingTurnReceipt / dispatch waiter for this turn.
    abstract CancelPendingDispatch: turnId: TurnId -> unit

    abstract QueryDispatchStatus: SessionId * TurnId -> JS.Promise<DispatchStatus>

    abstract QuerySessionQuiescence: SessionId * TurnId -> JS.Promise<QuiescenceStatus>

    abstract ClosePhysicalSession: SessionId -> JS.Promise<QuiescenceStatus>

/// Required event store — append failure is infrastructure failure.
/// Implementations MUST treat the events list as one atomic write.
type ISubsessionEventStore =
    abstract Append: sessionId: SessionId * events: SubsessionEvent list -> JS.Promise<unit>

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

// ── Helper functions extracted from CommandProcessor ──

let isHostEffect (e: Effect) : bool =
    match e with
    | DispatchPrompt _
    | QueryDispatchStatus _
    | QuerySessionQuiescence _
    | ClosePhysicalSession _
    | AbortHostSession _ -> true
    | _ -> false

let hasCompleteCaller (effects: Effect list) : bool =
    effects
    |> List.exists (function
        | CompleteCaller _ -> true
        | _ -> false)

let rejectOpt (effects: Effect list) : StartRunError option =
    effects
    |> List.tryPick (function
        | RejectStart err -> Some err
        | _ -> None)

let filterHostEffects (effects: Effect list) : Effect list = effects |> List.filter isHostEffect

let tryExtract (s: SubsessionState) : (RunContext * ActiveTurn) option =
    match s with
    | Dispatching(ctx, plan, _) -> Some(ctx, NotYetStarted plan)
    | CancellingDispatch(ctx, plan, _) -> Some(ctx, NotYetStarted plan)
    | ReconcilingUnknownDispatch(ctx, plan, _, _) -> Some(ctx, NotYetStarted plan)
    | ClosingUnknownDispatch(ctx, plan, _) -> Some(ctx, NotYetStarted plan)
    | Running(ctx, started, _) -> Some(ctx, Started started)
    | Draining(ctx, started, _, _) -> Some(ctx, Started started)
    | IssuingAbort(ctx, turn, _, _) -> Some(ctx, turn)
    | AwaitingAbortSettle(ctx, turn, _) -> Some(ctx, turn)
    | ReconcilingAbortSettle(ctx, turn, _) -> Some(ctx, turn)
    | Available _
    | Poisoned _ -> None

let activeTid (turn: ActiveTurn) : TurnId =
    match turn with
    | NotYetStarted p -> p.TurnId
    | Started s -> s.Plan.TurnId

let buildFailSafe (priorState: SubsessionState) (msg: string) : Decision option =
    match tryExtract priorState with
    | None -> None
    | Some(ctx, turn) ->
        let tid = activeTid turn

        let abortCtx =
            { Reason = EventStoreFailure msg
              AfterStop = FinishFailed(InfrastructureFailure("event store append failed: " + msg)) }

        Some
            { NextState = IssuingAbort(ctx, turn, abortCtx, false)
              Events = []
              Effects = [ AbortHostSession(ctx.SessionId, tid); CancelPendingDispatch tid ] }
