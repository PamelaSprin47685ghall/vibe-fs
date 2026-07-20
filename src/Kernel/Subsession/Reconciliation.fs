module Wanxiangshu.Kernel.Subsession.Reconciliation

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.Rules

// ── helpers ────────────────────────────────────────────────────────────────

/// IssuingAbort state + TurnStarted + AbortRequested + AbortHostSession,
/// shared by DispatchStatus.Accepted and DispatchAccepted.
let private beginAbortAfterDispatchAccepted
    (nowMs: int64)
    (ctx: RunContext)
    (plan: TurnPlan)
    (receipt: HostStartReceipt)
    (cancelCtx: CancelContext)
    : DecisionResult =
    let abortDeadlineAtMs = nowMs + 60_000L

    let events =
        [ TurnStarted
              { RunId = ctx.RunId
                TurnId = plan.TurnId
                Receipt = receipt }
          AbortRequested(ctx.RunId, plan.TurnId, abortDeadlineAtMs) ]

    decided
        (IssuingAbort(
            ctx,
            Started { Plan = plan; StartReceipt = receipt },
            { Reason = cancelCtx.Reason
              AfterStop = cancelCtx.AfterStop },
            false,
            abortDeadlineAtMs
        ))
        events
        [ AbortHostSession(ctx.SessionId, plan.TurnId) ]

/// TransportRejectedBeforeSend: apply the AfterAbort policy and flatten the
/// resulting Decided / NoChange into Ok.  Returns the outer Result so the
/// caller does not double-wrap with Ok.
let private handleTransportRejectedBeforeSend
    (nowMs: int64)
    (ctx: RunContext)
    (plan: TurnPlan)
    (cancelCtx: CancelContext)
    : Result<DecisionResult, DecisionError> =
    match
        applyAfterAbort
            nowMs
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
    (nowMs: int64)
    (ctx: RunContext)
    (plan: TurnPlan)
    (cancelCtx: CancelContext)
    (retryCount: int)
    (turnDeadlineAtMs: int64)
    : DecisionResult =
    if retryCount >= 1 then
        let reconciliationDeadlineAtMs = nowMs + 30_000L

        decided
            (ClosingUnknownDispatch(
                ctx,
                plan,
                HostProtocolBroken "reconciliation deadline expired twice",
                turnDeadlineAtMs,
                reconciliationDeadlineAtMs
            ))
            []
            [ ClosePhysicalSession ctx.SessionId ]
    else
        let reconciliationDeadlineAtMs = nowMs + 30_000L

        decided
            (ReconcilingUnknownDispatch(ctx, plan, cancelCtx, 1, turnDeadlineAtMs, reconciliationDeadlineAtMs))
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

let private handleReconcilingDispatchStatusResolved
    (nowMs: int64)
    (ctx: RunContext)
    (plan: TurnPlan)
    (cancelCtx: CancelContext)
    (retryCount: int)
    (turnDeadlineAtMs: int64)
    (reconciliationDeadlineAtMs: int64)
    (status: DispatchStatus)
    : DecisionResult =
    match status with
    | DispatchStatus.Accepted receipt -> beginAbortAfterDispatchAccepted nowMs ctx plan receipt cancelCtx
    | DispatchStatus.TransportRejectedBeforeSend _
    | DispatchStatus.TransportFailedAfterUnknownAcceptance _ ->
        handleTransportRejectedBeforeSend nowMs ctx plan cancelCtx
    | DispatchStatus.StillPending ->
        decided
            (ReconcilingUnknownDispatch(ctx, plan, cancelCtx, retryCount, turnDeadlineAtMs, reconciliationDeadlineAtMs))
            []
            []
    | DispatchStatus.Unknown ->
        let reconciliationDeadlineAtMs2 = nowMs + 30_000L

        decided
            (ClosingUnknownDispatch(
                ctx,
                plan,
                HostProtocolBroken "acceptance unknown and unresolvable",
                turnDeadlineAtMs,
                reconciliationDeadlineAtMs2
            ))
            []
            [ ClosePhysicalSession ctx.SessionId ]


let decide (nowMs: int64) state cmd =
    match state, cmd with
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, retryCount, turnDeadlineAtMs, reconciliationDeadlineAtMs),
      DispatchStatusResolved status ->
        Ok(
            handleReconcilingDispatchStatusResolved
                nowMs
                ctx
                plan
                cancelCtx
                retryCount
                turnDeadlineAtMs
                reconciliationDeadlineAtMs
                status
        )
    | ClosingUnknownDispatch(ctx, plan, poisonReason, turnDeadlineAtMs, reconciliationDeadlineAtMs),
      PhysicalCloseResolved Stopped -> Ok(handleClosingUnknownDispatchStopped ctx plan poisonReason)
    | ClosingUnknownDispatch(ctx, plan, poisonReason, turnDeadlineAtMs, reconciliationDeadlineAtMs),
      PhysicalCloseResolved _ -> Ok(handleClosingUnknownDispatchNotStopped ctx plan poisonReason)
    | ClosingUnknownDispatch(_, _, _, _, _), SessionClosed -> Ok(noChange StaleTimer)
    | ClosingUnknownDispatch _, _ -> Ok(noChange StaleTimer)
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, retryCount, turnDeadlineAtMs, reconciliationDeadlineAtMs),
      ReconciliationDeadlineExpired tid when tid = plan.TurnId ->
        Ok(handleReconciliationDeadlineExpired nowMs ctx plan cancelCtx retryCount turnDeadlineAtMs)
    | ReconcilingUnknownDispatch _, ReconciliationDeadlineExpired _ -> Ok(noChange StaleTimer)
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, _, turnDeadlineAtMs, reconciliationDeadlineAtMs),
      DispatchAccepted(tid, receipt) when tid = plan.TurnId ->
        Ok(beginAbortAfterDispatchAccepted nowMs ctx plan receipt cancelCtx)
    | ReconcilingUnknownDispatch _, DispatchAccepted _ -> Ok(noChange StaleTurnMarker)
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, _, turnDeadlineAtMs, reconciliationDeadlineAtMs),
      DispatchRejected(tid, HostRejected _) when tid = plan.TurnId -> Ok(handleDispatchRejected ctx plan cancelCtx)
    | ReconcilingUnknownDispatch _, DispatchRejected _ -> Ok(noChange StaleTurnMarker)
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, _, turnDeadlineAtMs, reconciliationDeadlineAtMs), SessionClosed ->
        Ok(closeActive ctx plan.TurnId)
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, _, turnDeadlineAtMs, reconciliationDeadlineAtMs),
      SessionIdleObserved -> Ok(applyAfterAbort nowMs ctx (NotYetStarted plan) cancelCtx)
    | ReconcilingUnknownDispatch _, CancelRequested
    | ReconcilingUnknownDispatch _, TurnErrorObserved _
    | ReconcilingUnknownDispatch _, EvidenceUpdated _
    | ReconcilingUnknownDispatch _, TurnDeadlineExpired _
    | ReconcilingUnknownDispatch _, AbortDeadlineExpired _ -> Ok(noChange StaleTimer)
    | ReconcilingUnknownDispatch _,
      (AbortConfirmed _ | AbortHostAccepted _ | AbortRequestFailed _ | SessionQuiescenceResolved _) ->
        illegal (stateName state) (cmdName cmd)
    | _ -> illegal (stateName state) (cmdName cmd)
