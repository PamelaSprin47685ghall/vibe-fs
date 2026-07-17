module Wanxiangshu.Kernel.Subsession.Dispatch

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.Rules

// ARCHITECTURE_EXEMPT: split this 130-line function later
let decide state cmd =
    match state, cmd with
    | Dispatching(ctx, plan, bufferedEvidence), DispatchAccepted(tid, receipt) when tid = plan.TurnId ->
        Ok(
            decided
                (Running(ctx, { Plan = plan; StartReceipt = receipt }, bufferedEvidence))
                [ TurnStarted
                      { RunId = ctx.RunId
                        TurnId = tid
                        Receipt = receipt } ]
                []
        )
    | Dispatching _, DispatchAccepted _ -> Ok(noChange StaleTurnMarker)
    | Dispatching(ctx, plan, _), DispatchRejected(tid, failure) when tid = plan.TurnId ->
        match failure with
        | HostRejected error ->
            match nextTurnFromPolicy ctx (afterError ctx.FallbackConfig ctx.Chain ctx.Policy error) with
            | Some(ctx2, plan2) ->
                Ok(
                    decided
                        (Dispatching(ctx2, plan2, CurrentTurnEvidence.empty))
                        [ TurnDispatchRequested(makeTurnData ctx2 plan2) ]
                        [ CancelPendingDispatch plan.TurnId; DispatchPrompt plan2 ]
                )
            | None ->
                Ok(
                    failRun
                        ctx
                        (match afterError ctx.FallbackConfig ctx.Chain ctx.Policy error with
                         | StopWithFailure f -> f
                         | _ -> FallbackExhausted error)
                        [ TurnFinished(plan.TurnId, TurnFailed error) ]
                )
        | HostAcceptanceUnknown error ->
            let cancelCtx =
                { Reason = AcceptanceUnknownAfterDispatch
                  AfterStop = RetryAfterSafeStop error }

            Ok(
                decided
                    (ReconcilingUnknownDispatch(ctx, plan, cancelCtx, 0))
                    []
                    [ QueryDispatchStatus(ctx.SessionId, plan.TurnId) ]
            )
    | Dispatching _, DispatchRejected _ -> Ok(noChange StaleTurnMarker)
    | Dispatching(ctx, plan, _), CancelRequested ->
        Ok(
            decided
                (CancellingDispatch(
                    ctx,
                    plan,
                    { Reason = UserRequested
                      AfterStop = FinishCancelled }
                ))
                []
                [ CancelPendingDispatch plan.TurnId ]
        )
    | Dispatching(ctx, plan, _), TurnDeadlineExpired tid when tid = plan.TurnId ->
        Ok(
            decided
                (CancellingDispatch(
                    ctx,
                    plan,
                    { Reason = TurnDeadline
                      AfterStop = FinishFailed(InfrastructureFailure "turn deadline expired before host accepted") }
                ))
                []
                [ CancelPendingDispatch plan.TurnId ]
        )
    | Dispatching _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | Dispatching(ctx, plan, _), SessionClosed -> Ok(closeActive ctx plan.TurnId)
    | Dispatching _, cmd when isIllegalWhenDispatching cmd -> illegal (stateName state) (cmdName cmd)

    | CancellingDispatch(ctx, plan, cancelCtx), DispatchAccepted(tid, receipt) when tid = plan.TurnId ->
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
    | CancellingDispatch _, DispatchAccepted _ -> Ok(noChange StaleTurnMarker)
    | CancellingDispatch(ctx, plan, cancelCtx), DispatchRejected(tid, failure) when tid = plan.TurnId ->
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
            Ok(
                decided
                    (ReconcilingUnknownDispatch(ctx, plan, cancelCtx, 0))
                    []
                    [ QueryDispatchStatus(ctx.SessionId, plan.TurnId) ]
            )
    | CancellingDispatch _, DispatchRejected _ -> Ok(noChange StaleTurnMarker)
    | CancellingDispatch _, CancelRequested -> Ok(noChange StaleTimer)
    | CancellingDispatch(ctx, plan, cancelCtx), TurnDeadlineExpired tid when tid = plan.TurnId ->
        Ok(
            decided
                (ReconcilingUnknownDispatch(ctx, plan, cancelCtx, 0))
                []
                [ QueryDispatchStatus(ctx.SessionId, plan.TurnId) ]
        )
    | CancellingDispatch _, TurnDeadlineExpired _
    | CancellingDispatch _, AbortDeadlineExpired _ -> Ok(noChange StaleTimer)
    | CancellingDispatch(ctx, plan, _), SessionClosed -> Ok(closeActive ctx plan.TurnId)
    | CancellingDispatch _, cmd when isIllegalWhenCancelling cmd -> illegal (stateName state) (cmdName cmd)
    | _ -> illegal (stateName state) (cmdName cmd)
