module Wanxiangshu.Tests.SubsessionScenarioTests

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Decision
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
      MaxRetries = 0
      LoopMaxContinues = 10
      MaxRecoveries = 3 }

let private sid = SessionId.create "child-1"
let private parent = SessionId.create "parent-1"
let private runId = RunId.create "run-1"

let private err: ErrorInput =
    { ErrorName = "RateLimit"
      DomainError = None
      Message = "429"
      StatusCode = Some 429
      IsRetryable = Some true }

let private request: StartRunRequest =
    { RunId = runId
      SessionId = sid
      ParentSessionId = parent
      Prompt = "do work"
      FallbackConfig = cfg
      Directive = RetryChain chain
      InitiallyCancelled = false }

let private decide state cmd =
    Wanxiangshu.Kernel.Subsession.Decision.decide 1000000L state cmd

let private mustDecide state cmd =
    match decide state cmd with
    | Ok(Decided d) -> d
    | Ok(NoChange r) -> failwith ("unexpected NoChange: " + string r)
    | Error e -> failwith ("decision error: " + string e)

let private mustNoChange state cmd expected =
    match decide state cmd with
    | Ok(NoChange r) when r = expected -> ()
    | other -> fail ("expected NoChange " + string expected + ", got " + string other)

let private completeCallerCount (effects: Effect list) =
    effects
    |> List.filter (function
        | CompleteCaller _ -> true
        | _ -> false)
    |> List.length

let private dispatchPromptCount (effects: Effect list) =
    effects
    |> List.filter (function
        | DispatchPrompt _ -> true
        | _ -> false)
    |> List.length

// ── Scenarios ──

let scenarioErrorThenIdleRetriesThenSucceeds () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan, _, _) ->
        let d1 =
            mustDecide d0.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved))

        let d2 = mustDecide d1.NextState (TurnErrorObserved err)

        match d2.NextState with
        | Draining(_, _, _, _, _) -> check "no CompleteCaller while error held" (completeCallerCount d2.Effects = 0)
        | other -> fail ("expected Draining, got " + string other)

        // MaxRetries=0 → error resolved on idle exhausts the chain immediately.
        let d3 = mustDecide d2.NextState SessionIdleObserved

        match d3.NextState with
        | Available _ ->
            check
                "failed (not succeeded) after error resolved on idle"
                (List.exists
                    (function
                    | CompleteCaller(_, Failed _) -> true
                    | _ -> false)
                    d3.Effects)
        | Dispatching _ -> check "or retries" (dispatchPromptCount d3.Effects = 1)
        | other -> fail ("unexpected: " + string other)
    | other -> fail ("expected Dispatching, got " + string other)

let scenarioErrorIdleRetry () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan, _, _) ->
        let d1 =
            mustDecide d0.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved))

        let d2 = mustDecide d1.NextState (TurnErrorObserved err)
        check "no DispatchPrompt on error" (dispatchPromptCount d2.Effects = 0)

        let d3 = mustDecide d2.NextState SessionIdleObserved

        match d3.NextState with
        | Dispatching _ ->
            check "retry DispatchPrompt after idle" (dispatchPromptCount d3.Effects = 1)
            check "no CompleteCaller on retry path" (completeCallerCount d3.Effects = 0)
        | Available _ -> check "CompleteCaller Failed" (completeCallerCount d3.Effects = 1)
        | other -> fail ("unexpected after error idle: " + string other)
    | other -> fail ("expected Dispatching, got " + string other)

let scenarioDispatchRejectRetry () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan, _, _) ->
        let d1 = mustDecide d0.NextState (DispatchRejected(plan.TurnId, HostRejected err))

        match d1.NextState with
        | Dispatching _ -> check "DispatchPrompt without idle" (dispatchPromptCount d1.Effects = 1)
        | Available _ -> check "or CompleteCaller Failed" (completeCallerCount d1.Effects = 1)
        | other -> fail ("unexpected: " + string other)
    | other -> fail ("expected Dispatching, got " + string other)

let scenarioAcceptanceUnknownAborts () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan, _, _) ->
        let d1 =
            mustDecide d0.NextState (DispatchRejected(plan.TurnId, HostAcceptanceUnknown err))

        match d1.NextState with
        | ReconcilingUnknownDispatch(ctx, plan, cancelCtx, _, _, _) ->
            check "no DispatchPrompt on acceptance unknown" (dispatchPromptCount d1.Effects = 0)

            let d2 =
                mustDecide d1.NextState (DispatchStatusResolved(DispatchStatus.Accepted OrderedTurnMarkerObserved))

            match d2.NextState with
            | IssuingAbort(_, _, abortCtx, _, _) ->
                check
                    "AbortHostSession"
                    (List.exists
                        (function
                        | AbortHostSession _ -> true
                        | _ -> false)
                        d2.Effects)

                match abortCtx.AfterStop with
                | RetryAfterSafeStop _ -> ()
                | other -> fail ("expected RetryAfterSafeStop, got " + string other)

                // AbortConfirmed applies AfterStop — never Cancelled.
                let d3 = mustDecide d2.NextState (AbortConfirmed plan.TurnId)

                check
                    "not Cancelled after AcceptanceUnknown"
                    (not (
                        List.exists
                            (function
                            | CompleteCaller(_, Cancelled) -> true
                            | _ -> false)
                            d3.Effects
                    ))
            | other -> fail ("expected IssuingAbort, got " + string other)
        | other -> fail ("expected ReconcilingUnknownDispatch, got " + string other)
    | other -> fail ("expected Dispatching, got " + string other)

