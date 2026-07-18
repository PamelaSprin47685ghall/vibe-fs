module Wanxiangshu.Kernel.Subsession.Reconciliation

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.Rules

// ── helpers ────────────────────────────────────────────────────────────────

/// IssuingAbort state + TurnStarted + AbortRequested + AbortHostSession,
/// shared by DispatchStatus.Accepted and DispatchAccepted.
let private beginAbortAfterDispatchAccepted
    (ctx: RunContext)
    (plan: TurnPlan)
    (receipt: HostStartReceipt)
    (cancelCtx: CancelContext)
    : DecisionResult =
    let events =
        [ TurnStarted
              { RunId = ctx.RunId
                TurnId = plan.TurnId
                Receipt = receipt }
          AbortRequested(ctx.RunId, plan.TurnId) ]

    decided
        (IssuingAbort(
            ctx,
            Started { Plan = plan; StartReceipt = receipt },
            { Reason = cancelCtx.Reason
              AfterStop = cancelCtx.AfterStop },
            false
        ))
        events
        [ AbortHostSession(ctx.SessionId, plan.TurnId) ]

/// TransportRejectedBeforeSend: apply the AfterAbort policy and flatten the
/// resulting Decided / NoChange into Ok.  Returns the outer Result so the
/// caller does not double-wrap with Ok.
let private handleTransportRejectedBeforeSend
    (ctx: RunContext)
    (plan: TurnPlan)
    (cancelCtx: CancelContext)
    : Result<DecisionResult, DecisionError> =
    match
        applyAfterAbort
            ctx
            (NotYetStarted plan)
            { Reason = cancelCtx.Reason
              AfterStop = cancelCtx.AfterStop }
    with
    | Decided dec -> Ok(decided dec.NextState dec.Events dec.Effects)
    | res -> Ok(res)

/// Poisoned state for the PhysicalCloseResolved path.  Two failure messages
/// exist: one for the Stopped sub-case (which also emits PhysicalSessionClosed)
/// and one for the non-Stopped sub-case.
let private handleClosingUnknownDispatchStopped
    (ctx: RunContext)
    (plan: TurnPlan)
    (poisonReason: PoisonReason)
    : DecisionResult =
    let res =
        Failed(InfrastructureFailure "dispatch acceptance unknown after physical session close")

    let events =
        [ SessionPoisoned(ctx.SessionId, poisonReason)
          PhysicalSessionClosed ctx.SessionId
          TurnFinished(plan.TurnId, TurnInfrastructureFailed "acceptance unknown")
          RunFinished(ctx.RunId, res) ]

    decided (Poisoned poisonReason) events [ CompleteCaller(ctx.RunId, res) ]

let private handleClosingUnknownDispatchNotStopped
    (ctx: RunContext)
    (plan: TurnPlan)
    (poisonReason: PoisonReason)
    : DecisionResult =
    let res = Failed(InfrastructureFailure "physical session close could not be proven")

    let events =
        [ SessionPoisoned(ctx.SessionId, poisonReason)
          TurnFinished(plan.TurnId, TurnInfrastructureFailed "physical session close could not be proven")
          RunFinished(ctx.RunId, res) ]

    decided (Poisoned poisonReason) events [ CompleteCaller(ctx.RunId, res) ]

/// Reconciliation deadline hit: retry once by re-querying, then poison.
let private handleReconciliationDeadlineExpired
    (ctx: RunContext)
    (plan: TurnPlan)
    (cancelCtx: CancelContext)
    (retryCount: int)
    : DecisionResult =
    if retryCount >= 1 then
        decided
            (ClosingUnknownDispatch(ctx, plan, HostProtocolBroken "reconciliation deadline expired twice"))
            []
            [ ClosePhysicalSession ctx.SessionId ]
    else
        decided
            (ReconcilingUnknownDispatch(ctx, plan, cancelCtx, 1))
            []
            [ QueryDispatchStatus(ctx.SessionId, plan.TurnId) ]

