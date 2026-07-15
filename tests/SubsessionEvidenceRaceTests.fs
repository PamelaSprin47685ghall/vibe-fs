module Wanxiangshu.Tests.SubsessionEvidenceRaceTests

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Decision
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Tests.Assert

let private fail (msg: string) = check msg false

let private model0: FallbackModel =
    { ProviderID = "p"
      ModelID = "m0"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private cfg: FallbackConfig =
    { DefaultChain = [ model0 ]
      AgentChains = Map.empty
      MaxRetries = 1
      LoopMaxContinues = 10
      MaxRecoveries = 3 }

let private sid = SessionId.create "child-evidence-race"
let private parent = SessionId.create "parent-evidence-race"
let private runId = RunId.create "run-evidence-race"

let private request: StartRunRequest =
    { RunId = runId
      SessionId = sid
      ParentSessionId = parent
      Prompt = "investigate README"
      FallbackConfig = cfg
      Directive = RetryChain [ model0 ]
      InitiallyCancelled = false }

let private mustDecide state cmd =
    match decide state cmd with
    | Ok(Decided d) -> d
    | Ok(NoChange r) -> failwith ("unexpected NoChange: " + string r)
    | Error e -> failwith ("decision error: " + string e)

/// Reproduces the real-world race behind "invalid run for tool 'subagent':
/// No assistant message in current turn" for investigator/coder/browser/meditator.
///
/// Host truth: `session.prompt` resolving (→ DispatchAccepted) and the host's
/// event bus delivering `message.updated` (role=assistant) (→ EvidenceUpdated)
/// are two INDEPENDENT async chains. Nothing orders one before the other. A
/// fast provider (or a mock LLM in e2e) can deliver the assistant's full text
/// reply BEFORE the prompt HTTP call itself resolves. That reply's evidence
/// arrives while the actor is still `Dispatching`, i.e. BEFORE `Running` even
/// exists to hold it.
///
/// This is the "premature evidence" counterpart to the already-tested
/// "premature idle" scenario (SubsessionDecisionTests.idleDuringDispatchingThenRealIdleConverges).
/// That test protects against a false TERMINATION signal arriving early.
/// This test protects against a true CONTENT signal arriving early — the
/// opposite direction of the same invariant: no fact who is destined for the
/// current turn may be silently destroyed by its arrival time.
let evidenceDuringDispatchingMustSurviveIntoRunning () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan, _) ->
        let earlyEvidence =
            { CurrentTurnEvidence.empty with
                Assistant = AssistantSnapshot("", 0L, "investigator report: README found at docs/", Some NormalFinish) }

        // The assistant's full reply lands via message.updated BEFORE the host
        // has confirmed acceptance of our own prompt call.
        match
            decide
                d0.NextState
                (EvidenceUpdated
                    { TurnId = Some plan.TurnId
                      Evidence = earlyEvidence })
        with
        | Ok(Decided d1) ->
            match d1.NextState with
            | Dispatching _ -> check "premature evidence decision is Decided, not silently ignored" true
            | other -> fail ("expected to remain Dispatching (evidence buffered), got " + string other)

            // Now the late confirmation that the host actually received our
            // prompt arrives — this must carry the buffered evidence forward.
            match decide d1.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved)) with
            | Ok(Decided d2) ->
                match d2.NextState with
                | Running(_, _, evidence) ->
                    match evidence.Assistant with
                    | AssistantSnapshot(_, _, text, _) ->
                        equal
                            "assistant evidence collected before acceptance is preserved into Running"
                            "investigator report: README found at docs/"
                            text
                    | NoAssistant -> fail "premature evidence was destroyed on the Dispatching→Running transition"
                    | EmptyAssistant -> fail "premature evidence was replaced with EmptyAssistant"
                    | AssistantDelta _ -> fail "premature evidence was a delta"
                | other -> fail ("expected Running with preserved evidence, got " + string other)

                // Idle now arrives. The subagent must be recognized as having
                // produced real output — NOT rejected as "No assistant message
                // in current turn" (Decision.fs classifyTurnEvidence NoAssistant path).
                match decide d2.NextState SessionIdleObserved with
                | Ok(Decided d3) ->
                    match d3.NextState with
                    | Available _ ->
                        let succeededWithReport =
                            d3.Effects
                            |> List.exists (function
                                | CompleteCaller(_, Succeeded text) ->
                                    text.Contains "investigator report: README found at docs/"
                                | _ -> false)

                        check "subagent run succeeds naturally instead of RecoveryExhausted" succeededWithReport
                    | other -> fail ("expected Available (converged to success), got " + string other)
                | other -> fail ("unexpected on idle after preserved evidence: " + string other)
            | Ok(NoChange r) -> fail ("DispatchAccepted must not be a NoChange, got " + string r)
            | Error e -> fail ("decision error on DispatchAccepted: " + string e)
        | Ok(NoChange _) -> fail "premature EvidenceUpdated while Dispatching must not be unconditionally discarded"
        | Error e -> fail ("decision error on premature EvidenceUpdated: " + string e)
    | other -> fail ("expected Dispatching after StartRun, got " + string other)

/// Same race, but the assistant's early reply is a bare tool-call finish with
/// no text yet — only a HasToolResult signal lands early. Must still merge,
/// not vanish, once Running exists.
let toolResultDuringDispatchingMustSurviveIntoRunning () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching(_, plan, _) ->
        let earlyToolEvidence =
            { CurrentTurnEvidence.empty with
                Tool = HasToolResult }

        match
            decide
                d0.NextState
                (EvidenceUpdated
                    { TurnId = Some plan.TurnId
                      Evidence = earlyToolEvidence })
        with
        | Ok(Decided d1) ->
            match decide d1.NextState (DispatchAccepted(plan.TurnId, OrderedTurnMarkerObserved)) with
            | Ok(Decided d2) ->
                match d2.NextState with
                | Running(_, _, evidence) ->
                    equal "tool result observed before acceptance survives" HasToolResult evidence.Tool
                | other -> fail ("expected Running, got " + string other)
            | other -> fail ("unexpected on DispatchAccepted: " + string other)
        | other -> fail ("unexpected on premature tool EvidenceUpdated: " + string other)
    | other -> fail ("expected Dispatching, got " + string other)

/// Mismatched turnId while Dispatching (evidence for a DIFFERENT, stale turn)
/// must still be rejected — buffering is scoped to the current plan only.
let mismatchedTurnEvidenceDuringDispatchingIsRejected () =
    let d0 = mustDecide (Available { SessionId = sid }) (StartRun request)

    match d0.NextState with
    | Dispatching _ ->
        let staleTurn = TurnId.create "some-other-stale-turn"

        let evidence =
            { CurrentTurnEvidence.empty with
                Assistant = AssistantSnapshot("", 0L, "stale", Some NormalFinish) }

        match
            decide
                d0.NextState
                (EvidenceUpdated
                    { TurnId = Some staleTurn
                      Evidence = evidence })
        with
        | Ok(NoChange StaleTurnMarker) -> check "mismatched turn evidence correctly rejected" true
        | other -> fail ("expected NoChange StaleTurnMarker for mismatched turn, got " + string other)
    | other -> fail ("expected Dispatching, got " + string other)

let run () =
    evidenceDuringDispatchingMustSurviveIntoRunning ()
    toolResultDuringDispatchingMustSurviveIntoRunning ()
    mismatchedTurnEvidenceDuringDispatchingIsRejected ()