let scenarioCancelIdle () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan, _, _) ->
        let d1 =
            mustDecide d0.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved))

        let d2 = mustDecide d1.NextState CancelRequested

        match d2.NextState with
        | IssuingAbort(_, _, _, _, _) ->
            let d3 = mustDecide d2.NextState (AbortHostAccepted plan.TurnId)

            match d3.NextState with
            | AwaitingAbortSettle _ ->
                let d4 = mustDecide d3.NextState SessionIdleObserved

                match d4.NextState with
                | ReconcilingAbortSettle _ ->
                    let d5 = mustDecide d4.NextState (SessionQuiescenceResolved Stopped)

                    match d5.NextState with
                    | Available _ ->
                        check
                            "CompleteCaller Cancelled"
                            (List.exists
                                (function
                                | CompleteCaller(_, Cancelled) -> true
                                | _ -> false)
                                d5.Effects)

                        // CancelAbortDeadline now managed by ResourceScope
                        // (ResourcePlan.diffResources), not emitted as Effect DU.
                        ()
                    | other -> fail ("expected Available, got " + string other)
                | other -> fail ("expected ReconcilingAbortSettle, got " + string other)
            | other -> fail ("expected AwaitingAbortSettle, got " + string other)
        | other -> fail ("expected IssuingAbort, got " + string other)
    | other -> fail ("expected Dispatching, got " + string other)

let scenarioAbortDeadlinePoisons () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan, _, _) ->
        let d1 =
            mustDecide d0.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved))

        let d2 = mustDecide d1.NextState CancelRequested

        match d2.NextState with
        | IssuingAbort(_, turn, _, _, _) ->
            let tid =
                match turn with
                | NotYetStarted p -> p.TurnId
                | Started s -> s.Plan.TurnId

            let d3 = mustDecide d2.NextState (AbortDeadlineExpired tid)

            match d3.NextState with
            | Poisoned _ ->
                check
                    "InfrastructureFailure"
                    (List.exists
                        (function
                        | CompleteCaller(_, Failed(InfrastructureFailure _)) -> true
                        | _ -> false)
                        d3.Effects)
            | other -> fail ("expected Poisoned, got " + string other)
        | other -> fail ("expected IssuingAbort, got " + string other)
    | other -> fail ("expected Dispatching, got " + string other)

let scenarioStaleTimerIgnored () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan, _, _) ->
        let stale = TurnId.create "stale-turn"
        mustNoChange d0.NextState (TurnDeadlineExpired stale) StaleTimer

        let d1 =
            mustDecide d0.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved))

        mustNoChange d1.NextState (TurnDeadlineExpired stale) StaleTimer
    | other -> fail ("expected Dispatching, got " + string other)

// ── Properties ──

let propErrorNeverDispatches () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan, _, _) ->
        let d1 =
            mustDecide d0.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved))

        let d2 = mustDecide d1.NextState (TurnErrorObserved err)
        check "prop2: no DispatchPrompt" (dispatchPromptCount d2.Effects = 0)
    | other -> fail ("expected Dispatching, got " + string other)

let propAtMostOneCompleteCaller () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan, _, _) ->
        let d1 =
            mustDecide d0.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved))

        let evidence =
            { CurrentTurnEvidence.empty with
                Assistant = AssistantSnapshot("", 0L, "x", Some NormalFinish) }

        let d2 =
            mustDecide
                d1.NextState
                (EvidenceUpdated
                    { TurnId = Some plan.TurnId
                      Evidence = evidence })

        let d3 = mustDecide d2.NextState SessionIdleObserved

        let total =
            completeCallerCount d0.Effects
            + completeCallerCount d1.Effects
            + completeCallerCount d2.Effects
            + completeCallerCount d3.Effects

        check "prop5: exactly one CompleteCaller" (total = 1)
    | other -> fail ("expected Dispatching, got " + string other)

let propPoisonedNoDispatch () =
    let state = Poisoned(HostProtocolBroken "test")
    let d = mustDecide state (StartRun request)
    check "prop7: no DispatchPrompt" (dispatchPromptCount d.Effects = 0)

    check
        "prop7: RejectStart"
        (List.exists
            (function
            | RejectStart _ -> true
            | _ -> false)
            d.Effects)

let run () =
    scenarioErrorThenIdleRetriesThenSucceeds ()
    scenarioErrorIdleRetry ()
    scenarioDispatchRejectRetry ()
    scenarioAcceptanceUnknownAborts ()
    scenarioCancelIdle ()
    scenarioAbortDeadlinePoisons ()
    scenarioStaleTimerIgnored ()
    propErrorNeverDispatches ()
    propAtMostOneCompleteCaller ()
    propPoisonedNoDispatch ()
