module Wanxiangshu.Kernel.Subsession.Reconciliation

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.Rules

// ARCHITECTURE_EXEMPT: split this 127-line function later
let decide state cmd =
    match state, cmd with
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, retryCount), DispatchStatusResolved status ->
        match status with
        | DispatchStatus.Accepted receipt ->
            let events =
                [ TurnStarted
                      { RunId = ctx.RunId
                        TurnId = plan.TurnId
                        Receipt = receipt }
                  AbortRequested(ctx.RunId, plan.TurnId) ]

            Ok(
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
            )
        | DispatchStatus.TransportRejectedBeforeSend _ ->
            match
                applyAfterAbort
                    ctx
                    (NotYetStarted plan)
                    { Reason = cancelCtx.Reason
                      AfterStop = cancelCtx.AfterStop }
            with
            | Decided dec -> Ok(decided dec.NextState dec.Events dec.Effects)
            | res -> Ok(res)
        | DispatchStatus.StillPending
        | DispatchStatus.TransportFailedAfterUnknownAcceptance _ ->
            Ok(decided (ReconcilingUnknownDispatch(ctx, plan, cancelCtx, retryCount)) [] [])
        | DispatchStatus.Unknown ->
            Ok(
                decided
                    (ClosingUnknownDispatch(ctx, plan, HostProtocolBroken "acceptance unknown and unresolvable"))
                    []
                    [ ClosePhysicalSession ctx.SessionId ]
            )
    | ClosingUnknownDispatch(ctx, plan, poisonReason), PhysicalCloseResolved Stopped ->
        let res =
            Failed(InfrastructureFailure "dispatch acceptance unknown after physical session close")

        let events =
            [ SessionPoisoned(ctx.SessionId, poisonReason)
              PhysicalSessionClosed ctx.SessionId
              TurnFinished(plan.TurnId, TurnInfrastructureFailed "acceptance unknown")
              RunFinished(ctx.RunId, res) ]

        Ok(decided (Poisoned poisonReason) events [ CompleteCaller(ctx.RunId, res) ])
    | ClosingUnknownDispatch(ctx, plan, poisonReason), PhysicalCloseResolved _ ->
        let res = Failed(InfrastructureFailure "physical session close could not be proven")

        let events =
            [ SessionPoisoned(ctx.SessionId, poisonReason)
              TurnFinished(plan.TurnId, TurnInfrastructureFailed "physical session close could not be proven")
              RunFinished(ctx.RunId, res) ]

        Ok(decided (Poisoned poisonReason) events [ CompleteCaller(ctx.RunId, res) ])
    | ClosingUnknownDispatch(_, _, _), SessionClosed -> Ok(noChange StaleTimer)
    | ClosingUnknownDispatch _, _ -> Ok(noChange StaleTimer)
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, retryCount), ReconciliationDeadlineExpired tid when
        tid = plan.TurnId
        ->
        if retryCount >= 1 then
            Ok(
                decided
                    (ClosingUnknownDispatch(ctx, plan, HostProtocolBroken "reconciliation deadline expired twice"))
                    []
                    [ ClosePhysicalSession ctx.SessionId ]
            )
        else
            Ok(
                decided
                    (ReconcilingUnknownDispatch(ctx, plan, cancelCtx, 1))
                    []
                    [ QueryDispatchStatus(ctx.SessionId, plan.TurnId) ]
            )
    | ReconcilingUnknownDispatch _, ReconciliationDeadlineExpired _ -> Ok(noChange StaleTimer)
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, _), DispatchAccepted(tid, receipt) when tid = plan.TurnId ->
        let events =
            [ TurnStarted
                  { RunId = ctx.RunId
                    TurnId = tid
                    Receipt = receipt }
              AbortRequested(ctx.RunId, tid) ]

        Ok(
            decided
                (IssuingAbort(
                    ctx,
                    Started { Plan = plan; StartReceipt = receipt },
                    { Reason = cancelCtx.Reason
                      AfterStop = cancelCtx.AfterStop },
                    false
                ))
                events
                [ AbortHostSession(ctx.SessionId, tid) ]
        )
    | ReconcilingUnknownDispatch _, DispatchAccepted _ -> Ok(noChange StaleTurnMarker)
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, _), DispatchRejected(tid, HostRejected _) when tid = plan.TurnId ->
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
    | ReconcilingUnknownDispatch _, DispatchRejected _ -> Ok(noChange StaleTurnMarker)
    | ReconcilingUnknownDispatch(ctx, plan, _, _), SessionClosed -> Ok(closeActive ctx plan.TurnId)
    | ReconcilingUnknownDispatch _, CancelRequested
    | ReconcilingUnknownDispatch _, TurnDeadlineExpired _
    | ReconcilingUnknownDispatch _, AbortDeadlineExpired _ -> Ok(noChange StaleTimer)
    | ReconcilingUnknownDispatch _,
      (AbortConfirmed _ | AbortHostAccepted _ | AbortRequestFailed _ | SessionQuiescenceResolved _) ->
        illegal (stateName state) (cmdName cmd)
    | _ -> illegal (stateName state) (cmdName cmd)
