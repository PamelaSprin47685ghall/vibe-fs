module Wanxiangshu.Kernel.Subsession.DispatchCancelling

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.Rules

let prependEvent (ev: SubsessionEvent) (decResult: DecisionResult) : DecisionResult =
    match decResult with
    | Decided d -> Decided { d with Events = ev :: d.Events }
    | NoChange _ -> decResult

let handleCancellingDispatchAccepted (nowMs: int64) (ctx: RunContext) (plan: TurnPlan) cancelCtx tid receipt =
    let abortDeadlineAtMs = nowMs + 30_000L

    let events =
        [ TurnStarted
              { RunId = ctx.RunId
                TurnId = tid
                Receipt = receipt }
          AbortRequested(ctx.RunId, tid, abortDeadlineAtMs) ]

    Ok(
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
            [ AbortHostSession(ctx.SessionId, tid) ]
    )

let handleCancellingRejected
    (nowMs: int64)
    (ctx: RunContext)
    (plan: TurnPlan)
    (turnDeadlineAtMs: int64)
    cancelCtx
    failure
    =
    match failure with
    | HostRejected _ ->
        let res =
            match cancelCtx.AfterStop with
            | FinishCancelled -> Cancelled
            | FinishFailed f -> Failed f
            | RetryAfterSafeStop _ -> Cancelled

        Ok(
            decided
                (Available { SessionId = ctx.SessionId })
                [ TurnFinished(plan.TurnId, TurnCancelled); RunFinished(ctx.RunId, res) ]
                [ CompleteCaller(ctx.RunId, res) ]
        )
    | HostAcceptanceUnknown _ ->
        let reconciliationDeadlineAtMs = nowMs + 10_000L

        Ok(
            decided
                (ReconcilingUnknownDispatch(ctx, plan, cancelCtx, 0, turnDeadlineAtMs, reconciliationDeadlineAtMs))
                []
                [ QueryDispatchStatus(ctx.SessionId, plan.TurnId) ]
        )

let handleCancellingDispatch
    (nowMs: int64)
    (ctx: RunContext)
    (plan: TurnPlan)
    cancelCtx
    idleObserved
    turnDeadlineAtMs
    cmd
    =
    match cmd with
    | DispatchAccepted(tid, receipt) when tid = plan.TurnId ->
        if idleObserved then
            let started = { Plan = plan; StartReceipt = receipt }

            let startedEvent =
                TurnStarted
                    { RunId = ctx.RunId
                      TurnId = tid
                      Receipt = receipt }

            let dec = applyAfterAbort nowMs ctx (Started started) cancelCtx
            Ok(prependEvent startedEvent dec)
        else
            handleCancellingDispatchAccepted nowMs ctx plan cancelCtx tid receipt
    | DispatchAccepted _ -> Ok(noChange StaleTurnMarker)
    | DispatchRejected(tid, failure) when tid = plan.TurnId ->
        handleCancellingRejected nowMs ctx plan turnDeadlineAtMs cancelCtx failure
    | DispatchRejected _ -> Ok(noChange StaleTurnMarker)
    | CancelRequested -> Ok(noChange StaleTimer)
    | TurnDeadlineExpired tid when tid = plan.TurnId ->
        let reconciliationDeadlineAtMs = nowMs + 10_000L

        Ok(
            decided
                (ReconcilingUnknownDispatch(ctx, plan, cancelCtx, 0, turnDeadlineAtMs, reconciliationDeadlineAtMs))
                []
                [ QueryDispatchStatus(ctx.SessionId, plan.TurnId) ]
        )
    | TurnDeadlineExpired _
    | AbortDeadlineExpired _ -> Ok(noChange StaleTimer)
    | SessionClosed -> Ok(closeActive ctx plan.TurnId)
    | cmd when isIllegalWhenCancelling cmd ->
        illegal (stateName (CancellingDispatch(ctx, plan, cancelCtx, idleObserved, turnDeadlineAtMs))) (cmdName cmd)
    | _ -> illegal (stateName (CancellingDispatch(ctx, plan, cancelCtx, idleObserved, turnDeadlineAtMs))) (cmdName cmd)
