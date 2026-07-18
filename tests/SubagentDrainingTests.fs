module Wanxiangshu.Tests.SubagentDrainingTests

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.DecisionObserve
open Wanxiangshu.Kernel.Subsession.TranscriptDecision
open Wanxiangshu.Tests.Assert

let private fail (msg: string) = check msg false

// ── Draining / handleDrainingIdle: CompletionRequested must not overwrite assistant text ──

/// Minimal RunContext usable by decideRunning / handleDrainingIdle.
let private makeRunContext () : RunContext =
    let sentinel =
        { ProviderID = ""
          ModelID = ""
          Variant = None
          Temperature = None
          TopP = None
          MaxTokens = None
          ReasoningEffort = None
          Thinking = false }

    { RunId = RunId.create "run-drain-test"
      ParentSessionId = SessionId.create "parent-drain"
      SessionId = SessionId.create "session-drain"
      Policy =
        { Selection = StableAt 0
          FailureCount = 0
          ContinueCount = 0
          RecoveryCount = 0 }
      FallbackConfig =
        { DefaultChain = []
          AgentChains = Map.empty
          MaxRetries = 3
          LoopMaxContinues = 3
          MaxRecoveries = 3 }
      Chain = []
      NextTurnOrdinal = TurnOrdinal.first }

/// Minimal StartedTurn.
let private makeStartedTurn () : StartedTurn =
    { Plan =
        { TurnId = TurnId.create "turn-drain-1"
          Ordinal = TurnOrdinal.first
          Model = None
          Prompt = "drain test" }
      StartReceipt = UserMessageObserved "msg-1" }

/// TurnErrorObserved that does NOT carry CompletionRequested outcome.
let private turnErrorObserved: Command =
    TurnErrorObserved
        { ErrorName = "TestError"
          DomainError = None
          Message = "simulated turn error"
          StatusCode = None
          IsRetryable = Some true }

/// Error input stored inside the Draining state — must match what handleDrainingIdle reads.
let private drainError: ErrorInput =
    { ErrorName = "TestError"
      DomainError = None
      Message = "simulated turn error"
      StatusCode = None
      IsRetryable = Some true }

/// Step 3: Draining + SessionIdleObserved → handleDrainingIdle fires succeedRun.
/// Bug: succeedRun uses evidence.Outcome = CompletionRequested "" → returns empty.
/// Correct: should return assistant text from merged evidence.
let private step3_idleObserved
    (ctx: RunContext)
    (started: StartedTurn)
    (error: ErrorInput)
    (mergedEvidence: CurrentTurnEvidence)
    =
    let r3 = decide (Draining(ctx, started, error, mergedEvidence)) SessionIdleObserved

    match r3 with
    | Ok(Decided decided3) ->
        match decided3.NextState with
        | Available _ ->
            match decided3.Effects with
            | [ CompleteCaller(_, Succeeded output) ] ->
                check "output is non-empty assistant text, not empty tool marker" (output <> "")
                equal "output is assistant text not CompletionRequested empty" "drain result" output
            | other -> fail ("expected [CompleteCaller Succeeded], got " + string other)
        | other -> fail ("expected Available after SessionIdleObserved, got " + string other)
    | other -> fail ("expected Decided after SessionIdleObserved, got " + string other)

/// Full state-machine path: Running → TurnErrorObserved → Draining →
/// EvidenceUpdated (CompletionRequested "" + non-empty AssistantSnapshot) →
/// SessionIdleObserved → handleDrainingIdle.
///
/// Bug: handleDrainingIdle checks evidence.Outcome = CompletionRequested "" first
/// and calls succeedRun with the empty marker, discarding the real assistant text.
/// The correct behaviour is to return the assistant text.
let drainingIdle_completionRequestedEmptyDoesNotOverwriteAssistantText () =
    // Step 1: Running + TurnErrorObserved → Draining
    let runningState =
        Running(makeRunContext (), makeStartedTurn (), CurrentTurnEvidence.empty)

    let r1 = decide runningState turnErrorObserved

    match r1 with
    | Ok(Decided decided1) ->
        match decided1.NextState with
        | Draining(ctx, started, error, _) ->
            // Step 2: EvidenceUpdated merges CompletionRequested "" + assistant snapshot
            let evUpdate =
                { TurnId = Some(TurnId.create "turn-drain-1")
                  Evidence =
                    { CurrentTurnEvidence.empty with
                        Outcome = CompletionRequested ""
                        Assistant = AssistantSnapshot("", 0L, "drain result", Some NormalFinish) } }

            let r2 =
                decide (Draining(ctx, started, error, CurrentTurnEvidence.empty)) (EvidenceUpdated evUpdate)

            match r2 with
            | Ok(Decided decided2) ->
                match decided2.NextState with
                | Draining(ctx2, started2, error2, mergedEvidence) ->
                    let hasMarker =
                        match mergedEvidence.Outcome with
                        | CompletionRequested _ -> true
                        | _ -> false

                    let hasRealText =
                        match mergedEvidence.Assistant with
                        | AssistantSnapshot(_, _, text, _) -> not (System.String.IsNullOrWhiteSpace text)
                        | _ -> false

                    check "merged evidence retains CompletionRequested" hasMarker
                    check "merged evidence retains non-empty assistant text" hasRealText

                    // Step 3: verify handleDrainingIdle returns assistant text
                    step3_idleObserved ctx2 started2 drainError mergedEvidence
                | other -> fail ("expected Draining after EvidenceUpdated, got " + string other)
            | other -> fail ("expected Decided after EvidenceUpdated, got " + string other)
        | other -> fail ("expected Draining after TurnErrorObserved, got " + string other)
    | other -> fail ("expected Decided after TurnErrorObserved, got " + string other)

let run () =
    drainingIdle_completionRequestedEmptyDoesNotOverwriteAssistantText ()
