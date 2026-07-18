module Wanxiangshu.Kernel.Subsession.DecisionObserve

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.TranscriptDecision
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.DecisionObservePredicates

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
    | TurnErrorObserved _ -> "TurnErrorObserved"
    | SessionIdleObserved -> "SessionIdleObserved"
    | EvidenceUpdated _ -> "EvidenceUpdated"
    | _ -> "Other"

let private handleRunningIdle (ctx: RunContext) (started: StartedTurn) (evidence: CurrentTurnEvidence) =
    let transcriptDec = classifyTurnEvidence evidence

    match transcriptDec with
    | CompleteNaturally output ->
        let ctx2 =
            { ctx with
                Policy = afterSuccessfulTurn ctx.Policy }

        succeedRun ctx2 output started.Plan.TurnId
    | _ ->
        let policyDec =
            afterTranscript ctx.FallbackConfig ctx.Chain ctx.Policy transcriptDec

        match nextTurnFromPolicy ctx policyDec with
        | Some(ctx2, plan2) ->
            let events =
                [ TurnFinished(started.Plan.TurnId, TurnRecovering)
                  TurnDispatchRequested(makeTurnData ctx2 plan2) ]

            decided (Dispatching(ctx2, plan2, CurrentTurnEvidence.empty)) events [ DispatchPrompt plan2 ]
        | None ->
            let failure' =
                match policyDec with
                | StopWithFailure f -> f
                | _ -> RecoveryExhausted "Session idle without task completion"

            failRun
                ctx
                failure'
                [ TurnFinished(started.Plan.TurnId, TurnInfrastructureFailed "session idle without task completion") ]

let private handleDrainingIdle
    (ctx: RunContext)
    (started: StartedTurn)
    (error: ErrorInput)
    (evidence: CurrentTurnEvidence)
    =
    // Priority 1: non-empty assistant text wins (mirrors classifyTurnEvidence).
    let assistantText =
        match evidence.Assistant with
        | AssistantSnapshot(_, _, text, _)
        | AssistantDelta(_, _, text, _) when not (System.String.IsNullOrWhiteSpace text) -> Some text
        | _ -> None

    match assistantText with
    | Some text -> succeedRun ctx text started.Plan.TurnId
    | None ->
        // Priority 2: non-empty CompletionRequested output as fallback.
        match evidence.Outcome with
        | CompletionRequested output when not (System.String.IsNullOrWhiteSpace output) ->
            succeedRun ctx output started.Plan.TurnId
        | _ ->
            let policyDec = afterError ctx.FallbackConfig ctx.Chain ctx.Policy error

            match nextTurnFromPolicy ctx policyDec with
            | Some(ctx2, plan2) ->
                let events =
                    [ TurnFinished(started.Plan.TurnId, TurnFailed error)
                      TurnDispatchRequested(makeTurnData ctx2 plan2) ]

                decided (Dispatching(ctx2, plan2, CurrentTurnEvidence.empty)) events [ DispatchPrompt plan2 ]
            | None ->
                let failure' =
                    match policyDec with
                    | StopWithFailure f -> f
                    | _ -> FallbackExhausted error

                failRun ctx failure' [ TurnFinished(started.Plan.TurnId, TurnFailed error) ]

let private decideDispatching (state: SubsessionState) (cmd: Command) =
    match state, cmd with
    | Dispatching _, TurnErrorObserved _ -> Ok(noChange UnattributedObservationBeforeStart)
    | Dispatching(ctx, plan, bufferedEvidence), EvidenceUpdated obs ->
        match obs.TurnId with
        | Some tid when tid = plan.TurnId ->
            let merged = CurrentTurnEvidence.merge bufferedEvidence obs.Evidence
            Ok(decided (Dispatching(ctx, plan, merged)) [] [])
        | Some _ -> Ok(noChange StaleTurnMarker)
        | None ->
            let merged = CurrentTurnEvidence.merge bufferedEvidence obs.Evidence
            Ok(decided (Dispatching(ctx, plan, merged)) [] [])
    | Dispatching _, SessionIdleObserved -> Ok(noChange DuplicateIdleBeforeTurnMarker)

    | CancellingDispatch _, SessionIdleObserved -> Ok(noChange DuplicateIdleBeforeTurnMarker)
    | CancellingDispatch _, TurnErrorObserved _ -> Ok(noChange UnattributedObservationBeforeStart)
    | CancellingDispatch _, EvidenceUpdated _ -> Ok(noChange EvidenceBeforeRun)

    | ReconcilingUnknownDispatch _, SessionIdleObserved -> Ok(noChange StaleTimer)
    | ReconcilingUnknownDispatch _, TurnErrorObserved _ -> Ok(noChange StaleTimer)
    | ReconcilingUnknownDispatch _, EvidenceUpdated _ -> Ok(noChange EvidenceBeforeRun)
    | _ -> illegal (stateName state) (cmdName cmd)

