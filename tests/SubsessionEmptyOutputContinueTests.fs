module Wanxiangshu.Tests.SubsessionEmptyOutputContinueTests

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Decision
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Tests.Assert

let private fail (msg: string) = check msg false

let private decide state cmd =
    Wanxiangshu.Kernel.Subsession.Decision.decide 1000000L state cmd

let private model0: FallbackModel =
    { ProviderID = "p"
      ModelID = "m0"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private chain: FallbackChain = [ model0 ]

let private cfg: FallbackConfig =
    { DefaultChain = chain
      AgentChains = Map.empty
      MaxRetries = 1
      LoopMaxContinues = 10
      MaxRecoveries = 3 }

let private sid = SessionId.create "child-continue"
let private parent = SessionId.create "parent-continue"
let private runId = RunId.create "run-continue"
let private turn0 = TurnId.create "run-continue-t0"

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

let private isDispatchPrompt =
    function
    | DispatchPrompt _ -> true
    | _ -> false

let private hasEffect pred (effects: Effect list) = List.exists pred effects

let private mkRunning evidence =
    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    Running(ctx, started, evidence, 1000000L)

let private assertContinues label evidence =
    let state = mkRunning evidence

    match decide state SessionIdleObserved with
    | Ok(Decided d) ->
        match d.NextState with
        | Dispatching _ -> check label (hasEffect isDispatchPrompt d.Effects)
        | Available _ -> fail (label + ": should continue, not fail")
        | other -> fail (label + ": expected Dispatching, got " + string other)
    | other -> fail (label + ": unexpected " + string other)

/// Regression: coder subagent that only thinks (no visible text, no tool call)
/// must trigger a zero-width continue — NOT fail with RecoveryExhausted.
/// Mirrors main-session isIdleNoContentAndNoTools → EmptyOutputError → SendContinue.
let emptyAssistantIdleContinues () =
    assertContinues
        "empty assistant continues"
        { CurrentTurnEvidence.empty with
            Assistant = EmptyAssistant }

let noAssistantIdleContinues () =
    assertContinues
        "no assistant continues"
        { CurrentTurnEvidence.empty with
            Assistant = NoAssistant }

let blankTextIdleContinues () =
    assertContinues
        "blank text continues"
        { CurrentTurnEvidence.empty with
            Assistant = AssistantSnapshot("", 0L, "   ") }

let drainingIdleSuccess_resetsPolicyContinueCount () =
    let policyWithContinue = { policy0 with ContinueCount = 2 }
    let ctx = mkCtx policyWithContinue (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"
    let started = { Plan = plan; StartReceipt = OrderedTurnMarkerObserved }
    let err = { ErrorName = "err"; DomainError = None; Message = "transient"; StatusCode = None; IsRetryable = Some true }
    let evidence = { CurrentTurnEvidence.empty with Assistant = AssistantSnapshot("", 0L, "Valid output text") }
    let state = Draining(ctx, started, err, evidence, 1000000L)

    match decide state SessionIdleObserved with
    | Ok(Decided d) ->
        match d.NextState with
        | Available _ -> check "draining idle success state is Available" true
        | other -> fail ("expected Available, got " + string other)
    | other -> fail ("unexpected " + string other)

let run () =
    emptyAssistantIdleContinues ()
    noAssistantIdleContinues ()
    blankTextIdleContinues ()
    drainingIdleSuccess_resetsPolicyContinueCount ()
