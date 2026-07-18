module Wanxiangshu.Tests.ContextBudgetIntegrationTests

open Wanxiangshu.Runtime.MessageTransform.ContextBudgetEngine
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.Pipeline
open Wanxiangshu.Runtime.ContextBudgetUsageCodec
open Wanxiangshu.Tests.ContextBudgetRealApiSpecs

// ── helpers ──────────────────────────────────────────────────────────────────

let private mkTestPlan (sessionID: string) maxTokens usage =
    { SessionID = sessionID
      Agent = "main"
      Directory = ""
      ProjectionPolicy = ProjectionPolicy.IncludeProjection
      BacklogProjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include
      CapsInjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Include
      ParallelHintPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Include
      ContextBudgetPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Include
      IsSubagentSession = false
      Cleaned = []
      RawArray = None
      SembleInjectEnabled = false
      Scope = RuntimeScope.create ()
      MaxInputTokens = maxTokens
      ModelKey = "integration-test:model"
      LimitSource = "openai-session-model"
      ObserveLatestUsage =
        fun () ->
            Promise.lift (
                Some
                    { AssistantMessageID = "test"
                      InputTokens = usage }
            ) }

let private mkBacklogOps =
    { Host = opencode
      GetOrRebuildBacklog = fun _ _ -> [] }

let private mkTestState scope sessionID =
    let s = beginPhase 30000L 100L 0L
    ContextBudgetStore.update scope sessionID (fun e -> { e with State = Some s })
    s

let private mkMsgInfo sessionID =
    { id = "user-1"
      sessionID = sessionID
      role = User
      agent = ""
      isError = false
      toolName = ""
      details = null
      time = null }

let private mkMessages (info: MessageInfo<obj>) =
    [ { info = info
        parts = []
        source = Native
        raw = null } ]

// staging-specific helpers

let private mkStagedEncodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray

let private mkStagedInjectFn
    (payloadBytes: int)
    (_: Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy)
    (arr: obj array)
    =
    promise { return Array.append arr [| box (String.replicate payloadBytes "s") |] }

let private mkStagedBuildCaps (payloadBytes: int) (arr: obj array) _ _ =
    Array.append arr [| box (String.replicate payloadBytes "c") |]

let private mkStagedLoadCaps () = promise { return [] }

// ── specs ────────────────────────────────────────────────────────────────────

let spec_runMessageTransformPipeline_nudge () =
    promise {
        let sessionID = "sess-pipeline-nudge"
        let msgInfo = mkMsgInfo sessionID
        let messages = mkMessages msgInfo

        let plan =
            { (mkTestPlan sessionID 200000 120000) with
                Cleaned = messages }

        mkTestState plan.Scope sessionID |> ignore

        let! res =
            runMessageTransformPipeline
                plan
                mkBacklogOps
                mkStagedEncodeMessages
                (mkStagedInjectFn 0)
                mkStagedLoadCaps
                (mkStagedBuildCaps 0)

        equal "pipeline should retain ordinary stages and nudge" 4 res.Length
        check "pipeline output is non-empty" (res.Length > 0)
    }

/// RED: tryExtractMaxInputTokens must read model.limit.context/input
let spec_tryExtractMaxInputTokens_realSchema () =
    let mkLimit ctx inp outOpt =
        let fields =
            [ "context", box ctx; "output", box outOpt ]
            @ (match inp with
               | Some i -> [ "input", box i ]
               | None -> [])

        createObj fields

    let t1 =
        createObj [ "session", createObj [ "model", createObj [ "limit", mkLimit 128000 (Some 200000) 8000 ] ] ]

    equal "extract limit.input" (Some 200000) (tryExtractMaxInputTokens t1)

    let t2 =
        createObj [ "session", createObj [ "model", createObj [ "limit", mkLimit 128000 None 8000 ] ] ]

    equal "extract limit.context (no input)" (Some 128000) (tryExtractMaxInputTokens t2)

    let t3 = createObj []
    equal "empty obj → None" None (tryExtractMaxInputTokens t3)

    let t4 =
        createObj
            [ "client", createObj [ "session", createObj [ "model", createObj [ "limit", mkLimit 100000 None 4000 ] ] ] ]

    equal "extract client.session.model.limit.context" (Some 100000) (tryExtractMaxInputTokens t4)

/// staging helper: plan with 150 k-token budget and 120 k-token usage record
let private mkStagedPlan (sessionID: string) =
    { SessionID = sessionID
      Agent = "main"
      Directory = ""
      ProjectionPolicy = ProjectionPolicy.IncludeProjection
      BacklogProjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include
      CapsInjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Include
      ParallelHintPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Include
      ContextBudgetPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Include
      IsSubagentSession = false
      Cleaned = []
      RawArray = None
      SembleInjectEnabled = false
      Scope = RuntimeScope.create ()
      MaxInputTokens = 150000
      ModelKey = "integration-test:model"
      LimitSource = "openai-session-model"
      ObserveLatestUsage =
        fun () ->
            Promise.lift (
                Some
                    { AssistantMessageID = "test"
                      InputTokens = 120000L }
            ) }

