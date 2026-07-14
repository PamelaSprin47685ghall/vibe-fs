module Wanxiangshu.Kernel.Subsession.Decision

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.TranscriptDecision
open Wanxiangshu.Kernel.Subsession.Policy

// ── Helpers ──

let private turnIdOf (plan: TurnPlan) : TurnId = plan.TurnId

let private deriveTurnId (ctx: RunContext) : TurnId =
    TurnId.create (RunId.value ctx.RunId + "-t" + string (TurnOrdinal.value ctx.NextTurnOrdinal))

let private advanceCtx (ctx: RunContext) : RunContext =
    { ctx with
        NextTurnOrdinal = TurnOrdinal.next ctx.NextTurnOrdinal }

let private makeTurnData (ctx: RunContext) (plan: TurnPlan) : TurnData =
    { RunId = ctx.RunId
      TurnId = plan.TurnId
      Ordinal = plan.Ordinal
      Model = plan.Model
      Prompt = plan.Prompt }

/// Create the next turn from a policy decision, returning the new context
/// and turn plan.
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

// ── Convenience constructors ──

let private decided state events effects : DecisionResult =
    Decided
        { NextState = state
          Events = events
          Effects = effects }

let private noChange reason : DecisionResult = NoChange reason

let private illegal state cmd : Result<DecisionResult, DecisionError> = Error(IllegalTransition(state, cmd))

// ── Pure reducer ──

