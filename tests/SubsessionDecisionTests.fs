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
      Chain = chain }

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
      Model = model
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

let private isRejectStart =
    function
    | RejectStart _ -> true
    | _ -> false

let private isReadTranscript =
    function
    | ReadTranscript _ -> true
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
    let state = Dispatching(ctx, plan)

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
    let state = Dispatching(ctx, plan)

    match decide state SessionIdleObserved with
    | Ok(NoChange DuplicateIdleBeforeTurnMarker) -> ()
    | other -> fail ("expected NoChange DuplicateIdleBeforeTurnMarker, got " + string other)

let runningErrorDrains () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let state = Running(ctx, started)

    match decide state (TurnErrorObserved err) with
    | Ok(Decided d) ->
        match d.NextState with
        | Draining(_, _, FailureObserved _) -> ()
        | other -> fail ("expected Draining FailureObserved, got " + string other)

        check "no DispatchPrompt on error" (not (hasEffect isDispatchPrompt d.Effects))
        check "no CompleteCaller on error" (not (hasEffect isCompleteCaller d.Effects))
    | other -> fail ("unexpected: " + string other)

let taskCompleteDoesNotCompleteCaller () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let state = Running(ctx, started)

    match decide state (TaskCompleteObserved "out") with
    | Ok(Decided d) ->
        match d.NextState with
        | Draining(_, _, CompletionRequested "out") -> ()
        | other -> fail ("expected Draining CompletionRequested, got " + string other)

        check "no CompleteCaller before idle" (not (hasEffect isCompleteCaller d.Effects))
    | other -> fail ("unexpected: " + string other)

let completionWinsOverError () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let state = Draining(ctx, started, FailureObserved err)

    match decide state (TaskCompleteObserved "out") with
    | Ok(Decided d) ->
        match d.NextState with
        | Draining(_, _, CompletionRequested "out") -> ()
        | other -> fail ("expected CompletionRequested wins, got " + string other)
    | other -> fail ("unexpected: " + string other)

let errorAfterCompletionIgnored () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let state = Draining(ctx, started, CompletionRequested "out")

    match decide state (TurnErrorObserved err) with
    | Ok(NoChange CompletionAlreadyWins) -> ()
    | other -> fail ("expected CompletionAlreadyWins, got " + string other)

let drainingCompletionIdleSucceeds () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let state = Draining(ctx, started, CompletionRequested "out")

    match decide state SessionIdleObserved with
    | Ok(Decided d) ->
        match d.NextState with
        | Available _ -> ()
        | other -> fail ("expected Available, got " + string other)

        check
            "CompleteCaller Succeeded"
            (hasEffect
                (function
                | CompleteCaller(_, Succeeded "out") -> true
                | _ -> false)
                d.Effects)

        check "no DispatchPrompt" (not (hasEffect isDispatchPrompt d.Effects))
    | other -> fail ("unexpected: " + string other)

let runningIdleReadsTranscript () =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let state = Running(ctx, started)

    match decide state SessionIdleObserved with
    | Ok(Decided d) ->
        match d.NextState with
        | InspectingTranscript _ -> ()
        | other -> fail ("expected InspectingTranscript, got " + string other)

        check "ReadTranscript effect" (hasEffect isReadTranscript d.Effects)
        check "no CompleteCaller yet" (not (hasEffect isCompleteCaller d.Effects))
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

let classifyTranscriptTodosComplete () =
    let snap =
        { AllTodosCompleted = true
          ToolCallAsTextRecoveryPrompt = None
          LastAssistantToolFinish = false
          HasToolResultAfterLastAssistant = false
          LastAssistantText = "done" }

    match classifyTranscript snap with
    | CompleteNaturally "done" -> ()
    | other -> fail ("expected CompleteNaturally, got " + string other)

let classifyTranscriptToolCallAsText () =
    let snap =
        { AllTodosCompleted = false
          ToolCallAsTextRecoveryPrompt = Some "retry tools"
          LastAssistantToolFinish = false
          HasToolResultAfterLastAssistant = false
          LastAssistantText = "raw xml" }

    match classifyTranscript snap with
    | RecoverWithPrompt "retry tools" -> ()
    | other -> fail ("expected RecoverWithPrompt, got " + string other)

// ── Entry ──

let run () =
    startRunFromAvailable ()
    secondStartRunRejected ()
    dispatchingIdleIgnored ()
    runningErrorDrains ()
    taskCompleteDoesNotCompleteCaller ()
    completionWinsOverError ()
    errorAfterCompletionIgnored ()
    drainingCompletionIdleSucceeds ()
    runningIdleReadsTranscript ()
    poisonedRejectsStart ()
    classifyTranscriptTodosComplete ()
    classifyTranscriptToolCallAsText ()