let private decideRunning (state: SubsessionState) (cmd: Command) =
    match state, cmd with
    | Running(ctx, started, evidence), TurnErrorObserved error ->
        match evidence.Outcome with
        | CompletionRequested _ -> Ok(decided (Running(ctx, started, evidence)) [] [])
        | _ -> Ok(decided (Draining(ctx, started, error, evidence)) [] [])
    | Running(ctx, started, evidence), SessionIdleObserved -> Ok(handleRunningIdle ctx started evidence)
    | Running(ctx, started, evidence), EvidenceUpdated obs ->
        match obs.TurnId with
        | Some tid when tid = started.Plan.TurnId ->
            let merged = CurrentTurnEvidence.merge evidence obs.Evidence
            Ok(decided (Running(ctx, started, merged)) [] [])
        | Some _ -> Ok(noChange StaleTurnMarker)
        | None ->
            let merged = CurrentTurnEvidence.merge evidence obs.Evidence
            Ok(decided (Running(ctx, started, merged)) [] [])

    | Draining _, TurnErrorObserved _ -> Ok(noChange DuplicateError)
    | Draining(ctx, started, error, evidence), SessionIdleObserved -> Ok(handleDrainingIdle ctx started error evidence)
    | Draining(ctx, started, error, evidence), EvidenceUpdated obs ->
        match obs.TurnId with
        | Some tid when tid = started.Plan.TurnId ->
            let merged = CurrentTurnEvidence.merge evidence obs.Evidence
            Ok(decided (Draining(ctx, started, error, merged)) [] [])
        | Some _ -> Ok(noChange StaleTurnMarker)
        | None ->
            let merged = CurrentTurnEvidence.merge evidence obs.Evidence
            Ok(decided (Draining(ctx, started, error, merged)) [] [])
    | _ -> illegal (stateName state) (cmdName cmd)

let private decideAbort (state: SubsessionState) (cmd: Command) =
    match state, cmd with
    | IssuingAbort(ctx, turn, abortCtx, idleObserved), SessionIdleObserved ->
        Ok(decided (IssuingAbort(ctx, turn, abortCtx, true)) [] [])
    | IssuingAbort _, (TurnErrorObserved _ | EvidenceUpdated _) -> Ok(noChange StaleTimer)

    | AwaitingAbortSettle(ctx, turn, abortCtx), SessionIdleObserved ->
        let tid = activeTurnId turn
        let effects = [ QuerySessionQuiescence(ctx.SessionId, tid) ]
        Ok(decided (ReconcilingAbortSettle(ctx, turn, abortCtx)) [] effects)
    | AwaitingAbortSettle _, (TurnErrorObserved _ | EvidenceUpdated _) -> Ok(noChange StaleTimer)

    | ReconcilingAbortSettle _, SessionIdleObserved -> Ok(noChange AbortInProgress)
    | ReconcilingAbortSettle _, (TurnErrorObserved _ | EvidenceUpdated _) -> Ok(noChange StaleTimer)
    | _ -> illegal (stateName state) (cmdName cmd)

let decide (state: SubsessionState) (cmd: Command) : Result<DecisionResult, DecisionError> =
    match state with
    | Poisoned _ -> Ok(noChange StaleTimer)
    | Available _ ->
        match cmd with
        | EvidenceUpdated _ -> Ok(noChange StaleTimer)
        | SessionIdleObserved -> Ok(noChange StaleTimer)
        | _ -> illegal (stateName state) (cmdName cmd)
    | Dispatching _
    | CancellingDispatch _
    | ReconcilingUnknownDispatch _ -> decideDispatching state cmd
    | Running _
    | Draining _ -> decideRunning state cmd
    | IssuingAbort _
    | AwaitingAbortSettle _
    | ReconcilingAbortSettle _ -> decideAbort state cmd
    | ClosingUnknownDispatch _ -> Ok(noChange StaleTimer)