/// RED: budget must see final outbound bytes after parallel-hint, injectFn, and CAPS prepend.
///
/// Pipeline ordering (Pipeline.fs):
///   line  86  encodeMessages afterBacklog            → encodedBacklog
///   line  92  applyContextBudget … encodedBacklogSlot → budget check ← MEASURES PRE-INJECTION SIZE
///   line  94  tryInjectParallelToolPrompt             → +1 Synthetic msg
///   line 117  injectFn encodedWithTopSlot             → +1 Synthetic element
///   line 118  prepenCapsWithState … injected …        → +1 Synthetic element
///
/// Budget math:
///   totalBytes at line 92 = utf8JsonBytes(encodedBacklogSlot) — pre-injection only
///   When LastCalibration=None + no observation → degraded: tokens = totalBytes / 2
///   Nudge fires when currentTokens > phaseBaseTokens (30 000 in this test)
///   Threshold: 2 × 30 000 = 60 000 bytes  → pre-injection ≤ 59 999 → no nudge
///
/// Payload sizing:
///   A single user message whose JSON-encoded size = 60 220 bytes (pre-injection)
///   60 220 / 2 = 30 110 > 30 000 → nudge fires at budget check (line 92) → WRONG MEASUREMENT
///   With injectFn (10k) + buildCaps (10k): final encoded ≈ 80 220 bytes
///   Calibrated: 120 000 × 80 220 / 100 000 = 96 264 tokens → nudge should fire → CORRECT MEASUREMENT
///
/// Current production code measures 60 220 bytes (pre-injection) at line 92,
/// NOT 80 220 bytes (post-injection). The nudge decision is made too early.
/// This test fails because resDirect (pre-injection budget) != resFull (post-injection budget).
let spec_applyContextBudget_mustSeeFinalOutboundAfterAllStages () =
    promise {
        // Each ASCII character becomes 2 bytes in JSON string, plus the JSON
        // wrapper for Message + MessageInfo + parts array ≈ 220 bytes overhead.
        // 60 000 "a" chars → 60 000 JSON string bytes + ≈220 overhead ≈ 60 220 total.
        let sessionID = "sess-final-outbound-budget"
        let msgInfo = mkMsgInfo sessionID
        let textPart = String.replicate 60_000 "a"

        let largeMsg =
            { msgInfo with role = User }
            |> fun info ->
                { info = info
                  parts = [ TextPart textPart ]
                  source = Native
                  raw = null }

        let messages = [ largeMsg ]

        let plan =
            { mkStagedPlan sessionID with
                Cleaned = messages }

        mkTestState plan.Scope sessionID |> ignore

        // injectFn adds exactly 10 000 bytes per slot
        let injectFn = mkStagedInjectFn 10_000
        // buildCaps adds exactly 10 000 bytes per slot
        let buildCaps = mkStagedBuildCaps 10_000

        // Direct: applyContextBudget measures pre-injection size = encodedBacklogSlot
        // pre-injection ≈ 60 220 bytes → degraded tokens = 60 220 / 2 = 30 110 > 30 000 → nudge fires
        let! resDirect = applyContextBudget plan mkBacklogOps messages [||]

        // resDirect contains: user + budget-nudge  → Length = 2
        equal "direct: budget nudge fires based on pre-injection size (≈60k bytes)" 2 resDirect.Length

        // Full pipeline: encodedWithTopSlot = encodedBacklogSlot (≈60k)
        //   → injectFn adds 10k → encodedWithTopSlot + inject = ≈70k
        //   → buildCaps adds 10k → final ≈ 80k
        //   calibrated tokens = 120 000 × 80 220 / 100 000 ≈ 96k > 30k → nudge MUST fire
        //   Budget measured only 60k at line 92 → missed the extra 20k → nudge absent → Length = 3
        let! resFull =
            runMessageTransformPipeline plan mkBacklogOps mkStagedEncodeMessages injectFn mkStagedLoadCaps buildCaps

        // BUG: resDirect has nudge (Length=2), resFull has no nudge (Length=3) — they disagree.
        // The full pipeline output is 3 because budget at line 92 measured 60k (pre-injection)
        // instead of 80k (post-injection), so it added no nudge despite final size warranting one.
        check
            "full pipeline output differs from direct budget: budget measured pre-injection bytes only"
            (resDirect.Length <> resFull.Length)
    }

let run () : JS.Promise<unit> =
    promise {
        do! spec_runMessageTransformPipeline_nudge ()
        do! spec_applyContextBudget_mustSeeFinalOutboundAfterAllStages ()
        spec_tryExtractMaxInputTokens_realSchema ()
        do! ContextBudgetRealApiSpecs.run ()
    }