/// DispatchRejected with HostRejected: resolve to Cancelled or the stored
/// Failure, finish the turn and run, and complete the caller.
let private handleDispatchRejected (ctx: RunContext) (plan: TurnPlan) (cancelCtx: CancelContext) : DecisionResult =
    let res =
        match cancelCtx.AfterStop with
        | FinishCancelled -> Cancelled
        | FinishFailed f -> Failed f
        | RetryAfterSafeStop _ -> Cancelled

    decided
        (Available { SessionId = ctx.SessionId })
        [ TurnFinished(plan.TurnId, TurnCancelled); RunFinished(ctx.RunId, res) ]
        [ CompleteCaller(ctx.RunId, res) ]

// ── main decision ───────────────────────────────────────────────────────────

let decide state cmd =
    match state, cmd with
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, retryCount), DispatchStatusResolved status ->
        match status with
        | DispatchStatus.Accepted receipt -> Ok(beginAbortAfterDispatchAccepted ctx plan receipt cancelCtx)
        | DispatchStatus.TransportRejectedBeforeSend _ -> handleTransportRejectedBeforeSend ctx plan cancelCtx
        | DispatchStatus.TransportFailedAfterUnknownAcceptance _ -> handleTransportRejectedBeforeSend ctx plan cancelCtx
        | DispatchStatus.StillPending ->
            Ok(decided (ReconcilingUnknownDispatch(ctx, plan, cancelCtx, retryCount)) [] [])
        | DispatchStatus.Unknown ->
            Ok(
                decided
                    (ClosingUnknownDispatch(ctx, plan, HostProtocolBroken "acceptance unknown and unresolvable"))
                    []
                    [ ClosePhysicalSession ctx.SessionId ]
            )
    | ClosingUnknownDispatch(ctx, plan, poisonReason), PhysicalCloseResolved Stopped ->
        Ok(handleClosingUnknownDispatchStopped ctx plan poisonReason)
    | ClosingUnknownDispatch(ctx, plan, poisonReason), PhysicalCloseResolved _ ->
        Ok(handleClosingUnknownDispatchNotStopped ctx plan poisonReason)
    | ClosingUnknownDispatch(_, _, _), SessionClosed -> Ok(noChange StaleTimer)
    | ClosingUnknownDispatch _, _ -> Ok(noChange StaleTimer)
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, retryCount), ReconciliationDeadlineExpired tid when
        tid = plan.TurnId
        ->
        Ok(handleReconciliationDeadlineExpired ctx plan cancelCtx retryCount)
    | ReconcilingUnknownDispatch _, ReconciliationDeadlineExpired _ -> Ok(noChange StaleTimer)
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, _), DispatchAccepted(tid, receipt) when tid = plan.TurnId ->
        Ok(beginAbortAfterDispatchAccepted ctx plan receipt cancelCtx)
    | ReconcilingUnknownDispatch _, DispatchAccepted _ -> Ok(noChange StaleTurnMarker)
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, _), DispatchRejected(tid, HostRejected _) when tid = plan.TurnId ->
        Ok(handleDispatchRejected ctx plan cancelCtx)
    | ReconcilingUnknownDispatch _, DispatchRejected _ -> Ok(noChange StaleTurnMarker)
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, _), SessionClosed -> Ok(closeActive ctx plan.TurnId)
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, _), SessionIdleObserved ->
        Ok(applyAfterAbort ctx (NotYetStarted plan) cancelCtx)
    | ReconcilingUnknownDispatch _, CancelRequested
    | ReconcilingUnknownDispatch _, TurnErrorObserved _
    | ReconcilingUnknownDispatch _, EvidenceUpdated _
    | ReconcilingUnknownDispatch _, TurnDeadlineExpired _
    | ReconcilingUnknownDispatch _, AbortDeadlineExpired _ -> Ok(noChange StaleTimer)
    | ReconcilingUnknownDispatch _,
      (AbortConfirmed _ | AbortHostAccepted _ | AbortRequestFailed _ | SessionQuiescenceResolved _) ->
        illegal (stateName state) (cmdName cmd)
    | _ -> illegal (stateName state) (cmdName cmd)
