module Wanxiangshu.Kernel.Subsession.Dispatch

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.Rules

let private handleDispatchingHostRejected (nowMs: int64) (ctx: RunContext) (plan: TurnPlan) (error: ErrorInput) =
    match nextTurnFromPolicy ctx (afterError ctx.FallbackConfig ctx.Chain ctx.Policy error) with
    | Some(ctx2, plan2) ->
        let turnDeadlineAtMs = nowMs + 300_000L

        Ok(
            decided
                (Dispatching(ctx2, plan2, CurrentTurnEvidence.empty, PendingTerminal.empty, turnDeadlineAtMs))
                [ TurnDispatchRequested
                      { RunId = ctx2.RunId
                        TurnId = plan2.TurnId
                        Ordinal = plan2.Ordinal
                        Model =
                          plan2.Model
                          |> Option.defaultValue
                              { ProviderID = ""
                                ModelID = ""
                                Variant = None
                                Temperature = None
                                TopP = None
                                MaxTokens = None
                                ReasoningEffort = None
                                Thinking = false }
                        Prompt = plan2.Prompt
                        DeadlineAtMs = turnDeadlineAtMs } ]
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

let private handleDispatchingHostAcceptanceUnknown
    (nowMs: int64)
    (ctx: RunContext)
    (plan: TurnPlan)
    (turnDeadlineAtMs: int64)
    (error: ErrorInput)
    =
    let cancelCtx =
        { Reason = AcceptanceUnknownAfterDispatch
          AfterStop = RetryAfterSafeStop error }

    let reconciliationDeadlineAtMs = nowMs + 10_000L

    Ok(
        decided
            (ReconcilingUnknownDispatch(ctx, plan, cancelCtx, 0, turnDeadlineAtMs, reconciliationDeadlineAtMs))
            []
            [ QueryDispatchStatus(ctx.SessionId, plan.TurnId) ]
    )

let private handleCancellingDispatchAccepted (nowMs: int64) (ctx: RunContext) (plan: TurnPlan) cancelCtx tid receipt =
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

let private handleCancellingRejected
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

let private handleDispatchAccepted
    (nowMs: int64)
    (ctx: RunContext)
    (plan: TurnPlan)
    (bufferedEvidence: CurrentTurnEvidence)
    (pending: PendingTerminal)
    (turnDeadlineAtMs: int64)
    (tid: TurnId)
    (receipt: HostStartReceipt)
    : DecisionResult =
    let started = { Plan = plan; StartReceipt = receipt }

    let turnStartedEvent =
        TurnStarted
            { RunId = ctx.RunId
              TurnId = tid
              Receipt = receipt }

    let result =
        match pending.PendingError with
        | Some error ->
            match bufferedEvidence.Outcome with
            | CompletionRequested _ ->
                if pending.PendingIdle then
                    DecisionObserve.handleRunningIdle nowMs ctx started bufferedEvidence
                else
                    decided (Running(ctx, started, bufferedEvidence, turnDeadlineAtMs)) [] []
            | _ ->
                if pending.PendingIdle then
                    DecisionObserve.handleDrainingIdle nowMs ctx started error bufferedEvidence
                else
                    decided (Draining(ctx, started, error, bufferedEvidence, turnDeadlineAtMs)) [] []
        | None ->
            if pending.PendingIdle then
                DecisionObserve.handleRunningIdle nowMs ctx started bufferedEvidence
            else
                decided (Running(ctx, started, bufferedEvidence, turnDeadlineAtMs)) [] []

    match result with
    | Decided decision ->
        Decided
            { decision with
                Events = turnStartedEvent :: decision.Events }
    | NoChange _ -> decided (Running(ctx, started, bufferedEvidence, turnDeadlineAtMs)) [ turnStartedEvent ] []


let private handleDispatching
    (nowMs: int64)
    (ctx: RunContext)
    (plan: TurnPlan)
    bufferedEvidence
    pending
    turnDeadlineAtMs
    cmd
    =
    match cmd with
    | DispatchAccepted(tid, receipt) when tid = plan.TurnId ->
        Ok(handleDispatchAccepted nowMs ctx plan bufferedEvidence pending turnDeadlineAtMs tid receipt)
    | DispatchAccepted _ -> Ok(noChange StaleTurnMarker)
    | DispatchRejected(tid, failure) when tid = plan.TurnId ->
        match failure with
        | HostRejected error -> handleDispatchingHostRejected nowMs ctx plan error
        | HostAcceptanceUnknown error -> handleDispatchingHostAcceptanceUnknown nowMs ctx plan turnDeadlineAtMs error
    | DispatchRejected _ -> Ok(noChange StaleTurnMarker)
    | CancelRequested ->
        Ok(
            decided
                (CancellingDispatch(
                    ctx,
                    plan,
                    { Reason = UserRequested
                      AfterStop = FinishCancelled },
                    turnDeadlineAtMs
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
                      AfterStop = FinishFailed(InfrastructureFailure "turn deadline expired before host accepted") },
                    turnDeadlineAtMs
                ))
                []
                [ CancelPendingDispatch plan.TurnId ]
        )
    | TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | SessionClosed -> Ok(closeActive ctx plan.TurnId)
    | cmd when isIllegalWhenDispatching cmd ->
        illegal (stateName (Dispatching(ctx, plan, bufferedEvidence, pending, turnDeadlineAtMs))) (cmdName cmd)
    | _ -> illegal (stateName (Dispatching(ctx, plan, bufferedEvidence, pending, turnDeadlineAtMs))) (cmdName cmd)

let private handleCancellingDispatch (nowMs: int64) (ctx: RunContext) (plan: TurnPlan) cancelCtx turnDeadlineAtMs cmd =
    match cmd with
    | DispatchAccepted(tid, receipt) when tid = plan.TurnId ->
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
        illegal (stateName (CancellingDispatch(ctx, plan, cancelCtx, turnDeadlineAtMs))) (cmdName cmd)
    | _ -> illegal (stateName (CancellingDispatch(ctx, plan, cancelCtx, turnDeadlineAtMs))) (cmdName cmd)

let decide (nowMs: int64) state cmd =
    match state, cmd with
    | Dispatching(ctx, plan, bufferedEvidence, pending, turnDeadlineAtMs), _ ->
        handleDispatching nowMs ctx plan bufferedEvidence pending turnDeadlineAtMs cmd
    | CancellingDispatch(ctx, plan, cancelCtx, turnDeadlineAtMs), _ ->
        handleCancellingDispatch nowMs ctx plan cancelCtx turnDeadlineAtMs cmd
    | _ -> illegal (stateName state) (cmdName cmd)
