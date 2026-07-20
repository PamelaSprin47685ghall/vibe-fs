module Wanxiangshu.Kernel.Subsession.Dispatch

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.Rules

let private prependEvent (ev: SubsessionEvent) (decResult: DecisionResult) : DecisionResult =
    match decResult with
    | Decided d -> Decided { d with Events = ev :: d.Events }
    | NoChange _ -> decResult

let private handleDispatchingHostRejected (nowMs: int64) (ctx: RunContext) (plan: TurnPlan) (error: ErrorInput) =
    match nextTurnFromPolicy ctx (afterError ctx.FallbackConfig ctx.Chain ctx.Policy error) with
    | Some(ctx2, plan2) ->
        let turnDeadlineAtMs = nowMs + 300_000L

        Ok(
            decided
                (Dispatching(ctx2, plan2, CurrentTurnEvidence.empty, turnDeadlineAtMs))
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
    bufferedEvidence
    turnDeadlineAtMs
    tid
    receipt
    =
    let started = { Plan = plan; StartReceipt = receipt }

    let startedEvent =
        TurnStarted
            { RunId = ctx.RunId
              TurnId = tid
              Receipt = receipt }

    if bufferedEvidence.IdleObserved then
        let errorOpt =
            match bufferedEvidence.Outcome with
            | FailureObserved error -> Some error
            | _ -> None

        let dec =
            match errorOpt with
            | Some error -> DecisionObserve.handleDrainingIdle nowMs ctx started error bufferedEvidence
            | None -> DecisionObserve.handleRunningIdle nowMs ctx started bufferedEvidence

        Ok(prependEvent startedEvent dec)
    else
        match bufferedEvidence.Outcome with
        | FailureObserved error ->
            Ok(decided (Draining(ctx, started, error, bufferedEvidence, turnDeadlineAtMs)) [ startedEvent ] [])
        | _ -> Ok(decided (Running(ctx, started, bufferedEvidence, turnDeadlineAtMs)) [ startedEvent ] [])

let private handleDispatching (nowMs: int64) (ctx: RunContext) (plan: TurnPlan) bufferedEvidence turnDeadlineAtMs cmd =
    match cmd with
    | DispatchAccepted(tid, receipt) when tid = plan.TurnId ->
        handleDispatchAccepted nowMs ctx plan bufferedEvidence turnDeadlineAtMs tid receipt
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
                    false,
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
                    false,
                    turnDeadlineAtMs
                ))
                []
                [ CancelPendingDispatch plan.TurnId ]
        )
    | TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | SessionClosed -> Ok(closeActive ctx plan.TurnId)
    | cmd when isIllegalWhenDispatching cmd ->
        illegal (stateName (Dispatching(ctx, plan, bufferedEvidence, turnDeadlineAtMs))) (cmdName cmd)
    | _ -> illegal (stateName (Dispatching(ctx, plan, bufferedEvidence, turnDeadlineAtMs))) (cmdName cmd)

let private handleCancellingDispatch
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

let decide (nowMs: int64) state cmd =
    match state, cmd with
    | Dispatching(ctx, plan, bufferedEvidence, turnDeadlineAtMs), _ ->
        handleDispatching nowMs ctx plan bufferedEvidence turnDeadlineAtMs cmd
    | CancellingDispatch(ctx, plan, cancelCtx, idleObserved, turnDeadlineAtMs), _ ->
        handleCancellingDispatch nowMs ctx plan cancelCtx idleObserved turnDeadlineAtMs cmd
    | _ -> illegal (stateName state) (cmdName cmd)
