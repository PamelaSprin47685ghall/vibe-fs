module Wanxiangshu.Kernel.Subsession.Decision

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.TranscriptDecision
open Wanxiangshu.Kernel.Subsession.Policy

// ── Helpers ──

let private makeTurnData (ctx: RunContext) (plan: TurnPlan) : TurnData =
    { RunId = ctx.RunId
      TurnId = plan.TurnId
      Ordinal = plan.Ordinal
      Model = plan.Model
      Prompt = plan.Prompt }

let private nextTurnFromPolicy (ctx: RunContext) (decision: PolicyDecision) : (RunContext * TurnPlan) option =
    match decision with
    | NextTurn(policy2, model, prompt) ->
        let ordinal = ctx.NextTurnOrdinal

        let turnId =
            TurnId.create (RunId.value ctx.RunId + "-t" + string (TurnOrdinal.value ordinal))

        let plan =
            { TurnId = turnId
              Ordinal = ordinal
              Model = model
              Prompt = prompt }

        let ctx2 =
            { ctx with
                Policy = policy2
                NextTurnOrdinal = TurnOrdinal.next ordinal }

        Some(ctx2, plan)
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

let private failRun
    (ctx: RunContext)
    (failure: RunFailure)
    (cancelTurn: TurnId option)
    (extraEvents: SubsessionEvent list)
    =
    let avail = Available { SessionId = ctx.SessionId }
    let result = Failed failure

    let events = extraEvents @ [ RunFinished(ctx.RunId, result) ]

    let effects =
        [ match cancelTurn with
          | Some tid -> CancelTurnDeadline tid
          | None -> ()
          CompleteCaller(ctx.RunId, result) ]

    decided avail events effects

let private succeedRun (ctx: RunContext) (output: string) (turnId: TurnId) =
    let avail = Available { SessionId = ctx.SessionId }
    let result = Succeeded output

    let events =
        [ TurnFinished(turnId, TurnCompleted output); RunFinished(ctx.RunId, result) ]

    let effects = [ CancelTurnDeadline turnId; CompleteCaller(ctx.RunId, result) ]

    decided avail events effects

let private finishWithResult (ctx: RunContext) (result: RunResult) (turnId: TurnId) (cancelAbort: bool) =
    let avail = Available { SessionId = ctx.SessionId }

    let finishEvent =
        match result with
        | Succeeded output -> TurnFinished(turnId, TurnCompleted output)
        | Failed(FallbackExhausted err) -> TurnFinished(turnId, TurnFailed err)
        | Failed(InfrastructureFailure reason) -> TurnFinished(turnId, TurnInfrastructureFailed reason)
        | Failed(ProtocolViolation reason) -> TurnFinished(turnId, TurnInfrastructureFailed reason)
        | Failed(RecoveryExhausted reason) -> TurnFinished(turnId, TurnInfrastructureFailed reason)
        | Failed NoModelConfigured -> TurnFinished(turnId, TurnInfrastructureFailed "no model configured")
        | Cancelled -> TurnFinished(turnId, TurnCancelled)

    let events = [ finishEvent; RunFinished(ctx.RunId, result) ]

    let effects =
        [ if cancelAbort then
              CancelAbortDeadline turnId
          CancelTurnDeadline turnId
          CompleteCaller(ctx.RunId, result) ]

    decided avail events effects

let private beginAbort (ctx: RunContext) (turn: ActiveTurn) (reason: AbortReason) (afterStop: AfterAbort) =
    let tid = activeTurnId turn

    let abortCtx =
        { Reason = reason
          AfterStop = afterStop }

    let events = [ AbortRequested(ctx.RunId, tid) ]

    let effects =
        [ AbortHostSession(ctx.SessionId, tid)
          CancelTurnDeadline tid
          CancelPendingDispatch tid
          ArmAbortDeadline tid ]

    // Host abort not yet accepted — idle must not settle yet.
    decided (IssuingAbort(ctx, turn, abortCtx)) events effects

/// Physical session is gone: force-complete, do not wait for idle/abort.
let private closeActive (ctx: RunContext) (turnId: TurnId) =
    let poisoned = Poisoned SessionClosedUnexpectedly
    let result = Failed(InfrastructureFailure "session closed")

    let events =
        [ SessionPoisoned(ctx.SessionId, SessionClosedUnexpectedly)
          TurnFinished(turnId, TurnInfrastructureFailed "session closed")
          RunFinished(ctx.RunId, result) ]

    let effects =
        [ CancelTurnDeadline turnId
          CancelAbortDeadline turnId
          CancelPendingDispatch turnId
          CompleteCaller(ctx.RunId, result)
          DisposeActor ]

    decided poisoned events effects

