module Wanxiangshu.Kernel.Subsession.Dispatch

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.Rules

let private handleDispatchingHostRejected (ctx: RunContext) (plan: TurnPlan) (error: ErrorInput) =
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

let private handleDispatchingHostAcceptanceUnknown (ctx: RunContext) (plan: TurnPlan) (error: ErrorInput) =
    let cancelCtx =
        { Reason = AcceptanceUnknownAfterDispatch
          AfterStop = RetryAfterSafeStop error }

    Ok(
        decided
            (ReconcilingUnknownDispatch(ctx, plan, cancelCtx, 0))
            []
            [ QueryDispatchStatus(ctx.SessionId, plan.TurnId) ]
    )

let private handleCancellingDispatchAccepted (ctx: RunContext) (plan: TurnPlan) cancelCtx tid receipt =
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

let private handleCancellingRejected (ctx: RunContext) (plan: TurnPlan) cancelCtx failure =
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

let private handleDispatching (ctx: RunContext) (plan: TurnPlan) bufferedEvidence cmd =
    match cmd with
    | DispatchAccepted(tid, receipt) when tid = plan.TurnId ->
        Ok(
            decided
                (Running(ctx, { Plan = plan; StartReceipt = receipt }, bufferedEvidence))
                [ TurnStarted
                      { RunId = ctx.RunId
                        TurnId = tid
                        Receipt = receipt } ]
                []
        )
    | DispatchAccepted _ -> Ok(noChange StaleTurnMarker)
    | DispatchRejected(tid, failure) when tid = plan.TurnId ->
        match failure with
        | HostRejected error -> handleDispatchingHostRejected ctx plan error
        | HostAcceptanceUnknown error -> handleDispatchingHostAcceptanceUnknown ctx plan error
    | DispatchRejected _ -> Ok(noChange StaleTurnMarker)
    | CancelRequested ->
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
    | TurnDeadlineExpired tid when tid = plan.TurnId ->
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
    | TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | SessionClosed -> Ok(closeActive ctx plan.TurnId)
    | cmd when isIllegalWhenDispatching cmd ->
        illegal (stateName (Dispatching(ctx, plan, bufferedEvidence))) (cmdName cmd)
    | _ -> illegal (stateName (Dispatching(ctx, plan, bufferedEvidence))) (cmdName cmd)

let private handleCancellingDispatch (ctx: RunContext) (plan: TurnPlan) cancelCtx cmd =
    match cmd with
    | DispatchAccepted(tid, receipt) when tid = plan.TurnId ->
        handleCancellingDispatchAccepted ctx plan cancelCtx tid receipt
    | DispatchAccepted _ -> Ok(noChange StaleTurnMarker)
    | DispatchRejected(tid, failure) when tid = plan.TurnId -> handleCancellingRejected ctx plan cancelCtx failure
    | DispatchRejected _ -> Ok(noChange StaleTurnMarker)
    | CancelRequested -> Ok(noChange StaleTimer)
    | TurnDeadlineExpired tid when tid = plan.TurnId ->
        Ok(
            decided
                (ReconcilingUnknownDispatch(ctx, plan, cancelCtx, 0))
                []
                [ QueryDispatchStatus(ctx.SessionId, plan.TurnId) ]
        )
    | TurnDeadlineExpired _
    | AbortDeadlineExpired _ -> Ok(noChange StaleTimer)
    | SessionClosed -> Ok(closeActive ctx plan.TurnId)
    | cmd when isIllegalWhenCancelling cmd ->
        illegal (stateName (CancellingDispatch(ctx, plan, cancelCtx))) (cmdName cmd)
    | _ -> illegal (stateName (CancellingDispatch(ctx, plan, cancelCtx))) (cmdName cmd)

let decide state cmd =
    match state, cmd with
    | Dispatching(ctx, plan, bufferedEvidence), _ -> handleDispatching ctx plan bufferedEvidence cmd
    | CancellingDispatch(ctx, plan, cancelCtx), _ -> handleCancellingDispatch ctx plan cancelCtx cmd
    | _ -> illegal (stateName state) (cmdName cmd)
