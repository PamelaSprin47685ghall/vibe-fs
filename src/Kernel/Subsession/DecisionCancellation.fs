module Wanxiangshu.Kernel.Subsession.DecisionCancellation

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Policy

let private delegateToHostSentinel =
    { ProviderID = ""
      ModelID = ""
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private makeTurnData (c: RunContext) (p: TurnPlan) : TurnData =
    { RunId = c.RunId
      TurnId = p.TurnId
      Ordinal = p.Ordinal
      Model = p.Model |> Option.defaultValue delegateToHostSentinel
      Prompt = p.Prompt }

let private nextTurnFromPolicy (ctx: RunContext) (decision: PolicyDecision) : (RunContext * TurnPlan) option =
    match decision with
    | NextTurn(policy2, model, prompt) ->
        let ordinal = ctx.NextTurnOrdinal

        let turnId =
            TurnId.create (RunId.value ctx.RunId + "-t" + string (TurnOrdinal.value ordinal))

        let plan =
            { TurnId = turnId
              Ordinal = ordinal
              Model = Some model
              Prompt = prompt }

        Some(
            { ctx with
                Policy = policy2
                NextTurnOrdinal = TurnOrdinal.next ordinal },
            plan
        )
    | StopWithFailure _ -> None

let private activeTurnId (turn: ActiveTurn) : TurnId =
    match turn with
    | NotYetStarted p -> p.TurnId
    | Started s -> s.Plan.TurnId

let private decided state events effects : DecisionResult =
    Decided
        { NextState = state
          Events = events
          Effects = effects }

let private noChange reason : DecisionResult = NoChange reason

let private illegal state cmd : Result<DecisionResult, DecisionError> = Error(IllegalTransition(state, cmd))

let private failRun (ctx: RunContext) (failure: RunFailure) (extraEvents: SubsessionEvent list) =
    let result = Failed failure

    decided
        (Available { SessionId = ctx.SessionId })
        (extraEvents @ [ RunFinished(ctx.RunId, result) ])
        [ CompleteCaller(ctx.RunId, result) ]

let private finishWithResult (ctx: RunContext) (result: RunResult) (turnId: TurnId) =
    let finishEvent =
        match result with
        | Succeeded output -> TurnFinished(turnId, TurnCompleted output)
        | Failed(FallbackExhausted err) -> TurnFinished(turnId, TurnFailed err)
        | Failed(InfrastructureFailure reason) -> TurnFinished(turnId, TurnInfrastructureFailed reason)
        | Failed(ProtocolViolation reason) -> TurnFinished(turnId, TurnInfrastructureFailed reason)
        | Failed(RecoveryExhausted reason) -> TurnFinished(turnId, TurnInfrastructureFailed reason)
        | Failed NoModelConfigured -> TurnFinished(turnId, TurnInfrastructureFailed "no model configured")
        | Cancelled -> TurnFinished(turnId, TurnCancelled)

    decided
        (Available { SessionId = ctx.SessionId })
        [ finishEvent; RunFinished(ctx.RunId, result) ]
        [ CompleteCaller(ctx.RunId, result) ]

let private beginAbort (ctx: RunContext) (turn: ActiveTurn) (reason: AbortReason) (afterStop: AfterAbort) =
    let tid = activeTurnId turn
    let events = [ AbortRequested(ctx.RunId, tid) ]
    let effects = [ AbortHostSession(ctx.SessionId, tid); CancelPendingDispatch tid ]

    decided
        (IssuingAbort(
            ctx,
            turn,
            { Reason = reason
              AfterStop = afterStop },
            false
        ))
        events
        effects

let private closeActive (ctx: RunContext) (turnId: TurnId) =
    let poisoned = Poisoned SessionClosedUnexpectedly
    let result = Failed(InfrastructureFailure "session closed")

    let events =
        [ SessionPoisoned(ctx.SessionId, SessionClosedUnexpectedly)
          TurnFinished(turnId, TurnInfrastructureFailed "session closed")
          RunFinished(ctx.RunId, result) ]

    decided
        poisoned
        events
        [ CancelPendingDispatch turnId
          CompleteCaller(ctx.RunId, result)
          DisposeActor ]

let private applyAfterAbort (ctx: RunContext) (turn: ActiveTurn) (abortCtx: AbortContext) =
    let tid = activeTurnId turn

    match abortCtx.AfterStop with
    | FinishCancelled -> finishWithResult ctx Cancelled tid
    | FinishFailed failure -> finishWithResult ctx (Failed failure) tid
    | RetryAfterSafeStop error ->
        match nextTurnFromPolicy ctx (afterError ctx.FallbackConfig ctx.Chain ctx.Policy error) with
        | Some(ctx2, plan2) ->
            let events =
                [ TurnFinished(tid, TurnFailed error)
                  TurnDispatchRequested(makeTurnData ctx2 plan2) ]

            decided (Dispatching(ctx2, plan2, CurrentTurnEvidence.empty)) events [ DispatchPrompt plan2 ]
        | None ->
            let failure' =
                match afterError ctx.FallbackConfig ctx.Chain ctx.Policy error with
                | StopWithFailure f -> f
                | _ -> FallbackExhausted error

            finishWithResult ctx (Failed failure') tid

let private isStaleTimerCommand =
    function
    | TurnDeadlineExpired _
    | AbortDeadlineExpired _
    | AbortConfirmed _
    | AbortHostAccepted _
    | AbortRequestFailed _
    | EvidenceUpdated _
    | ReconciliationDeadlineExpired _
    | SessionQuiescenceResolved _ -> true
    | _ -> false

let private isIllegalWhenDispatching =
    function
    | AbortDeadlineExpired _
    | DispatchStatusResolved _
    | AbortConfirmed _
    | AbortHostAccepted _
    | AbortRequestFailed _
    | ReconciliationDeadlineExpired _
    | SessionQuiescenceResolved _ -> true
    | _ -> false

let private isIllegalWhenCancelling =
    function
    | DispatchStatusResolved _
    | AbortConfirmed _
    | AbortHostAccepted _
    | AbortRequestFailed _
    | ReconciliationDeadlineExpired _
    | SessionQuiescenceResolved _ -> true
    | _ -> false

let private isIllegalWhenRunning =
    function
    | DispatchStatusResolved _
    | AbortDeadlineExpired _
    | AbortConfirmed _
    | AbortHostAccepted _
    | AbortRequestFailed _
    | ReconciliationDeadlineExpired _
    | SessionQuiescenceResolved _ -> true
    | _ -> false

let private isIllegalWhenDraining = isIllegalWhenRunning

let private stateName state =
    match state with
    | Available _ -> "Available"
    | Dispatching _ -> "Dispatching"
    | CancellingDispatch _ -> "CancellingDispatch"
    | ReconcilingUnknownDispatch _ -> "ReconcilingUnknownDispatch"
    | ClosingUnknownDispatch _ -> "ClosingUnknownDispatch"
    | Running _ -> "Running(evidence)"
    | Draining _ -> "Draining"
    | IssuingAbort _ -> "IssuingAbort"
    | AwaitingAbortSettle _ -> "AwaitingAbortSettle"
    | ReconcilingAbortSettle _ -> "ReconcilingAbortSettle"
    | Poisoned _ -> "Poisoned"

let private cmdName cmd =
    match cmd with
    | DispatchAccepted _ -> "DispatchAccepted"
    | DispatchRejected _ -> "DispatchRejected"
    | DispatchStatusResolved _ -> "DispatchStatusResolved"
    | CancelRequested -> "CancelRequested"
    | TurnDeadlineExpired _ -> "TurnDeadlineExpired"
    | AbortDeadlineExpired _ -> "AbortDeadlineExpired"
    | ReconciliationDeadlineExpired _ -> "ReconciliationDeadlineExpired"
    | AbortConfirmed _ -> "AbortConfirmed"
    | AbortHostAccepted _ -> "AbortHostAccepted"
    | AbortRequestFailed _ -> "AbortRequestFailed"
    | SessionQuiescenceResolved _ -> "SessionQuiescenceResolved"
    | PhysicalCloseResolved _ -> "PhysicalCloseResolved"
    | SessionClosed -> "SessionClosed"
    | _ -> "Other"

let private decideDispatch state cmd =
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

let private decideReconcile state cmd =
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

let private decideRunningDraining state cmd =
    match state, cmd with
    | Running(ctx, started, _), CancelRequested -> Ok(beginAbort ctx (Started started) UserRequested FinishCancelled)
    | Running(ctx, started, _), TurnDeadlineExpired tid when tid = started.Plan.TurnId ->
        Ok(beginAbort ctx (Started started) TurnDeadline (FinishFailed(InfrastructureFailure "turn deadline expired")))
    | Running _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | Running _, (DispatchAccepted _ | DispatchRejected _) -> Ok(noChange StaleTurnMarker)
    | Running(ctx, started, _), SessionClosed -> Ok(closeActive ctx started.Plan.TurnId)
    | Running _, cmd when isIllegalWhenRunning cmd -> illegal (stateName state) (cmdName cmd)

    | Draining(ctx, started, _, _), CancelRequested ->
        Ok(beginAbort ctx (Started started) UserRequested FinishCancelled)
    | Draining(ctx, started, _, _), TurnDeadlineExpired tid when tid = started.Plan.TurnId ->
        Ok(
            beginAbort
                ctx
                (Started started)
                TurnDeadline
                (FinishFailed(InfrastructureFailure "turn deadline expired while draining"))
        )
    | Draining _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | Draining _, (DispatchAccepted _ | DispatchRejected _) -> Ok(noChange StaleTurnMarker)
    | Draining(ctx, started, _, _), SessionClosed -> Ok(closeActive ctx started.Plan.TurnId)
    | Draining _, cmd when isIllegalWhenDraining cmd -> illegal (stateName state) (cmdName cmd)
    | _ -> illegal (stateName state) (cmdName cmd)

let private decideIssuingAbort ctx turn abortCtx idleObserved cmd =
    match cmd with
    | AbortConfirmed tid when tid = activeTurnId turn -> Ok(applyAfterAbort ctx turn abortCtx)
    | AbortConfirmed _ -> Ok(noChange StaleTurnMarker)
    | AbortHostAccepted tid when tid = activeTurnId turn ->
        if idleObserved then
            Ok(decided (ReconcilingAbortSettle(ctx, turn, abortCtx)) [] [ QuerySessionQuiescence(ctx.SessionId, tid) ])
        else
            Ok(decided (AwaitingAbortSettle(ctx, turn, abortCtx)) [] [])
    | AbortHostAccepted _ -> Ok(noChange StaleTurnMarker)
    | AbortRequestFailed _ -> Ok(noChange AbortInProgress)
    | DispatchAccepted(tid, receipt) when tid = activeTurnId turn ->
        match turn with
        | NotYetStarted plan ->
            Ok(
                decided
                    (IssuingAbort(ctx, Started { Plan = plan; StartReceipt = receipt }, abortCtx, idleObserved))
                    [ TurnStarted
                          { RunId = ctx.RunId
                            TurnId = tid
                            Receipt = receipt } ]
                    []
            )
        | Started _ -> Ok(noChange StaleTurnMarker)
    | DispatchAccepted _
    | DispatchRejected _ -> Ok(noChange StaleTurnMarker)
    | AbortDeadlineExpired tid when tid = activeTurnId turn ->
        let res = Failed(InfrastructureFailure "abort deadline expired")

        Ok(
            decided
                (Poisoned(AbortDidNotSettle tid))
                [ SessionPoisoned(ctx.SessionId, AbortDidNotSettle tid)
                  TurnFinished(tid, TurnInfrastructureFailed "abort deadline expired")
                  RunFinished(ctx.RunId, res) ]
                [ CompleteCaller(ctx.RunId, res) ]
        )
    | AbortDeadlineExpired _
    | CancelRequested
    | TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | SessionClosed -> Ok(closeActive ctx (activeTurnId turn))
    | _ -> illegal "IssuingAbort" (cmdName cmd)

let private decideAwaitingAbort ctx turn abortCtx cmd =
    match cmd with
    | AbortConfirmed tid when tid = activeTurnId turn -> Ok(applyAfterAbort ctx turn abortCtx)
    | AbortConfirmed _ -> Ok(noChange StaleTurnMarker)
    | AbortHostAccepted _
    | AbortRequestFailed _ -> Ok(noChange AbortInProgress)
    | AbortDeadlineExpired tid when tid = activeTurnId turn ->
        let res = Failed(InfrastructureFailure "abort deadline expired")

        Ok(
            decided
                (Poisoned(AbortDidNotSettle tid))
                [ SessionPoisoned(ctx.SessionId, AbortDidNotSettle tid)
                  TurnFinished(tid, TurnInfrastructureFailed "abort deadline expired")
                  RunFinished(ctx.RunId, res) ]
                [ CompleteCaller(ctx.RunId, res) ]
        )
    | AbortDeadlineExpired _
    | CancelRequested
    | TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | DispatchAccepted _
    | DispatchRejected _ -> Ok(noChange StaleTurnMarker)
    | SessionClosed -> Ok(closeActive ctx (activeTurnId turn))
    | _ -> illegal "AwaitingAbortSettle" (cmdName cmd)

let private decideReconcilingAbort ctx turn abortCtx cmd =
    match cmd with
    | SessionQuiescenceResolved status ->
        let tid = activeTurnId turn

        match status with
        | Stopped -> Ok(applyAfterAbort ctx turn abortCtx)
        | StillRunning -> Ok(decided (AwaitingAbortSettle(ctx, turn, abortCtx)) [] [])
        | StopUnknown ->
            let res = Failed(InfrastructureFailure "abort did not settle")

            Ok(
                decided
                    (Poisoned(AbortDidNotSettle tid))
                    [ SessionPoisoned(ctx.SessionId, AbortDidNotSettle tid)
                      TurnFinished(tid, TurnInfrastructureFailed "abort did not settle")
                      RunFinished(ctx.RunId, res) ]
                    [ CompleteCaller(ctx.RunId, res) ]
            )
    | AbortConfirmed tid when tid = activeTurnId turn -> Ok(applyAfterAbort ctx turn abortCtx)
    | AbortConfirmed _ -> Ok(noChange StaleTurnMarker)
    | SessionClosed -> Ok(closeActive ctx (activeTurnId turn))
    | AbortDeadlineExpired tid when tid = activeTurnId turn ->
        let res = Failed(InfrastructureFailure "abort deadline expired")

        Ok(
            decided
                (Poisoned(AbortDidNotSettle tid))
                [ SessionPoisoned(ctx.SessionId, AbortDidNotSettle tid)
                  TurnFinished(tid, TurnInfrastructureFailed "abort deadline expired")
                  RunFinished(ctx.RunId, res) ]
                [ CompleteCaller(ctx.RunId, res) ]
        )
    | AbortDeadlineExpired _
    | CancelRequested
    | TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | DispatchAccepted _
    | DispatchRejected _ -> Ok(noChange StaleTurnMarker)
    | AbortHostAccepted _
    | AbortRequestFailed _ -> Ok(noChange AbortInProgress)
    | _ -> illegal "ReconcilingAbortSettle" (cmdName cmd)

let private decideAbort state cmd =
    match state with
    | IssuingAbort(ctx, turn, abortCtx, idleObserved) -> decideIssuingAbort ctx turn abortCtx idleObserved cmd
    | AwaitingAbortSettle(ctx, turn, abortCtx) -> decideAwaitingAbort ctx turn abortCtx cmd
    | ReconcilingAbortSettle(ctx, turn, abortCtx) -> decideReconcilingAbort ctx turn abortCtx cmd
    | _ -> illegal (stateName state) (cmdName cmd)

let decide (state: SubsessionState) (cmd: Command) : Result<DecisionResult, DecisionError> =
    match state with
    | Poisoned _ ->
        match cmd with
        | SessionClosed -> Ok(decided state [] [ DisposeActor ])
        | _ -> Ok(noChange StaleTimer)
    | Available _ ->
        match cmd with
        | CancelRequested -> Ok(noChange StaleTimer)
        | SessionClosed -> Ok(decided state [] [ DisposeActor ])
        | cmd when isStaleTimerCommand cmd -> Ok(noChange StaleTimer)
        | _ -> illegal (stateName state) (cmdName cmd)
    | Dispatching _
    | CancellingDispatch _ -> decideDispatch state cmd
    | ReconcilingUnknownDispatch _
    | ClosingUnknownDispatch _ -> decideReconcile state cmd
    | Running _
    | Draining _ -> decideRunningDraining state cmd
    | IssuingAbort _
    | AwaitingAbortSettle _
    | ReconcilingAbortSettle _ -> decideAbort state cmd
