module Wanxiangshu.Tests.ContextBudgetPipelineNudgeTests

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

// ── helpers (staging only, not shared with other test files) ────────────────────

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

/// plan with 150 k-token budget and 120 k-token usage record
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
      ModelKey = "openai/gpt-4o:default"
      LimitSource = "openai-session-model"
      ObserveLatestUsage =
        fun () ->
            Promise.lift (
                Some
                    { AssistantMessageID = "test"
                      InputTokens = 120000L }
            ) }

// ── RED: budget must measure final outbound after all ordinary stages ───────────

/// Pipeline ordering (Pipeline.fs):
///   line  86  encodeMessages afterBacklog            → encodedBacklog
///   line  92  applyContextBudget … encodedBacklogSlot → budget check ← PRE-INJECTION SIZE
///   line  94  tryInjectParallelToolPrompt             → +1 Synthetic msg
///   line 117  injectFn encodedWithTopSlot             → +1 Synthetic element
///   line 118  prependCapsWithState … injected …       → +1 Synthetic element
///
/// Budget math:
///   totalBytes at line 92 = utf8JsonBytes(encodedBacklogSlot) — pre-injection only
///   LastCalibration=None + no observation → degraded: tokens = totalBytes / 2
///   Nudge fires when currentTokens > phaseBaseTokens (30 000 from mkTestState)
///   Pre-injection threshold: 2 × 30 000 = 60 000 bytes
///
/// Payload sizing:
///   60 000 "a" chars → JSON string ≈ 60 000 + ≈220 overhead ≈ 60 220 pre-injection bytes
///   60 220 / 2 = 30 110 > 30 000 → nudge fires at budget check (line 92) → pre-injection path nudge
///   injectFn 10k + buildCaps 10k → final ≈ 80 220 bytes
///   Calibrated: 120 000 × 80 220 / 100 000 ≈ 96 264 → nudge SHOULD fire on final size
///
/// Current production: budget at line 92 measures 60 220 bytes (pre-injection),
/// NOT 80 220 bytes (post-injection). The nudge decision is made too early.
let spec_applyContextBudget_mustSeeFinalOutboundAfterAllStages () =
    promise {
        let sessionID = "sess-final-outbound-budget"
        let msgInfo = mkMsgInfo sessionID
        // 60 000 ASCII chars → JSON string value ≈ 60 000 bytes + ≈220 msg wrapper ≈ 60 220 total
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

        let injectFn = mkStagedInjectFn 10_000
        let buildCaps = mkStagedBuildCaps 10_000

        // Direct: applyContextBudget sees only encodedBacklogSlot (pre-injection)
        // 60 220 bytes / 2 = 30 110 degraded tokens > 30 000 phaseBase → nudge fires → Length = 2
        let! resDirect = applyContextBudget plan mkBacklogOps messages [||]
        equal "direct: nudge fires on pre-injection bytes (≈60k / 2 ≈ 30k tokens)" 2 resDirect.Length

        // Full pipeline: final outbound = encodedBacklogSlot (60k) + inject (10k) + caps (10k) ≈ 80k
        // calibrated tokens ≈ 96k → nudge SHOULD fire → Length = 2
        // Bug: budget at line 92 only sees 60k, not 80k, so nudge absent → Length = 3
        let! resFull =
            runMessageTransformPipeline plan mkBacklogOps mkStagedEncodeMessages injectFn mkStagedLoadCaps buildCaps

        check
            "full pipeline output differs from direct: budget measured pre-injection bytes at line 92, missing injectFn + caps overhead (direct nudge, full no-nudge)"
            (resDirect.Length <> resFull.Length)
    }