/// After host is confirmed stopped, apply AfterAbort.
let private applyAfterAbort (ctx: RunContext) (turn: ActiveTurn) (abortCtx: AbortContext) (cancelAbort: bool) =
    let tid = activeTurnId turn

    match abortCtx.AfterStop with
    | FinishCancelled -> finishWithResult ctx Cancelled tid cancelAbort
    | FinishFailed failure -> finishWithResult ctx (Failed failure) tid cancelAbort
    | RetryAfterSafeStop error ->
        let policyDec = afterError ctx.FallbackConfig ctx.Chain ctx.Policy error

        match nextTurnFromPolicy ctx policyDec with
        | Some(ctx2, plan2) ->
            let events =
                [ TurnFinished(tid, TurnFailed error)
                  TurnDispatchRequested(makeTurnData ctx2 plan2) ]

            let effects =
                [ if cancelAbort then
                      CancelAbortDeadline tid
                  CancelTurnDeadline tid
                  ArmTurnDeadline plan2.TurnId
                  DispatchPrompt plan2 ]

            decided (Dispatching(ctx2, plan2)) events effects
        | None ->
            let failure' =
                match policyDec with
                | StopWithFailure f -> f
                | _ -> FallbackExhausted error

            finishWithResult ctx (Failed failure') tid cancelAbort

let private isActiveAbortState =
    function
    | IssuingAbort _
    | AwaitingAbortSettle _
    | ReconcilingAbortSettle _
    | CancellingDispatch _
    | ReconcilingUnknownDispatch _ -> true
    | _ -> false

// ── Pure reducer ──

let decide (state: SubsessionState) (cmd: Command) : Result<DecisionResult, DecisionError> =
    let stateName () =
        match state with
        | Available _ -> "Available"
        | Dispatching _ -> "Dispatching"
        | CancellingDispatch _ -> "CancellingDispatch"
        | ReconcilingUnknownDispatch _ -> "ReconcilingUnknownDispatch"
        | Running _ -> "Running(evidence)"
        | Draining _ -> "Draining"
        | IssuingAbort _ -> "IssuingAbort"
        | AwaitingAbortSettle _ -> "AwaitingAbortSettle"
        | ReconcilingAbortSettle _ -> "ReconcilingAbortSettle"
        | Poisoned _ -> "Poisoned"

    let cmdName () =
        match cmd with
        | StartRun _ -> "StartRun"
        | DispatchAccepted _ -> "DispatchAccepted"
        | DispatchRejected _ -> "DispatchRejected"
        | TurnErrorObserved _ -> "TurnErrorObserved"
        | SessionIdleObserved -> "SessionIdleObserved"
        | EvidenceUpdated _ -> "EvidenceUpdated"
        | DispatchStatusResolved _ -> "DispatchStatusResolved"
        | CancelRequested -> "CancelRequested"
        | TurnDeadlineExpired _ -> "TurnDeadlineExpired"
        | AbortDeadlineExpired _ -> "AbortDeadlineExpired"
        | AbortConfirmed _ -> "AbortConfirmed"
        | AbortHostAccepted _ -> "AbortHostAccepted"
        | AbortRequestFailed _ -> "AbortRequestFailed"
        | SessionClosed -> "SessionClosed"

    match state, cmd with
    // ── Poisoned ──
    | Poisoned reason, StartRun _ -> Ok(decided state [] [ RejectStart(StartRunError.SessionPoisoned reason) ])

    | Poisoned _, SessionClosed -> Ok(decided state [] [ DisposeActor ])

    | Poisoned _, _ -> Ok(noChange StaleTimer)

    // ── StartRun concurrency ──
    | (Dispatching _ | Running _ | Draining _ | IssuingAbort _ | AwaitingAbortSettle _ | ReconcilingAbortSettle _ | ReconcilingUnknownDispatch _),
      StartRun _ -> Ok(decided state [] [ RejectStart AlreadyRunning ])

    // ── Available + StartRun ──
    | Available avail, StartRun req when req.InitiallyCancelled ->
        // Paired start/finish so log has no orphan terminal.
        let events =
            [ RunStarted
                  { RunId = req.RunId
                    ParentSessionId = req.ParentSessionId
                    SessionId = req.SessionId }
              RunFinished(req.RunId, Cancelled) ]

        let effects = [ CompleteCaller(req.RunId, Cancelled) ]
        Ok(decided (Available avail) events effects)

    | Available _, StartRun req ->
        match req.Chain with
        | [] -> Ok(decided state [] [ RejectStart NoModelAvailable ])
        | firstModel :: _ ->
            let policy = initialPolicy req.FallbackConfig req.Chain

            let ctx =
                { RunId = req.RunId
                  ParentSessionId = req.ParentSessionId
                  SessionId = req.SessionId
                  Policy = policy
                  FallbackConfig = req.FallbackConfig
                  Chain = req.Chain
                  NextTurnOrdinal = TurnOrdinal.next TurnOrdinal.first }

            let plan =
                { TurnId = TurnId.create (RunId.value req.RunId + "-t0")
                  Ordinal = TurnOrdinal.first
                  Model = firstModel
                  Prompt = req.Prompt }

            let events =
                [ RunStarted
                      { RunId = req.RunId
                        ParentSessionId = req.ParentSessionId
                        SessionId = req.SessionId }
                  TurnDispatchRequested(makeTurnData ctx plan) ]

            let effects = [ ArmTurnDeadline plan.TurnId; DispatchPrompt plan ]

            Ok(decided (Dispatching(ctx, plan)) events effects)

    | Available _, CancelRequested -> Ok(noChange StaleTimer)

    | Available _, SessionClosed -> Ok(decided state [] [ DisposeActor ])

    | Available _,
      (TurnDeadlineExpired _ | AbortDeadlineExpired _ | AbortConfirmed _ | AbortHostAccepted _ | AbortRequestFailed _ | EvidenceUpdated _) ->
        Ok(noChange StaleTimer)

    | Available _, _ -> illegal (stateName ()) (cmdName ())

    // ── Dispatching ──
    | Dispatching(ctx, plan), DispatchAccepted(tid, receipt) when tid = plan.TurnId ->
        let started = { Plan = plan; StartReceipt = receipt }

        let events =
            [ TurnStarted
                  { RunId = ctx.RunId
                    TurnId = tid
                    Receipt = receipt } ]

        Ok(decided (Running(ctx, started, CurrentTurnEvidence.empty)) events [])

    | Dispatching _, DispatchAccepted _ -> Ok(noChange StaleTurnMarker)

    | Dispatching(ctx, plan), DispatchRejected(tid, failure) when tid = plan.TurnId ->
        match failure with
        | HostRejected error ->
            let policyDec = afterError ctx.FallbackConfig ctx.Chain ctx.Policy error

            match nextTurnFromPolicy ctx policyDec with
            | Some(ctx2, plan2) ->
                let events = [ TurnDispatchRequested(makeTurnData ctx2 plan2) ]

                let effects =
                    [ CancelTurnDeadline plan.TurnId
                      CancelPendingDispatch plan.TurnId
                      ArmTurnDeadline plan2.TurnId
                      DispatchPrompt plan2 ]

                Ok(decided (Dispatching(ctx2, plan2)) events effects)
            | None ->
                let failure' =
                    match policyDec with
                    | StopWithFailure f -> f
                    | _ -> FallbackExhausted error

                Ok(failRun ctx failure' (Some plan.TurnId) [ TurnFinished(plan.TurnId, TurnFailed error) ])

        | HostAcceptanceUnknown error ->
            let cancelCtx =
                { Reason = AcceptanceUnknownAfterDispatch
                  AfterStop = RetryAfterSafeStop error }

            let effects = [ QueryDispatchStatus(ctx.SessionId, plan.TurnId) ]
            Ok(decided (ReconcilingUnknownDispatch(ctx, plan, cancelCtx)) [] effects)

    | Dispatching _, DispatchRejected _ -> Ok(noChange StaleTurnMarker)

    | Dispatching _, TurnErrorObserved _ -> Ok(noChange UnattributedObservationBeforeStart)

    | Dispatching _, EvidenceUpdated _ -> Ok(noChange EvidenceBeforeRun)

    | Dispatching _, SessionIdleObserved -> Ok(noChange DuplicateIdleBeforeTurnMarker)

    | Dispatching(ctx, plan), CancelRequested ->
        let cancelCtx: CancelContext =
            { Reason = UserRequested
              AfterStop = FinishCancelled }

        let effects = [ CancelPendingDispatch plan.TurnId ]
        Ok(decided (CancellingDispatch(ctx, plan, cancelCtx)) [] effects)

    | Dispatching(ctx, plan), TurnDeadlineExpired tid when tid = plan.TurnId ->
        let cancelCtx: CancelContext =
            { Reason = TurnDeadline
              AfterStop = FinishFailed(InfrastructureFailure "turn deadline expired before host accepted") }

        let effects = [ CancelPendingDispatch plan.TurnId; CancelTurnDeadline plan.TurnId ]
        Ok(decided (CancellingDispatch(ctx, plan, cancelCtx)) [] effects)

    | Dispatching _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)

    | Dispatching(ctx, plan), SessionClosed -> Ok(closeActive ctx plan.TurnId)

    | Dispatching _,
      (AbortDeadlineExpired _ | DispatchStatusResolved _ | AbortConfirmed _ | AbortHostAccepted _ | AbortRequestFailed _) ->
        illegal (stateName ()) (cmdName ())

    // ── CancellingDispatch: waiting for dispatch result after cancel requested ──
    | CancellingDispatch(ctx, plan, cancelCtx), DispatchAccepted(tid, receipt) when tid = plan.TurnId ->
        // Host confirmed it received the prompt → turn IS running → must abort.
        let started = { Plan = plan; StartReceipt = receipt }

        let abortCtx =
            { Reason = cancelCtx.Reason
              AfterStop = cancelCtx.AfterStop }

        let events =
            [ TurnStarted
                  { RunId = ctx.RunId
                    TurnId = tid
                    Receipt = receipt }
              AbortRequested(ctx.RunId, tid) ]

        let effects = [ AbortHostSession(ctx.SessionId, tid); ArmAbortDeadline tid ]

        Ok(decided (IssuingAbort(ctx, Started started, abortCtx)) events effects)

    | CancellingDispatch _, DispatchAccepted _ -> Ok(noChange StaleTurnMarker)

    | CancellingDispatch(ctx, plan, cancelCtx), DispatchRejected(tid, failure) when tid = plan.TurnId ->
        match failure with
        | HostRejected _ ->
            // Host definitely did NOT receive prompt → safe to cancel/fail without abort.
            let result =
                match cancelCtx.AfterStop with
                | FinishCancelled -> Cancelled
                | FinishFailed f -> Failed f
                | RetryAfterSafeStop _ -> Cancelled

            let avail = Available { SessionId = ctx.SessionId }

            let events =
                [ TurnFinished(plan.TurnId, TurnCancelled); RunFinished(ctx.RunId, result) ]

            let effects = [ CompleteCaller(ctx.RunId, result) ]
            Ok(decided avail events effects)

        | HostAcceptanceUnknown _ ->
            // Cannot confirm host didn't receive → query host to determine.
            let effects = [ QueryDispatchStatus(ctx.SessionId, plan.TurnId) ]
            Ok(decided (ReconcilingUnknownDispatch(ctx, plan, cancelCtx)) [] effects)

    | CancellingDispatch _, DispatchRejected _ -> Ok(noChange StaleTurnMarker)

    | CancellingDispatch _, SessionIdleObserved -> Ok(noChange DuplicateIdleBeforeTurnMarker)
    | CancellingDispatch _, TurnErrorObserved _ -> Ok(noChange UnattributedObservationBeforeStart)
    | CancellingDispatch _, EvidenceUpdated _ -> Ok(noChange EvidenceBeforeRun)
    | CancellingDispatch _, CancelRequested -> Ok(noChange StaleTimer)
    | CancellingDispatch(ctx, plan, cancelCtx), TurnDeadlineExpired tid when tid = plan.TurnId ->
        // Dispatch result not received within deadline → cannot confirm acceptance.
        // Enter ReconcilingUnknownDispatch to query or poison.
        let effects = [ QueryDispatchStatus(ctx.SessionId, plan.TurnId) ]
        Ok(decided (ReconcilingUnknownDispatch(ctx, plan, cancelCtx)) [] effects)
    | CancellingDispatch _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | CancellingDispatch _, AbortDeadlineExpired _ -> Ok(noChange StaleTimer)

    | (CancellingDispatch _), StartRun _ -> Ok(decided state [] [ RejectStart AlreadyRunning ])

    | CancellingDispatch(ctx, plan, _), SessionClosed -> Ok(closeActive ctx plan.TurnId)

    | CancellingDispatch _, (DispatchStatusResolved _ | AbortConfirmed _ | AbortHostAccepted _ | AbortRequestFailed _) ->
        illegal (stateName ()) (cmdName ())

    // ── ReconcilingUnknownDispatch ──
    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx), DispatchStatusResolved status ->
        match status with
        | DispatchStatus.Accepted receipt ->
            let started = { Plan = plan; StartReceipt = receipt }

            let abortCtx =
                { Reason = cancelCtx.Reason
                  AfterStop = cancelCtx.AfterStop }

            let events =
                [ TurnStarted
                      { RunId = ctx.RunId
                        TurnId = plan.TurnId
                        Receipt = receipt }
                  AbortRequested(ctx.RunId, plan.TurnId) ]

            let effects =
                [ AbortHostSession(ctx.SessionId, plan.TurnId); ArmAbortDeadline plan.TurnId ]

            Ok(decided (IssuingAbort(ctx, Started started, abortCtx)) events effects)
        | DispatchStatus.DefinitelyNotAccepted -> Ok(applyAfterAbort ctx (NotYetStarted plan) cancelCtx false)
        | DispatchStatus.StillPending -> Ok(noChange AbortInProgress)
        | DispatchStatus.Unknown ->
            let poisonReason = HostProtocolBroken "acceptance unknown and unresolvable"
            let poisoned = Poisoned poisonReason

            let result =
                Failed(InfrastructureFailure "dispatch acceptance unknown after reconciliation")

            let events =
                [ SessionPoisoned(ctx.SessionId, poisonReason)
                  TurnFinished(plan.TurnId, TurnInfrastructureFailed "acceptance unknown")
                  RunFinished(ctx.RunId, result) ]

            let effects = [ CompleteCaller(ctx.RunId, result) ]
            Ok(decided poisoned events effects)

    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx), DispatchAccepted(tid, receipt) when tid = plan.TurnId ->
        // Late acceptance confirms host received → must abort.
        let started = { Plan = plan; StartReceipt = receipt }

        let abortCtx =
            { Reason = cancelCtx.Reason
              AfterStop = cancelCtx.AfterStop }

        let events =
            [ TurnStarted
                  { RunId = ctx.RunId
                    TurnId = tid
                    Receipt = receipt }
              AbortRequested(ctx.RunId, tid) ]

        let effects = [ AbortHostSession(ctx.SessionId, tid); ArmAbortDeadline tid ]

        Ok(decided (IssuingAbort(ctx, Started started, abortCtx)) events effects)

    | ReconcilingUnknownDispatch _, DispatchAccepted _ -> Ok(noChange StaleTurnMarker)

    | ReconcilingUnknownDispatch(ctx, plan, cancelCtx), DispatchRejected(tid, HostRejected _) when tid = plan.TurnId ->
        // Definite rejection → safe cancel.
        let result =
            match cancelCtx.AfterStop with
            | FinishCancelled -> Cancelled
            | FinishFailed f -> Failed f
            | RetryAfterSafeStop _ -> Cancelled

        let avail = Available { SessionId = ctx.SessionId }

        let events =
            [ TurnFinished(plan.TurnId, TurnCancelled); RunFinished(ctx.RunId, result) ]

        let effects = [ CompleteCaller(ctx.RunId, result) ]
        Ok(decided avail events effects)

    | ReconcilingUnknownDispatch _, DispatchRejected _ -> Ok(noChange StaleTurnMarker)

    | ReconcilingUnknownDispatch(ctx, plan, _), SessionClosed -> Ok(closeActive ctx plan.TurnId)

    | ReconcilingUnknownDispatch _, CancelRequested -> Ok(noChange StaleTimer)
    | ReconcilingUnknownDispatch _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | ReconcilingUnknownDispatch _, AbortDeadlineExpired _ -> Ok(noChange StaleTimer)
    | ReconcilingUnknownDispatch _, SessionIdleObserved -> Ok(noChange StaleTimer)
    | ReconcilingUnknownDispatch _, TurnErrorObserved _ -> Ok(noChange StaleTimer)
    | ReconcilingUnknownDispatch _, EvidenceUpdated _ -> Ok(noChange EvidenceBeforeRun)

    | ReconcilingUnknownDispatch _, (AbortConfirmed _ | AbortHostAccepted _ | AbortRequestFailed _) ->
        illegal (stateName ()) (cmdName ())

    // ── Running ──
    | Running(ctx, started, _), TurnErrorObserved error ->
        // Hold the error — do not act until idle proves the host has stopped.
        Ok(decided (Draining(ctx, started, error)) [] [])

    | Running(ctx, started, evidence), SessionIdleObserved ->
        let transcriptDec = classifyTurnEvidence evidence

        match transcriptDec with
        | CompleteNaturally output ->
            let ctx2 =
                { ctx with
                    Policy = afterSuccessfulTurn ctx.Policy }

            Ok(succeedRun ctx2 output started.Plan.TurnId)

        | _ ->
            let policyDec =
                afterTranscript ctx.FallbackConfig ctx.Chain ctx.Policy transcriptDec

            match nextTurnFromPolicy ctx policyDec with
            | Some(ctx2, plan2) ->
                let events =
                    [ TurnFinished(started.Plan.TurnId, TurnRecovering)
                      TurnDispatchRequested(makeTurnData ctx2 plan2) ]

                let effects =
                    [ CancelTurnDeadline started.Plan.TurnId
                      ArmTurnDeadline plan2.TurnId
                      DispatchPrompt plan2 ]

                Ok(decided (Dispatching(ctx2, plan2)) events effects)
            | None ->
                let failure' =
                    match policyDec with
                    | StopWithFailure f -> f
                    | _ -> RecoveryExhausted "Session idle without task completion"

                Ok(
                    failRun
                        ctx
                        failure'
                        (Some started.Plan.TurnId)
                        [ TurnFinished(
                              started.Plan.TurnId,
                              TurnInfrastructureFailed "session idle without task completion"
                          ) ]
                )

    | Running(ctx, started, evidence), EvidenceUpdated obs ->
        if TurnId.value obs.TurnId = "" || obs.TurnId = started.Plan.TurnId then
            let merged = CurrentTurnEvidence.merge evidence obs.Evidence
            Ok(decided (Running(ctx, started, merged)) [] [])
        else
            Ok(noChange StaleTurnMarker)

    | Running(ctx, started, _), CancelRequested -> Ok(beginAbort ctx (Started started) UserRequested FinishCancelled)

    | Running(ctx, started, _), TurnDeadlineExpired tid when tid = started.Plan.TurnId ->
        Ok(beginAbort ctx (Started started) TurnDeadline (FinishFailed(InfrastructureFailure "turn deadline expired")))

    | Running _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)

    | Running _, (DispatchAccepted _ | DispatchRejected _) -> Ok(noChange StaleTurnMarker)

    | Running(ctx, started, _), SessionClosed -> Ok(closeActive ctx started.Plan.TurnId)

    | Running _,
      (DispatchStatusResolved _ | AbortDeadlineExpired _ | AbortConfirmed _ | AbortHostAccepted _ | AbortRequestFailed _) ->
        illegal (stateName ()) (cmdName ())

    // ── Draining: host reported an error but has not gone idle yet ──
    | Draining _, TurnErrorObserved _ -> Ok(noChange DuplicateError)

    | Draining(ctx, started, error), SessionIdleObserved ->
        let policyDec = afterError ctx.FallbackConfig ctx.Chain ctx.Policy error

        match nextTurnFromPolicy ctx policyDec with
        | Some(ctx2, plan2) ->
            let events =
                [ TurnFinished(started.Plan.TurnId, TurnFailed error)
                  TurnDispatchRequested(makeTurnData ctx2 plan2) ]

            let effects =
                [ CancelTurnDeadline started.Plan.TurnId
                  ArmTurnDeadline plan2.TurnId
                  DispatchPrompt plan2 ]

            Ok(decided (Dispatching(ctx2, plan2)) events effects)
        | None ->
            let failure' =
                match policyDec with
                | StopWithFailure f -> f
                | _ -> FallbackExhausted error

            Ok(failRun ctx failure' (Some started.Plan.TurnId) [ TurnFinished(started.Plan.TurnId, TurnFailed error) ])

    | Draining(ctx, started, _), CancelRequested -> Ok(beginAbort ctx (Started started) UserRequested FinishCancelled)

    | Draining(ctx, started, _), TurnDeadlineExpired tid when tid = started.Plan.TurnId ->
        Ok(
            beginAbort
                ctx
                (Started started)
                TurnDeadline
                (FinishFailed(InfrastructureFailure "turn deadline expired while draining"))
        )

    | Draining _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)

    | Draining _, (DispatchAccepted _ | DispatchRejected _) -> Ok(noChange StaleTurnMarker)

    | Draining _, EvidenceUpdated _ -> Ok(noChange StaleTimer)

    | Draining _,
      (DispatchStatusResolved _ | AbortDeadlineExpired _ | AbortConfirmed _ | AbortHostAccepted _ | AbortRequestFailed _) ->
        illegal (stateName ()) (cmdName ())

    | Draining(ctx, started, _), SessionClosed -> Ok(closeActive ctx started.Plan.TurnId)

    // ── IssuingAbort: host abort not yet accepted; idle must NOT settle ──
    | IssuingAbort(ctx, turn, abortCtx), AbortConfirmed tid when tid = activeTurnId turn ->
        Ok(applyAfterAbort ctx turn abortCtx true)

    | IssuingAbort _, AbortConfirmed _ -> Ok(noChange StaleTurnMarker)

    | IssuingAbort(ctx, turn, abortCtx), AbortHostAccepted tid when tid = activeTurnId turn ->
        Ok(decided (AwaitingAbortSettle(ctx, turn, abortCtx)) [] [])

    | IssuingAbort _, AbortHostAccepted _ -> Ok(noChange StaleTurnMarker)

    | IssuingAbort _, AbortRequestFailed _ ->
        // Stay IssuingAbort; wait for deadline / SessionClosed. Never treat as stopped.
        Ok(noChange AbortInProgress)

    | IssuingAbort _, SessionIdleObserved -> Ok(noChange IdleBeforeAbortBarrier)

    | IssuingAbort(ctx, turn, abortCtx), DispatchAccepted(tid, receipt) when tid = activeTurnId turn ->
        // Late acceptance: host confirmed it received the prompt. Upgrade ActiveTurn.
        match turn with
        | NotYetStarted plan ->
            let started = { Plan = plan; StartReceipt = receipt }

            let events =
                [ TurnStarted
                      { RunId = ctx.RunId
                        TurnId = tid
                        Receipt = receipt } ]

            Ok(decided (IssuingAbort(ctx, Started started, abortCtx)) events [])
        | Started _ ->
            // Already started — duplicate acceptance, ignore.
            Ok(noChange StaleTurnMarker)

    | IssuingAbort _, DispatchAccepted _ -> Ok(noChange StaleTurnMarker)

    | IssuingAbort _, DispatchRejected _ -> Ok(noChange StaleTurnMarker)

    | IssuingAbort(ctx, turn, _), AbortDeadlineExpired tid when tid = activeTurnId turn ->
        let poisoned = Poisoned(AbortDidNotSettle tid)
        let result = Failed(InfrastructureFailure "abort deadline expired")

        let effects = [ CompleteCaller(ctx.RunId, result) ]

        let events =
            [ SessionPoisoned(ctx.SessionId, AbortDidNotSettle tid)
              TurnFinished(tid, TurnInfrastructureFailed "abort deadline expired")
              RunFinished(ctx.RunId, result) ]

        Ok(decided poisoned events effects)

    | IssuingAbort _, AbortDeadlineExpired _ -> Ok(noChange StaleTimer)

    | IssuingAbort _, CancelRequested -> Ok(noChange StaleTimer)

    | IssuingAbort _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)

    | IssuingAbort _, (TurnErrorObserved _ | EvidenceUpdated _) -> Ok(noChange StaleTimer)

    | IssuingAbort(ctx, turn, _), SessionClosed -> Ok(closeActive ctx (activeTurnId turn))

    | IssuingAbort _, (DispatchStatusResolved _) -> illegal (stateName ()) (cmdName ())

    // ── AwaitingAbortSettle: barrier accepted; idle proves stop ──
    | AwaitingAbortSettle(ctx, turn, abortCtx), AbortConfirmed tid when tid = activeTurnId turn ->
        Ok(applyAfterAbort ctx turn abortCtx true)

    | AwaitingAbortSettle _, AbortConfirmed _ -> Ok(noChange StaleTurnMarker)

    | AwaitingAbortSettle(ctx, turn, abortCtx), SessionIdleObserved ->
        let tid = activeTurnId turn
        let effects = [ QueryDispatchStatus(ctx.SessionId, tid) ]
        Ok(decided (ReconcilingAbortSettle(ctx, turn, abortCtx)) [] effects)

    | AwaitingAbortSettle _, AbortHostAccepted _ -> Ok(noChange AbortInProgress)

    | AwaitingAbortSettle _, AbortRequestFailed _ -> Ok(noChange AbortInProgress)

    | AwaitingAbortSettle(ctx, turn, _), AbortDeadlineExpired tid when tid = activeTurnId turn ->
        let poisoned = Poisoned(AbortDidNotSettle tid)
        let result = Failed(InfrastructureFailure "abort deadline expired")

        let effects = [ CompleteCaller(ctx.RunId, result) ]

        let events =
            [ SessionPoisoned(ctx.SessionId, AbortDidNotSettle tid)
              TurnFinished(tid, TurnInfrastructureFailed "abort deadline expired")
              RunFinished(ctx.RunId, result) ]

        Ok(decided poisoned events effects)

    | AwaitingAbortSettle _, AbortDeadlineExpired _ -> Ok(noChange StaleTimer)

    | AwaitingAbortSettle _, CancelRequested -> Ok(noChange StaleTimer)

    | AwaitingAbortSettle _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)

    | AwaitingAbortSettle _, (TurnErrorObserved _ | EvidenceUpdated _) -> Ok(noChange StaleTimer)

    | AwaitingAbortSettle _, (DispatchAccepted _ | DispatchRejected _) -> Ok(noChange StaleTurnMarker)

    | AwaitingAbortSettle(ctx, turn, _), SessionClosed -> Ok(closeActive ctx (activeTurnId turn))

    | AwaitingAbortSettle _, (DispatchStatusResolved _) -> illegal (stateName ()) (cmdName ())

    // ── ReconcilingAbortSettle: reconcile status after idle ──
    | ReconcilingAbortSettle(ctx, turn, abortCtx), DispatchStatusResolved status ->
        let tid = activeTurnId turn

        match status with
        | DispatchStatus.Accepted _
        | DispatchStatus.DefinitelyNotAccepted -> Ok(applyAfterAbort ctx turn abortCtx true)
        | DispatchStatus.StillPending -> Ok(decided (AwaitingAbortSettle(ctx, turn, abortCtx)) [] [])
        | DispatchStatus.Unknown ->
            let poisoned = Poisoned(AbortDidNotSettle tid)
            let result = Failed(InfrastructureFailure "abort did not settle")

            let events =
                [ SessionPoisoned(ctx.SessionId, AbortDidNotSettle tid)
                  TurnFinished(tid, TurnInfrastructureFailed "abort did not settle")
                  RunFinished(ctx.RunId, result) ]

            let effects = [ CompleteCaller(ctx.RunId, result) ]
            Ok(decided poisoned events effects)

    | ReconcilingAbortSettle(ctx, turn, abortCtx), AbortConfirmed tid when tid = activeTurnId turn ->
        Ok(applyAfterAbort ctx turn abortCtx true)

    | ReconcilingAbortSettle _, AbortConfirmed _ -> Ok(noChange StaleTurnMarker)

    | ReconcilingAbortSettle(ctx, turn, _), SessionClosed -> Ok(closeActive ctx (activeTurnId turn))

    | ReconcilingAbortSettle(ctx, turn, _), AbortDeadlineExpired tid when tid = activeTurnId turn ->
        let poisoned = Poisoned(AbortDidNotSettle tid)
        let result = Failed(InfrastructureFailure "abort deadline expired")

        let effects = [ CompleteCaller(ctx.RunId, result) ]

        let events =
            [ SessionPoisoned(ctx.SessionId, AbortDidNotSettle tid)
              TurnFinished(tid, TurnInfrastructureFailed "abort deadline expired")
              RunFinished(ctx.RunId, result) ]

        Ok(decided poisoned events effects)

    | ReconcilingAbortSettle _, AbortDeadlineExpired _ -> Ok(noChange StaleTimer)

    | ReconcilingAbortSettle _, CancelRequested -> Ok(noChange StaleTimer)

    | ReconcilingAbortSettle _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)

    | ReconcilingAbortSettle _, SessionIdleObserved -> Ok(noChange AbortInProgress)

    | ReconcilingAbortSettle _, (TurnErrorObserved _ | EvidenceUpdated _) -> Ok(noChange StaleTimer)

    | ReconcilingAbortSettle _, (DispatchAccepted _ | DispatchRejected _) -> Ok(noChange StaleTurnMarker)

    | ReconcilingAbortSettle _, (AbortHostAccepted _ | AbortRequestFailed _) -> Ok(noChange AbortInProgress)

