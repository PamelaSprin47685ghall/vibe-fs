module Wanxiangshu.Tests.SubsessionDecisionTests

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Decision
open Wanxiangshu.Kernel.Subsession.TranscriptDecision
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Tests.Assert

let private fail (msg: string) = check msg false

// ── Fixtures ──

let private model0: FallbackModel =
    { ProviderID = "p"
      ModelID = "m0"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private model1: FallbackModel = { model0 with ModelID = "m1" }

let private chain: FallbackChain = [ model0; model1 ]

let private cfg: FallbackConfig =
    { DefaultChain = chain
      AgentChains = Map.empty
      MaxRetries = 1
      LoopMaxContinues = 10
      MaxRecoveries = 3 }

let private sid = SessionId.create "child-1"
let private parent = SessionId.create "parent-1"
let private runId = RunId.create "run-1"
let private turn0 = TurnId.create "run-1-t0"

let private err: ErrorInput =
    { ErrorName = "RateLimit"
      DomainError = None
      Message = "429"
      StatusCode = Some 429
      IsRetryable = Some true }

let private avail = Available { SessionId = sid }

let private request: StartRunRequest =
    { RunId = runId
      SessionId = sid
      ParentSessionId = parent
      Prompt = "do work"
      FallbackConfig = cfg
      Directive = RetryChain chain
      InitiallyCancelled = false }

let private policy0 = initialPolicy cfg chain

let private mkCtx policy ordinal =
    { RunId = runId
      ParentSessionId = parent
      SessionId = sid
      Policy = policy
      FallbackConfig = cfg
      Chain = chain
      NextTurnOrdinal = ordinal }

let private mkPlan tid ordinal model prompt =
    { TurnId = tid
      Ordinal = ordinal
      Model = Some model
      Prompt = prompt }

let private hasEffect pred (effects: Effect list) = List.exists pred effects

let private isDispatchPrompt =
    function
    | DispatchPrompt _ -> true
    | _ -> false

let private isCompleteCaller =
    function
    | CompleteCaller _ -> true
    | _ -> false

// ── Table-driven scenarios ──

let startRunFromAvailable () =
    match decide avail (StartRun request) with
    | Ok(Decided d) ->
        match d.NextState with
        | Dispatching _ -> ()
        | other -> fail ("expected Dispatching, got " + string other)

        check "emits DispatchPrompt" (hasEffect isDispatchPrompt d.Effects)

        check
            "arms turn deadline"
            (hasEffect
                (function
                | ArmTurnDeadline _ -> true
                | _ -> false)
                d.Effects)

        check "no CompleteCaller on start" (not (hasEffect isCompleteCaller d.Effects))
    | other -> fail ("unexpected: " + string other)

let secondStartRunRejected () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"
    let state = Dispatching(ctx, plan, CurrentTurnEvidence.empty)

    match decide state (StartRun request) with
    | Ok(Decided d) ->
        check
            "RejectStart AlreadyRunning"
            (hasEffect
                (function
                | RejectStart AlreadyRunning -> true
                | _ -> false)
                d.Effects)

        check "state unchanged" (d.NextState = state)
    | other -> fail ("unexpected: " + string other)

let dispatchingIdleIgnored () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"
    let state = Dispatching(ctx, plan, CurrentTurnEvidence.empty)

    match decide state SessionIdleObserved with
    | Ok(NoChange DuplicateIdleBeforeTurnMarker) -> ()
    | other -> fail ("expected NoChange DuplicateIdleBeforeTurnMarker, got " + string other)

let dispatchingErrorIgnored () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"
    let state = Dispatching(ctx, plan, CurrentTurnEvidence.empty)

    match decide state (TurnErrorObserved err) with
    | Ok(NoChange UnattributedObservationBeforeStart) -> ()
    | other -> fail ("expected UnattributedObservationBeforeStart, got " + string other)

/// Regression: idle can legitimately arrive while a turn is Dispatching (host
/// event ordering is not process-ordered w.r.t. our own dispatch promise).
/// It must be ignored here — NOT silently accepted into a state that can
/// never recover. Then, once DispatchAccepted legitimately arrives and the
/// turn reaches Running, a SUBSEQUENT idle must classify normally and the
/// run must actually converge to Succeeded. This pins the exact causality
/// bug that shipped in the IntegrationSubagentMockClient race: firing
/// SessionIdleObserved before the Dispatch promise resolves must never wedge
/// the actor forever.
let idleDuringDispatchingThenRealIdleConverges () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"
    let dispatchingState = Dispatching(ctx, plan, CurrentTurnEvidence.empty)

    // Premature idle while still Dispatching: must be a named ignore, not a
    // state transition (i.e. the actor must remain Dispatching afterwards).
    match decide dispatchingState SessionIdleObserved with
    | Ok(NoChange DuplicateIdleBeforeTurnMarker) -> ()
    | other -> fail ("expected NoChange DuplicateIdleBeforeTurnMarker, got " + string other)

    // Now the legitimate DispatchAccepted arrives (as if the host's prompt
    // call had actually resolved) — must reach Running.
    match decide dispatchingState (DispatchAccepted(turn0, OrderedTurnMarkerObserved)) with
    | Ok(Decided d1) ->
        match d1.NextState with
        | Running(_, _, evidence0) ->
            check "evidence starts empty" (evidence0 = CurrentTurnEvidence.empty)

            // Evidence arrives (assistant text, normal finish) before idle.
            let evidence =
                { CurrentTurnEvidence.empty with
                    Assistant = AssistantContent("done", Some NormalFinish) }

            match decide d1.NextState (EvidenceUpdated { TurnId = turn0; Evidence = evidence }) with
            | Ok(Decided d2) ->
                match d2.NextState with
                | Running _ ->
                    // Real idle now — must classify and converge to Succeeded,
                    // proving the earlier premature idle did not leave any
                    // latent state that would block or corrupt this transition.
                    match decide d2.NextState SessionIdleObserved with
                    | Ok(Decided d3) ->
                        match d3.NextState with
                        | Available _ ->
                            check
                                "run converges to Succeeded"
                                (hasEffect
                                    (function
                                    | CompleteCaller(_, Succeeded "done") -> true
                                    | _ -> false)
                                    d3.Effects)
                        | other -> fail ("expected Available (converged), got " + string other)
                    | other -> fail ("unexpected on real idle: " + string other)
                | other -> fail ("expected Running after evidence merge, got " + string other)
            | other -> fail ("unexpected on EvidenceUpdated: " + string other)
        | other -> fail ("expected Running after DispatchAccepted, got " + string other)
    | other -> fail ("unexpected on DispatchAccepted: " + string other)

let runningErrorDrains () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let state = Running(ctx, started, CurrentTurnEvidence.empty)

    match decide state (TurnErrorObserved err) with
    | Ok(Decided d) ->
        match d.NextState with
        | Draining(_, _, heldErr) -> equal "held error preserved" err.Message heldErr.Message
        | other -> fail ("expected Draining, got " + string other)

        check "no DispatchPrompt on error" (not (hasEffect isDispatchPrompt d.Effects))
        check "no CompleteCaller on error" (not (hasEffect isCompleteCaller d.Effects))
        check "no events on error (held, not yet acted on)" (List.isEmpty d.Events)
    | other -> fail ("unexpected: " + string other)

let drainingDuplicateErrorIgnored () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let state = Draining(ctx, started, err)

    match decide state (TurnErrorObserved err) with
    | Ok(NoChange DuplicateError) -> ()
    | other -> fail ("expected DuplicateError, got " + string other)

let drainingIdleRetriesViaFallbackPolicy () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let state = Draining(ctx, started, err)

    match decide state SessionIdleObserved with
    | Ok(Decided d) ->
        match d.NextState with
        | Dispatching _ -> check "retries after held error resolved on idle" (hasEffect isDispatchPrompt d.Effects)
        | Available _ ->
            check
                "or exhausts to CompleteCaller Failed"
                (hasEffect
                    (function
                    | CompleteCaller(_, Failed _) -> true
                    | _ -> false)
                    d.Effects)
        | other -> fail ("unexpected: " + string other)
    | other -> fail ("unexpected: " + string other)

let poisonedRejectsStart () =
    let state = Poisoned(AbortDidNotSettle turn0)

    match decide state (StartRun request) with
    | Ok(Decided d) ->
        check
            "RejectStart SessionPoisoned"
            (hasEffect
                (function
                | RejectStart(StartRunError.SessionPoisoned _) -> true
                | _ -> false)
                d.Effects)

        check "no DispatchPrompt when poisoned" (not (hasEffect isDispatchPrompt d.Effects))
    | other -> fail ("unexpected: " + string other)

let sessionClosedCompletesCaller () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let state = Running(ctx, started, CurrentTurnEvidence.empty)

    match decide state SessionClosed with
    | Ok(Decided d) ->
        match d.NextState with
        | Poisoned SessionClosedUnexpectedly -> ()
        | other -> fail ("expected Poisoned SessionClosedUnexpectedly, got " + string other)

        check
            "CompleteCaller on SessionClosed"
            (hasEffect
                (function
                | CompleteCaller _ -> true
                | _ -> false)
                d.Effects)

        check
            "DisposeActor"
            (hasEffect
                (function
                | DisposeActor -> true
                | _ -> false)
                d.Effects)
    | other -> fail ("unexpected: " + string other)

let abortIdleTriggersReconcile () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let abortCtx =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = AwaitingAbortSettle(ctx, Started started, abortCtx)

    match decide state SessionIdleObserved with
    | Ok(Decided d) ->
        match d.NextState with
        | ReconcilingAbortSettle _ -> ()
        | other -> fail ("expected ReconcilingAbortSettle, got " + string other)

        check
            "QueryDispatchStatus effect"
            (hasEffect
                (function
                | QueryDispatchStatus _ -> true
                | _ -> false)
                d.Effects)
    | other -> fail ("unexpected: " + string other)

let reconcileAbortSettleAccepted () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let abortCtx =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = ReconcilingAbortSettle(ctx, Started started, abortCtx)
    let receipt = HostRunAccepted "host-run-1"

    match decide state (DispatchStatusResolved(DispatchStatus.Accepted receipt)) with
    | Ok(Decided d) ->
        match d.NextState with
        | Available _ -> ()
        | other -> fail ("expected Available state, got " + string other)

        check
            "CompleteCaller Cancelled"
            (hasEffect
                (function
                | CompleteCaller(_, Cancelled) -> true
                | _ -> false)
                d.Effects)
    | other -> fail ("unexpected: " + string other)

let reconcileAbortSettleDefinitelyNotAccepted () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let abortCtx =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = ReconcilingAbortSettle(ctx, Started started, abortCtx)

    match decide state (DispatchStatusResolved DispatchStatus.DefinitelyNotAccepted) with
    | Ok(Decided d) ->
        match d.NextState with
        | Available _ -> ()
        | other -> fail ("expected Available state, got " + string other)

        check
            "CompleteCaller Cancelled"
            (hasEffect
                (function
                | CompleteCaller(_, Cancelled) -> true
                | _ -> false)
                d.Effects)
    | other -> fail ("unexpected: " + string other)

let reconcileAbortSettleStillPending () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let abortCtx =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = ReconcilingAbortSettle(ctx, Started started, abortCtx)

    match decide state (DispatchStatusResolved DispatchStatus.StillPending) with
    | Ok(Decided d) ->
        match d.NextState with
        | AwaitingAbortSettle _ -> ()
        | other -> fail ("expected AwaitingAbortSettle state, got " + string other)
    | other -> fail ("unexpected: " + string other)

let reconcileAbortSettleUnknown () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let abortCtx =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = ReconcilingAbortSettle(ctx, Started started, abortCtx)

    match decide state (DispatchStatusResolved DispatchStatus.Unknown) with
    | Ok(Decided d) ->
        match d.NextState with
        | Poisoned(AbortDidNotSettle tid) when tid = turn0 -> ()
        | other -> fail ("expected Poisoned AbortDidNotSettle, got " + string other)

        check
            "CompleteCaller Failed"
            (hasEffect
                (function
                | CompleteCaller(_, Failed _) -> true
                | _ -> false)
                d.Effects)
    | other -> fail ("unexpected: " + string other)



// ── Policy A → B → C ──

let policyAdvancesModels () =
    let mA = { model0 with ModelID = "A" }
    let mB = { model0 with ModelID = "B" }
    let mC = { model0 with ModelID = "C" }
    let chain3 = [ mA; mB; mC ]

    let cfg0 =
        { cfg with
            MaxRetries = 0
            DefaultChain = chain3 }

    let p0 = initialPolicy cfg0 chain3

    // First error at StableAt 0 with MaxRetries=0 → Scanning(0,0) with model A
    match afterError cfg0 chain3 p0 err with
    | NextTurn(p1, model, _) ->
        equal "first scan model A" "A" model.ModelID

        match p1.Selection with
        | Scanning(0, 0) -> ()
        | other -> fail ("expected Scanning(0,0), got " + string other)

        // Next error while Scanning → nextIdx = 1 → B
        match afterError cfg0 chain3 p1 err with
        | NextTurn(p2, model2, _) ->
            equal "second scan model B" "B" model2.ModelID

            match p2.Selection with
            | Scanning(1, 0) -> ()
            | other -> fail ("expected Scanning(1,0), got " + string other)

            match afterError cfg0 chain3 p2 err with
            | NextTurn(_, model3, _) -> equal "third scan model C" "C" model3.ModelID
            | StopWithFailure _ -> fail "expected next turn C"
        | StopWithFailure _ -> fail "expected next turn B"
    | StopWithFailure _ -> fail "expected first next turn"



let idleBeforeAbortBarrierIgnored () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let abortCtx =
        { Reason = AcceptanceUnknownAfterDispatch
          AfterStop = RetryAfterSafeStop err }

    let state = IssuingAbort(ctx, NotYetStarted plan, abortCtx)

    match decide state SessionIdleObserved with
    | Ok(NoChange IdleBeforeAbortBarrier) -> ()
    | other -> fail ("expected NoChange IdleBeforeAbortBarrier, got " + string other)

let abortUnavailableStaysIssuing () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let abortCtx =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = IssuingAbort(ctx, NotYetStarted plan, abortCtx)

    match decide state (AbortRequestFailed(turn0, err)) with
    | Ok(NoChange AbortInProgress) -> ()
    | other -> fail ("expected AbortInProgress, got " + string other)

let initiallyCancelledNoDispatch () =
    let req =
        { request with
            InitiallyCancelled = true }

    match decide avail (StartRun req) with
    | Ok(Decided d) ->
        check "no DispatchPrompt" (not (hasEffect isDispatchPrompt d.Effects))

        check
            "CompleteCaller Cancelled"
            (hasEffect
                (function
                | CompleteCaller(_, Cancelled) -> true
                | _ -> false)
                d.Effects)
    | other -> fail ("unexpected: " + string other)

let acceptanceUnknownRetriesAfterAbortConfirmed () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let abortCtx =
        { Reason = AcceptanceUnknownAfterDispatch
          AfterStop = RetryAfterSafeStop err }

    let state = AwaitingAbortSettle(ctx, NotYetStarted plan, abortCtx)

    match decide state (AbortConfirmed turn0) with
    | Ok(Decided d) ->
        match d.NextState with
        | Dispatching _ -> check "retry after safe stop" (hasEffect isDispatchPrompt d.Effects)
        | Available _ ->
            // Exhausted chain is also acceptable.
            check
                "failed not cancelled"
                (hasEffect
                    (function
                    | CompleteCaller(_, Failed _) -> true
                    | _ -> false)
                    d.Effects)
        | other -> fail ("unexpected next: " + string other)

        check
            "not Cancelled"
            (not (
                hasEffect
                    (function
                    | CompleteCaller(_, Cancelled) -> true
                    | _ -> false)
                    d.Effects
            ))
    | other -> fail ("unexpected: " + string other)

let turnDeadlineAfterAbortIsTimeoutNotCancelled () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let abortCtx =
        { Reason = TurnDeadline
          AfterStop = FinishFailed(InfrastructureFailure "turn deadline expired") }

    let state = AwaitingAbortSettle(ctx, Started started, abortCtx)

    match decide state SessionIdleObserved with
    | Ok(Decided d) ->
        match d.NextState with
        | ReconcilingAbortSettle _ ->
            match decide d.NextState (DispatchStatusResolved(DispatchStatus.Accepted OrderedTurnMarkerObserved)) with
            | Ok(Decided d2) ->
                match d2.NextState with
                | Available _ ->
                    check
                        "timeout failure"
                        (hasEffect
                            (function
                            | CompleteCaller(_, Failed(InfrastructureFailure _)) -> true
                            | _ -> false)
                            d2.Effects)

                    check
                        "not Cancelled"
                        (not (
                            hasEffect
                                (function
                                | CompleteCaller(_, Cancelled) -> true
                                | _ -> false)
                                d2.Effects
                        ))
                | other -> fail ("expected Available state, got " + string other)
            | other -> fail ("unexpected decide d2: " + string other)
        | other -> fail ("expected ReconcilingAbortSettle, got " + string other)
    | other -> fail ("unexpected: " + string other)

// ── ModelDirective: DelegateToHost vs RetryChain ──

/// DelegateToHost must produce a TurnPlan with Model=None and proceed through
/// the full normal dispatch lifecycle — it is NOT a rejection path. This is
/// the branch that lets OpenCode's session.prompt fall through to
/// ag.model / opencode.jsonc static config instead of wanxiangshu forcing a
/// parent-session model override.
let startRunWithDelegateToHostProducesNoneModel () =
    let req =
        { request with
            Directive = DelegateToHost }

    match decide avail (StartRun req) with
    | Ok(Decided d) ->
        match d.NextState with
        | Dispatching(_, plan, _) -> check "DelegateToHost plan has no model" plan.Model.IsNone
        | other -> fail ("expected Dispatching, got " + string other)

        check "DelegateToHost still dispatches" (hasEffect isDispatchPrompt d.Effects)
    | other -> fail ("unexpected: " + string other)

/// RetryChain [] is a defensive branch for callers that violate the
/// invariant "non-empty chain when retry capability is requested". Must
/// reject exactly like the current empty-Chain behavior — no dispatch.
let startRunWithEmptyRetryChainRejectsNoModel () =
    let req =
        { request with
            Directive = RetryChain [] }

    match decide avail (StartRun req) with
    | Ok(Decided d) ->
        check
            "RejectStart NoModelAvailable"
            (hasEffect
                (function
                | RejectStart NoModelAvailable -> true
                | _ -> false)
                d.Effects)

        check "no DispatchPrompt on empty retry chain" (not (hasEffect isDispatchPrompt d.Effects))
    | other -> fail ("unexpected: " + string other)

/// RetryChain with a non-empty chain must behave exactly as today: first
/// model dispatched, full chain preserved on RunContext for later retries.
let startRunWithRetryChainProducesSomeModel () =
    let req =
        { request with
            Directive = RetryChain chain }

    match decide avail (StartRun req) with
    | Ok(Decided d) ->
        match d.NextState with
        | Dispatching(ctx, plan, _) ->
            check "RetryChain plan model is first of chain" (plan.Model = Some model0)
            check "RetryChain ctx keeps full chain" (ctx.Chain = chain)
        | other -> fail ("expected Dispatching, got " + string other)
    | other -> fail ("unexpected: " + string other)

let run () =
    startRunFromAvailable ()
    secondStartRunRejected ()
    dispatchingIdleIgnored ()
    dispatchingErrorIgnored ()
    idleDuringDispatchingThenRealIdleConverges ()
    runningErrorDrains ()
    drainingDuplicateErrorIgnored ()
    drainingIdleRetriesViaFallbackPolicy ()
    poisonedRejectsStart ()
    sessionClosedCompletesCaller ()
    abortIdleTriggersReconcile ()
    reconcileAbortSettleAccepted ()
    reconcileAbortSettleDefinitelyNotAccepted ()
    reconcileAbortSettleStillPending ()
    reconcileAbortSettleUnknown ()
    policyAdvancesModels ()
    idleBeforeAbortBarrierIgnored ()
    abortUnavailableStaysIssuing ()
    initiallyCancelledNoDispatch ()
    acceptanceUnknownRetriesAfterAbortConfirmed ()
    turnDeadlineAfterAbortIsTimeoutNotCancelled ()
    startRunWithDelegateToHostProducesNoneModel ()
    startRunWithEmptyRetryChainRejectsNoModel ()
    startRunWithRetryChainProducesSomeModel ()
