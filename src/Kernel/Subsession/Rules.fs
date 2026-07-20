module Wanxiangshu.Kernel.Subsession.Rules

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Policy

let delegateToHostSentinel =
    { ProviderID = ""
      ModelID = ""
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let makeTurnData (c: RunContext) (p: TurnPlan) : TurnData =
    { RunId = c.RunId
      TurnId = p.TurnId
      Ordinal = p.Ordinal
      Model = p.Model |> Option.defaultValue delegateToHostSentinel
      Prompt = p.Prompt
      DeadlineAtMs = 0L }

let nextTurnFromPolicy (ctx: RunContext) (decision: PolicyDecision) : (RunContext * TurnPlan) option =
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

let activeTurnId (turn: ActiveTurn) : TurnId =
    match turn with
    | NotYetStarted p -> p.TurnId
    | Started s -> s.Plan.TurnId

let decided state events effects : DecisionResult =
    Decided
        { NextState = state
          Events = events
          Effects = effects }

let noChange reason : DecisionResult = NoChange reason

let illegal state cmd : Result<DecisionResult, DecisionError> = Error(IllegalTransition(state, cmd))

let failRun (ctx: RunContext) (failure: RunFailure) (extraEvents: SubsessionEvent list) =
    let result = Failed failure

    decided
        (Available { SessionId = ctx.SessionId })
        (extraEvents @ [ RunFinished(ctx.RunId, result) ])
        [ CompleteCaller(ctx.RunId, result) ]

let finishWithResult (ctx: RunContext) (result: RunResult) (turnId: TurnId) =
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

let beginAbort (nowMs: int64) (ctx: RunContext) (turn: ActiveTurn) (reason: AbortReason) (afterStop: AfterAbort) =
    let tid = activeTurnId turn
    let abortDeadlineAtMs = nowMs + 60_000L
    let events = [ AbortRequested(ctx.RunId, tid, abortDeadlineAtMs) ]
    let effects = [ AbortHostSession(ctx.SessionId, tid); CancelPendingDispatch tid ]

    decided
        (IssuingAbort(
            ctx,
            turn,
            { Reason = reason
              AfterStop = afterStop },
            false,
            abortDeadlineAtMs
        ))
        events
        effects

let closeActive (ctx: RunContext) (turnId: TurnId) =
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

let applyAfterAbort (nowMs: int64) (ctx: RunContext) (turn: ActiveTurn) (abortCtx: AbortContext) =
    let tid = activeTurnId turn

    match abortCtx.AfterStop with
    | FinishCancelled -> finishWithResult ctx Cancelled tid
    | FinishFailed failure -> finishWithResult ctx (Failed failure) tid
    | RetryAfterSafeStop error ->
        match nextTurnFromPolicy ctx (afterError ctx.FallbackConfig ctx.Chain ctx.Policy error) with
        | Some(ctx2, plan2) ->
            let turnDeadlineAtMs = nowMs + 300_000L

            let events =
                [ TurnFinished(tid, TurnFailed error)
                  TurnDispatchRequested
                      { RunId = ctx2.RunId
                        TurnId = plan2.TurnId
                        Ordinal = plan2.Ordinal
                        Model = plan2.Model |> Option.defaultValue delegateToHostSentinel
                        Prompt = plan2.Prompt
                        DeadlineAtMs = turnDeadlineAtMs } ]

            decided
                (Dispatching(ctx2, plan2, CurrentTurnEvidence.empty, turnDeadlineAtMs))
                events
                [ DispatchPrompt plan2 ]
        | None ->
            let failure' =
                match afterError ctx.FallbackConfig ctx.Chain ctx.Policy error with
                | StopWithFailure f -> f
                | _ -> FallbackExhausted error

            finishWithResult ctx (Failed failure') tid

let isStaleTimerCommand =
    function
    | TurnDeadlineExpired _
    | AbortDeadlineExpired _
    | AbortConfirmed _
    | AbortHostAccepted _
    | AbortRequestFailed _
    | EvidenceUpdated _
    | ReconciliationDeadlineExpired _
    | SessionQuiescenceResolved _
    | DispatchStatusResolved _
    | PhysicalCloseResolved _ -> true
    | _ -> false

let isIllegalWhenDispatching =
    function
    | AbortDeadlineExpired _
    | DispatchStatusResolved _
    | AbortConfirmed _
    | AbortHostAccepted _
    | AbortRequestFailed _
    | ReconciliationDeadlineExpired _
    | SessionQuiescenceResolved _ -> true
    | _ -> false

let isIllegalWhenCancelling =
    function
    | DispatchStatusResolved _
    | AbortConfirmed _
    | AbortHostAccepted _
    | AbortRequestFailed _
    | ReconciliationDeadlineExpired _
    | SessionQuiescenceResolved _ -> true
    | _ -> false

let isIllegalWhenRunning =
    function
    | DispatchStatusResolved _
    | AbortDeadlineExpired _
    | AbortConfirmed _
    | AbortHostAccepted _
    | AbortRequestFailed _
    | ReconciliationDeadlineExpired _
    | SessionQuiescenceResolved _ -> true
    | _ -> false

let isIllegalWhenDraining = isIllegalWhenRunning

let stateName state =
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

let cmdName cmd =
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