// ── Reconcile: produce poison decision for unfinished state after restart ──

let private tryExtractActiveForReconcile (s: SubsessionState) : (RunContext * ActiveTurn) option =
    match s with
    | Dispatching(ctx, plan) -> Some(ctx, NotYetStarted plan)
    | CancellingDispatch(ctx, plan, _) -> Some(ctx, NotYetStarted plan)
    | ReconcilingUnknownDispatch(ctx, plan, _) -> Some(ctx, NotYetStarted plan)
    | Running(ctx, started, _) -> Some(ctx, Started started)
    | Draining(ctx, started, _) -> Some(ctx, Started started)
    | IssuingAbort(ctx, turn, _) -> Some(ctx, turn)
    | AwaitingAbortSettle(ctx, turn, _) -> Some(ctx, turn)
    | ReconcilingAbortSettle(ctx, turn, _) -> Some(ctx, turn)
    | Available _
    | Poisoned _ -> None

/// Given an active subsession state discovered on restart, produce the Decision
/// that must be persisted to NDJSON so the run is durably closed.
/// Returns None only for Available/Poisoned (nothing to reconcile).
let reconcile (state: SubsessionState) : Decision option =
    match state with
    | Available _
    | Poisoned _ -> None
    | _ ->
        match tryExtractActiveForReconcile state with
        | None -> None
        | Some(ctx, turn) ->
            let tid = activeTurnId turn
            let poisoned = Poisoned SessionStateUnknownAfterRestart
            let result = Failed(InfrastructureFailure "session state unknown after restart")

            let events =
                [ SessionPoisoned(ctx.SessionId, SessionStateUnknownAfterRestart)
                  TurnFinished(tid, TurnInfrastructureFailed "session state unknown after restart")
                  RunFinished(ctx.RunId, result) ]

            let effects = [ CompleteCaller(ctx.RunId, result) ]

            Some
                { NextState = poisoned
                  Events = events
                  Effects = effects }
