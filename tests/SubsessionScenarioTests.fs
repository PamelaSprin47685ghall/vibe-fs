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
      MaxRetries = 0 // force model switch on first retryable error after idle
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
      Chain = chain }

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

/// TaskComplete → idle → success
let scenarioTaskCompleteIdleSuccess () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    let state1 =
        match d0.NextState with
        | Dispatching(ctx, plan) ->
            let d1 =
                mustDecide d0.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved))

            match d1.NextState with
            | Running(ctx2, started) ->
                let d2 = mustDecide d1.NextState (TaskCompleteObserved "hello")

                match d2.NextState with
                | Draining _ ->
                    check "no CompleteCaller after task_complete" (completeCallerCount d2.Effects = 0)
                    let d3 = mustDecide d2.NextState SessionIdleObserved

                    match d3.NextState with
                    | Available _ ->
                        check
                            "CompleteCaller Succeeded once"
                            (completeCallerCount d3.Effects = 1
                             && List.exists
                                 (function
                                 | CompleteCaller(_, Succeeded "hello") -> true
                                 | _ -> false)
                                 d3.Effects)
                    | other -> fail ("expected Available, got " + string other)
                | other -> fail ("expected Draining, got " + string other)
            | other -> fail ("expected Running, got " + string other)
        | other -> fail ("expected Dispatching, got " + string other)

    ignore state1

/// Error → TaskComplete → idle → success (task_complete wins)
let scenarioErrorThenTaskComplete () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan) ->
        let d1 =
            mustDecide d0.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved))

        let d2 = mustDecide d1.NextState (TurnErrorObserved err)
        let d3 = mustDecide d2.NextState (TaskCompleteObserved "won")
        let d4 = mustDecide d3.NextState SessionIdleObserved

        match d4.NextState with
        | Available _ ->
            check
                "success after error+task_complete"
                (List.exists
                    (function
                    | CompleteCaller(_, Succeeded "won") -> true
                    | _ -> false)
                    d4.Effects)
        | other -> fail ("expected Available, got " + string other)
    | other -> fail ("expected Dispatching, got " + string other)

/// Error → idle → retry DispatchPrompt (no CompleteCaller on error alone)
let scenarioErrorIdleRetry () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan) ->
        let d1 =
            mustDecide d0.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved))

        let d2 = mustDecide d1.NextState (TurnErrorObserved err)
        check "no DispatchPrompt on error" (dispatchPromptCount d2.Effects = 0)

        let d3 = mustDecide d2.NextState SessionIdleObserved

        match d3.NextState with
        | Dispatching _ ->
            check "retry DispatchPrompt after idle" (dispatchPromptCount d3.Effects = 1)
            check "no CompleteCaller on retry path" (completeCallerCount d3.Effects = 0)
        | Available _ ->
            // Exhausted chain is also valid if policy stops
            check "CompleteCaller Failed" (completeCallerCount d3.Effects = 1)
        | other -> fail ("unexpected after error idle: " + string other)
    | other -> fail ("expected Dispatching, got " + string other)

/// Dispatch reject → next turn without waiting idle
let scenarioDispatchRejectRetry () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan) ->
        let d1 = mustDecide d0.NextState (DispatchRejected(plan.TurnId, err))

        match d1.NextState with
        | Dispatching _ -> check "DispatchPrompt without idle" (dispatchPromptCount d1.Effects = 1)
        | Available _ -> check "or CompleteCaller Failed" (completeCallerCount d1.Effects = 1)
        | other -> fail ("unexpected: " + string other)
    | other -> fail ("expected Dispatching, got " + string other)

/// Cancel → Aborting → idle → Cancelled
let scenarioCancelIdle () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan) ->
        let d1 =
            mustDecide d0.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved))

        let d2 = mustDecide d1.NextState CancelRequested

        match d2.NextState with
        | Aborting _ ->
            let d3 = mustDecide d2.NextState SessionIdleObserved

            match d3.NextState with
            | Available _ ->
                check
                    "CompleteCaller Cancelled"
                    (List.exists
                        (function
                        | CompleteCaller(_, Cancelled) -> true
                        | _ -> false)
                        d3.Effects)
            | other -> fail ("expected Available, got " + string other)
        | other -> fail ("expected Aborting, got " + string other)
    | other -> fail ("expected Dispatching, got " + string other)

/// Abort deadline → Poisoned + InfrastructureFailure
let scenarioAbortDeadlinePoisons () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan) ->
        let d1 =
            mustDecide d0.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved))

        let d2 = mustDecide d1.NextState CancelRequested

        match d2.NextState with
        | Aborting(_, turn, _) ->
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
        | other -> fail ("expected Aborting, got " + string other)
    | other -> fail ("expected Dispatching, got " + string other)

/// Stale timer does not affect new turn
let scenarioStaleTimerIgnored () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan) ->
        let stale = TurnId.create "stale-turn"
        mustNoChange d0.NextState (TurnDeadlineExpired stale) StaleTimer

        let d1 =
            mustDecide d0.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved))

        mustNoChange d1.NextState (TurnDeadlineExpired stale) StaleTimer
    | other -> fail ("expected Dispatching, got " + string other)

// ── Properties ──

/// Property 2: TurnErrorObserved never directly produces DispatchPrompt
let propErrorNeverDispatches () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan) ->
        let d1 =
            mustDecide d0.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved))

        let d2 = mustDecide d1.NextState (TurnErrorObserved err)
        check "prop2: no DispatchPrompt" (dispatchPromptCount d2.Effects = 0)
    | other -> fail ("expected Dispatching, got " + string other)

/// Property 3: TaskCompleteObserved never directly CompleteCaller
let propTaskCompleteNeverCompletes () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan) ->
        let d1 =
            mustDecide d0.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved))

        let d2 = mustDecide d1.NextState (TaskCompleteObserved "x")
        check "prop3: no CompleteCaller" (completeCallerCount d2.Effects = 0)
    | other -> fail ("expected Dispatching, got " + string other)

/// Property 5: any RunId at most one CompleteCaller across a success path
let propAtMostOneCompleteCaller () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan) ->
        let d1 =
            mustDecide d0.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved))

        let d2 = mustDecide d1.NextState (TaskCompleteObserved "x")
        let d3 = mustDecide d2.NextState SessionIdleObserved

        let total =
            completeCallerCount d0.Effects
            + completeCallerCount d1.Effects
            + completeCallerCount d2.Effects
            + completeCallerCount d3.Effects

        check "prop5: exactly one CompleteCaller" (total = 1)
    | other -> fail ("expected Dispatching, got " + string other)

/// Property 7: Poisoned never DispatchPrompt
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
    scenarioTaskCompleteIdleSuccess ()
    scenarioErrorThenTaskComplete ()
    scenarioErrorIdleRetry ()
    scenarioDispatchRejectRetry ()
    scenarioCancelIdle ()
    scenarioAbortDeadlinePoisons ()
    scenarioStaleTimerIgnored ()
    propErrorNeverDispatches ()
    propTaskCompleteNeverCompletes ()
    propAtMostOneCompleteCaller ()
    propPoisonedNoDispatch ()