/// Core state-machine transition function.
///
/// Every state × command combination is explicit — no wildcard catch-all.
/// Illegal transitions return Error(IllegalTransition).
/// Benign duplicates return NoChange with a named IgnoreReason.
let decide (state: SubsessionState) (cmd: Command) : Result<DecisionResult, DecisionError> =

    let stateName () =
        match state with
        | Available _ -> "Available"
        | Dispatching _ -> "Dispatching"
        | Running _ -> "Running"
        | Draining _ -> "Draining"
        | InspectingTranscript _ -> "InspectingTranscript"
        | Aborting _ -> "Aborting"
        | Poisoned _ -> "Poisoned"

    let cmdName () =
        match cmd with
        | StartRun _ -> "StartRun"
        | DispatchAccepted _ -> "DispatchAccepted"
        | DispatchRejected _ -> "DispatchRejected"
        | TurnErrorObserved _ -> "TurnErrorObserved"
        | TaskCompleteObserved _ -> "TaskCompleteObserved"
        | SessionIdleObserved -> "SessionIdleObserved"
        | TranscriptLoaded _ -> "TranscriptLoaded"
        | CancelRequested -> "CancelRequested"
        | TurnDeadlineExpired _ -> "TurnDeadlineExpired"
        | AbortAcknowledged _ -> "AbortAcknowledged"
        | AbortDeadlineExpired _ -> "AbortDeadlineExpired"
        | SessionClosed -> "SessionClosed"

    // ── 7.12 Poisoned ──
    match state, cmd with
    | Poisoned reason, StartRun _ -> Ok(decided state [] [ RejectStart(StartRunError.SessionPoisoned reason) ])

    | Poisoned _, _ -> Ok(noChange StaleTimer)

    // ── StartRun in non-Available, non-Poisoned ──
    | (Dispatching _ | Running _ | Draining _ | InspectingTranscript _ | Aborting _), StartRun _ ->
        Ok(decided state [] [ RejectStart AlreadyRunning ])

    // ── 7.1 Available + StartRun ──
    | Available avail, StartRun req ->
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

            let effects = [ DispatchPrompt plan; ArmTurnDeadline plan.TurnId ]

            Ok(decided (Dispatching(ctx, plan)) events effects)

    // ── 7.1 Non-Available + StartRun (already handled above) ──

    // ── Available + other commands (illegal except benign ignores) ──
    | Available _, CancelRequested -> Ok(noChange StaleTimer)

    | Available _, SessionClosed -> Ok(noChange StaleTimer)

    | Available _, (TurnDeadlineExpired _ | AbortAcknowledged _ | AbortDeadlineExpired _) -> Ok(noChange StaleTimer)

    | Available _, _ -> illegal (stateName ()) (cmdName ())

    // ── 7.2, 7.3, 7.4 Dispatching ──
    | Dispatching(ctx, plan), DispatchAccepted(tid, receipt) when tid = plan.TurnId ->
        let started = { Plan = plan; StartReceipt = receipt }

        let events =
            [ TurnStarted
                  { RunId = ctx.RunId
                    TurnId = tid
                    Receipt = receipt } ]

        Ok(decided (Running(ctx, started)) events [])

    | Dispatching _, DispatchAccepted _ -> Ok(noChange StaleTurnMarker)

    | Dispatching(ctx, plan), DispatchRejected(tid, error) when tid = plan.TurnId ->
        let policyDec = afterError ctx.FallbackConfig ctx.Chain ctx.Policy error

        match nextTurnFromPolicy ctx policyDec with
        | Some(ctx2, plan2) ->
            let events = [ TurnDispatchRequested(makeTurnData ctx2 plan2) ]

            let effects =
                [ CancelTurnDeadline plan.TurnId
                  DispatchPrompt plan2
                  ArmTurnDeadline plan2.TurnId ]

            Ok(decided (Dispatching(ctx2, plan2)) events effects)
        | None ->
            let failure =
                match policyDec with
                | StopWithFailure f -> f
                | _ -> FallbackExhausted error

            let avail = Available { SessionId = ctx.SessionId }

            let events = [ RunFinished(ctx.RunId, Failed failure) ]

            let effects =
                [ CancelTurnDeadline plan.TurnId; CompleteCaller(ctx.RunId, Failed failure) ]

            Ok(decided avail events effects)

    | Dispatching _, DispatchRejected _ -> Ok(noChange StaleTurnMarker)

    // Error during Dispatching — treat as started with OrderedTurnMarkerObserved
    | Dispatching(ctx, plan), TurnErrorObserved error ->
        let started =
            { Plan = plan
              StartReceipt = OrderedTurnMarkerObserved }

        let events = [ TurnOutcomeObserved(plan.TurnId, FailureObserved error) ]

        Ok(decided (Draining(ctx, started, FailureObserved error)) events [])

    // TaskComplete during Dispatching
    | Dispatching(ctx, plan), TaskCompleteObserved output ->
        let started =
            { Plan = plan
              StartReceipt = OrderedTurnMarkerObserved }

        let events = [ TurnOutcomeObserved(plan.TurnId, CompletionRequested output) ]

        Ok(decided (Draining(ctx, started, CompletionRequested output)) events [])

    // 7.4: idle during Dispatching is a stale/late idle
    | Dispatching _, SessionIdleObserved -> Ok(noChange DuplicateIdleBeforeTurnMarker)

    // Cancel during Dispatching
    | Dispatching(ctx, plan), CancelRequested ->
        let activeTurn = NotYetStarted plan

        let abortCtx =
            { Reason = UserRequested
              FinalResult = Cancelled }

        let events = [ AbortRequested plan.TurnId ]

        let effects =
            [ AbortHostSession(ctx.SessionId, plan.TurnId)
              CancelTurnDeadline plan.TurnId
              ArmAbortDeadline plan.TurnId ]

        Ok(decided (Aborting(ctx, activeTurn, abortCtx)) events effects)

    // Turn deadline during Dispatching
    | Dispatching(ctx, plan), TurnDeadlineExpired tid when tid = plan.TurnId ->
        let activeTurn = NotYetStarted plan

        let abortCtx =
            { Reason = TurnDeadline
              FinalResult = Cancelled }

        let events = [ AbortRequested plan.TurnId ]

        let effects =
            [ AbortHostSession(ctx.SessionId, plan.TurnId)
              CancelTurnDeadline plan.TurnId
              ArmAbortDeadline plan.TurnId ]

        Ok(decided (Aborting(ctx, activeTurn, abortCtx)) events effects)

    | Dispatching _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)

    | Dispatching(ctx, _), SessionClosed ->
        let avail = Available { SessionId = ctx.SessionId }
        Ok(decided avail [] [])

    | Dispatching _, (AbortAcknowledged _ | AbortDeadlineExpired _ | TranscriptLoaded _) ->
        illegal (stateName ()) (cmdName ())

    // ── 7.5, 7.6, 7.8 Running ──
    | Running(ctx, started), TurnErrorObserved error ->
        let events = [ TurnOutcomeObserved(started.Plan.TurnId, FailureObserved error) ]

        Ok(decided (Draining(ctx, started, FailureObserved error)) events [])

    | Running(ctx, started), TaskCompleteObserved output ->
        let events =
            [ TurnOutcomeObserved(started.Plan.TurnId, CompletionRequested output) ]

        Ok(decided (Draining(ctx, started, CompletionRequested output)) events [])

    // 7.8: idle without explicit outcome → inspect transcript
    | Running(ctx, started), SessionIdleObserved ->
        let effects = [ ReadTranscript ctx.SessionId ]
        Ok(decided (InspectingTranscript(ctx, started)) [] effects)

    // Cancel during Running
    | Running(ctx, started), CancelRequested ->
        let abortCtx =
            { Reason = UserRequested
              FinalResult = Cancelled }

        let events = [ AbortRequested started.Plan.TurnId ]

        let effects =
            [ AbortHostSession(ctx.SessionId, started.Plan.TurnId)
              CancelTurnDeadline started.Plan.TurnId
              ArmAbortDeadline started.Plan.TurnId ]

        Ok(decided (Aborting(ctx, Started started, abortCtx)) events effects)

    // Turn deadline during Running
    | Running(ctx, started), TurnDeadlineExpired tid when tid = started.Plan.TurnId ->
        let abortCtx =
            { Reason = TurnDeadline
              FinalResult = Cancelled }

        let events = [ AbortRequested started.Plan.TurnId ]

        let effects =
            [ AbortHostSession(ctx.SessionId, started.Plan.TurnId)
              CancelTurnDeadline started.Plan.TurnId
              ArmAbortDeadline started.Plan.TurnId ]

        Ok(decided (Aborting(ctx, Started started, abortCtx)) events effects)

    | Running _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)

    | Running _, (DispatchAccepted _ | DispatchRejected _) -> Ok(noChange StaleTurnMarker)

    | Running(ctx, _), SessionClosed ->
        let avail = Available { SessionId = ctx.SessionId }
        Ok(decided avail [] [])

    | Running _, (TranscriptLoaded _ | AbortAcknowledged _ | AbortDeadlineExpired _) ->
        illegal (stateName ()) (cmdName ())

    // ── 7.7, 7.9 Draining ──

    // 7.7: task_complete wins over error
    | Draining(ctx, started, FailureObserved _), TaskCompleteObserved output ->
        let events =
            [ TurnOutcomeObserved(started.Plan.TurnId, CompletionRequested output) ]

        Ok(decided (Draining(ctx, started, CompletionRequested output)) events [])

    // Completion already wins — ignore subsequent error
    | Draining(_, _, CompletionRequested _), TurnErrorObserved _ -> Ok(noChange CompletionAlreadyWins)

    // Duplicate error
    | Draining(_, _, FailureObserved _), TurnErrorObserved _ -> Ok(noChange DuplicateError)

    // Duplicate task_complete
    | Draining(_, _, CompletionRequested _), TaskCompleteObserved _ -> Ok(noChange DuplicateTaskComplete)

    // 7.9: Draining(CompletionRequested) + idle → success
    | Draining(ctx, started, CompletionRequested output), SessionIdleObserved ->
        let avail = Available { SessionId = ctx.SessionId }

        let events =
            [ TurnFinished(started.Plan.TurnId, TurnCompleted output)
              RunFinished(ctx.RunId, Succeeded output) ]

        let effects =
            [ CancelTurnDeadline started.Plan.TurnId
              CompleteCaller(ctx.RunId, Succeeded output) ]

        Ok(decided avail events effects)

    // 7.9: Draining(FailureObserved) + idle → policy.afterError
    | Draining(ctx, started, FailureObserved error), SessionIdleObserved ->
        let policyDec = afterError ctx.FallbackConfig ctx.Chain ctx.Policy error

        match nextTurnFromPolicy ctx policyDec with
        | Some(ctx2, plan2) ->
            let events =
                [ TurnFinished(started.Plan.TurnId, TurnFailed error)
                  TurnDispatchRequested(makeTurnData ctx2 plan2) ]

            let effects =
                [ CancelTurnDeadline started.Plan.TurnId
                  DispatchPrompt plan2
                  ArmTurnDeadline plan2.TurnId ]

            Ok(decided (Dispatching(ctx2, plan2)) events effects)
        | None ->
            let failure =
                match policyDec with
                | StopWithFailure f -> f
                | _ -> FallbackExhausted error

            let avail = Available { SessionId = ctx.SessionId }

            let events =
                [ TurnFinished(started.Plan.TurnId, TurnFailed error)
                  RunFinished(ctx.RunId, Failed failure) ]

            let effects =
                [ CancelTurnDeadline started.Plan.TurnId
                  CompleteCaller(ctx.RunId, Failed failure) ]

            Ok(decided avail events effects)

    // Cancel during Draining
    | Draining(ctx, started, _), CancelRequested ->
        let abortCtx =
            { Reason = UserRequested
              FinalResult = Cancelled }

        let events = [ AbortRequested started.Plan.TurnId ]

        let effects =
            [ AbortHostSession(ctx.SessionId, started.Plan.TurnId)
              CancelTurnDeadline started.Plan.TurnId
              ArmAbortDeadline started.Plan.TurnId ]

        Ok(decided (Aborting(ctx, Started started, abortCtx)) events effects)

    // Turn deadline during Draining
    | Draining(ctx, started, _), TurnDeadlineExpired tid when tid = started.Plan.TurnId ->
        let abortCtx =
            { Reason = TurnDeadline
              FinalResult = Cancelled }

        let events = [ AbortRequested started.Plan.TurnId ]

        let effects =
            [ AbortHostSession(ctx.SessionId, started.Plan.TurnId)
              CancelTurnDeadline started.Plan.TurnId
              ArmAbortDeadline started.Plan.TurnId ]

        Ok(decided (Aborting(ctx, Started started, abortCtx)) events effects)

    | Draining _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)

    | Draining _, (DispatchAccepted _ | DispatchRejected _) -> Ok(noChange StaleTurnMarker)

    | Draining _, (TranscriptLoaded _ | AbortAcknowledged _ | AbortDeadlineExpired _) ->
        illegal (stateName ()) (cmdName ())

    | Draining(ctx, _, _), SessionClosed ->
        let avail = Available { SessionId = ctx.SessionId }
        Ok(decided avail [] [])

    // ── 7.10 InspectingTranscript ──
    | InspectingTranscript(ctx, started), TranscriptLoaded snap ->
        let transcriptDec = classifyTranscript snap

        match transcriptDec with
        | CompleteNaturally output ->
            let avail = Available { SessionId = ctx.SessionId }

            let events =
                [ TurnFinished(started.Plan.TurnId, TurnCompleted output)
                  RunFinished(ctx.RunId, Succeeded output) ]

            let effects =
                [ CancelTurnDeadline started.Plan.TurnId
                  CompleteCaller(ctx.RunId, Succeeded output) ]

            Ok(decided avail events effects)

        | _ ->
            let policyDec =
                afterTranscript ctx.FallbackConfig ctx.Chain ctx.Policy transcriptDec

            match nextTurnFromPolicy ctx policyDec with
            | Some(ctx2, plan2) ->
                let finishOutcome =
                    match transcriptDec with
                    | RecoverWithPrompt _ -> TurnRecovering
                    | _ -> TurnRecovering

                let events =
                    [ TurnFinished(started.Plan.TurnId, finishOutcome)
                      TurnDispatchRequested(makeTurnData ctx2 plan2) ]

                let effects =
                    [ CancelTurnDeadline started.Plan.TurnId
                      DispatchPrompt plan2
                      ArmTurnDeadline plan2.TurnId ]

                Ok(decided (Dispatching(ctx2, plan2)) events effects)
            | None ->
                let failure =
                    match policyDec with
                    | StopWithFailure f -> f
                    | _ -> RecoveryExhausted "Session idle without task completion and no recovery available"

                let avail = Available { SessionId = ctx.SessionId }

                let incompleteError: ErrorInput =
                    { ErrorName = "IncompleteRun"
                      DomainError = None
                      Message = "Session ended without completion"
                      StatusCode = None
                      IsRetryable = None }

                let events =
                    [ TurnFinished(started.Plan.TurnId, TurnFailed incompleteError)
                      RunFinished(ctx.RunId, Failed failure) ]

                let effects =
                    [ CancelTurnDeadline started.Plan.TurnId
                      CompleteCaller(ctx.RunId, Failed failure) ]

                Ok(decided avail events effects)

    | InspectingTranscript _, TurnErrorObserved _ -> Ok(noChange DuplicateError)

    | InspectingTranscript _, TaskCompleteObserved _ -> Ok(noChange DuplicateTaskComplete)

    | InspectingTranscript _, SessionIdleObserved -> Ok(noChange DuplicateIdleBeforeTurnMarker)

    | InspectingTranscript(ctx, started), CancelRequested ->
        let abortCtx =
            { Reason = UserRequested
              FinalResult = Cancelled }

        let events = [ AbortRequested started.Plan.TurnId ]

        let effects =
            [ AbortHostSession(ctx.SessionId, started.Plan.TurnId)
              CancelTurnDeadline started.Plan.TurnId
              ArmAbortDeadline started.Plan.TurnId ]

        Ok(decided (Aborting(ctx, Started started, abortCtx)) events effects)

    | InspectingTranscript _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)

    | InspectingTranscript _, (DispatchAccepted _ | DispatchRejected _) -> Ok(noChange StaleTurnMarker)

    | InspectingTranscript(ctx, _), SessionClosed ->
        let avail = Available { SessionId = ctx.SessionId }
        Ok(decided avail [] [])

    | InspectingTranscript _, (AbortAcknowledged _ | AbortDeadlineExpired _) -> illegal (stateName ()) (cmdName ())

    // ── 7.11 Aborting ──
    | Aborting(ctx, _, _), SessionIdleObserved ->
        let avail = Available { SessionId = ctx.SessionId }

        let effects = [ CompleteCaller(ctx.RunId, Cancelled) ]

        let events = [ RunFinished(ctx.RunId, Cancelled) ]
        Ok(decided avail events effects)

    | Aborting(ctx, turn, _), AbortAcknowledged tid ->
        let activeTurnId =
            match turn with
            | NotYetStarted p -> p.TurnId
            | Started s -> s.Plan.TurnId

        if tid <> activeTurnId then
            Ok(noChange StaleTurnMarker)
        else
            let avail = Available { SessionId = ctx.SessionId }

            let effects =
                [ CancelAbortDeadline activeTurnId; CompleteCaller(ctx.RunId, Cancelled) ]

            let events = [ RunFinished(ctx.RunId, Cancelled) ]
            Ok(decided avail events effects)

    | Aborting(ctx, turn, _), AbortDeadlineExpired tid ->
        let activeTurnId =
            match turn with
            | NotYetStarted p -> p.TurnId
            | Started s -> s.Plan.TurnId

        if tid <> activeTurnId then
            Ok(noChange StaleTimer)
        else
            let poisoned = Poisoned(AbortDidNotSettle activeTurnId)

            let effects =
                [ CompleteCaller(ctx.RunId, Failed(InfrastructureFailure "abort deadline expired")) ]

            let events =
                [ SessionPoisoned(ctx.SessionId, AbortDidNotSettle activeTurnId)
                  RunFinished(ctx.RunId, Failed(InfrastructureFailure "abort deadline expired")) ]

            Ok(decided poisoned events effects)

    | Aborting _, CancelRequested -> Ok(noChange StaleTimer)

    | Aborting _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)

    | Aborting _, (TurnErrorObserved _ | TaskCompleteObserved _) -> Ok(noChange StaleTimer)

    | Aborting _, (DispatchAccepted _ | DispatchRejected _) -> Ok(noChange StaleTurnMarker)

    | Aborting(ctx, _, _), SessionClosed ->
        let avail = Available { SessionId = ctx.SessionId }
        Ok(decided avail [] [])

    | Aborting _, TranscriptLoaded _ -> illegal (stateName ()) (cmdName ())
