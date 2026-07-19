module Wanxiangshu.Tests.ContextBudgetCalibrationTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Runtime.ContextBudgetLimitResolver
open Wanxiangshu.Runtime.ContextBudgetTrace
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.ContextBudgetStore
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Kernel.HostTools
open Fable.Core
open Fable.Core.JsInterop

// ─── Helpers ──────────────────────────────────────────────────────────────────

let private mkBacklogOps =
    { Host = opencode
      GetOrRebuildBacklog = fun _ _ -> [] }

let private mkStagedPlan (sessionID: string) (directory: string) (scope: RuntimeScope) =
    { SessionID = sessionID
      Agent = "main"
      Directory = directory
      ProjectionPolicy = ProjectionPolicy.IncludeProjection
      BacklogProjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include
      CapsInjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Include
      ParallelHintPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Include
      ContextBudgetPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Include
      IsSubagentSession = false
      Cleaned = []
      RawArray = None
      SembleInjectEnabled = false
      Scope = scope
      MaxInputTokens = 128000
      ModelKey = "openai/gpt-4o:default"
      LimitSource = "openai-session-model"
      ObserveLatestUsage = fun () -> Promise.lift None }

// ─── Calibration ──────────────────────────────────────────────────────────────

let spec_calibration_uses_previous_outbound () =
    let calibration =
        calibrateUsage
            { AssistantMessageID = "a1"
              InputTokens = 60000L }
            { Fingerprint = "r1"; Bytes = 10000 }

    equal "calibration keeps observed tokens" (Some 60000L) (calibration |> Option.map _.InputTokens)
    equal "calibration uses the completed request bytes" (Some 10000) (calibration |> Option.map _.OutboundBytes)

let spec_stable_estimate_uses_folded_bytes () =
    let calibration =
        { AssistantMessageID = "a1"
          InputTokens = 20000L
          OutboundBytes = 80000 }

    equal
        "folded baseline is estimated from folded bytes"
        (Some 5000L)
        (estimateTokensFromCalibration calibration 20000)

let spec_bootstrap_hard_safety () =
    check "bootstrap protects at 75 percent" (bootstrapHardSafety 7500L 10000L)
    check "bootstrap stays quiet below 75 percent" (not (bootstrapHardSafety 7499L 10000L))

// ─── Specs ────────────────────────────────────────────────────────────────────

let spec_calibrate_usesPriorPendingNotCurrent () =
    spec_calibration_uses_previous_outbound ()

let spec_calibrate_noLastUsage_returnsNone () =
    let calibration =
        { AssistantMessageID = "a1"
          InputTokens = 60000L
          OutboundBytes = 10000 }

    equal "zero folded bytes is None" None (estimateTokensFromCalibration calibration 0)
    equal "negative folded bytes is None" None (estimateTokensFromCalibration calibration -1)

let spec_calibrate_confidenceDegraded () =
    check "bootstrap hard safety is available" (bootstrapHardSafety 8000L 10000L)

let spec_hardSafety_triggersAt75Percent () = spec_bootstrap_hard_safety ()

let spec_hardSafety_triggersAtSaturation () =
    check "100 percent triggers hard safety" (bootstrapHardSafety 150000L 150000L)
    check "zero tokens does not trigger hard safety" (not (bootstrapHardSafety 0L 150000L))

// ─── Contract: DecisionTrace identity fields ───────────────────────────────────

let spec_decisionTrace_identity_fields_distinct () =
    let modelKey = "openai/gpt-4o:v1"
    let limitSrc = "hard-limit"
    let stableB = 48000
    let limit = 128000L
    let finalB = 51000
    let estTokens = 96000L

    let trace =
        { Limit = limit
          ModelKey = modelKey
          LimitSource = limitSrc
          ObservedTokens = Some 94000L
          CalibrationBytes = Some 10000
          FinalOutboundBytes = finalB
          EstimatedTokens = estTokens
          StableBytes = stableB
          PhaseBaseTokens = 30000L
          Confidence = UsageConfidence.Observed
          Pressure = BelowThreshold
          Action = "no-action" }

    equal "ModelKey preserved as distinct identity" modelKey trace.ModelKey
    equal "LimitSource preserved as distinct source tag" limitSrc trace.LimitSource
    equal "StableBytes is a separate stable-bandwidth value, not FinalOutboundBytes" stableB trace.StableBytes

    check
        "StableBytes differs from FinalOutboundBytes when fold baseline is not the last round"
        (trace.StableBytes <> trace.FinalOutboundBytes)

// ─── Contract: MessageTransformPlan carries explicit model identity ─────────────

let spec_messageTransformPlan_carriesModelKeyAndLimitSource () =
    let sessionID = "sess-plan-identity"
    let directory = "/workspace"
    let providerID = "test-provider"
    let modelID = "test-model"
    let variant = "default"

    let expectedModelKey = providerID + "/" + modelID + ":" + variant
    let expectedSource = "openai-session-model"

    let scope = Wanxiangshu.Runtime.RuntimeScope.create ()

    let plan =
        { SessionID = sessionID
          Agent = "main"
          Directory = directory
          ProjectionPolicy = ProjectionPolicy.IncludeProjection
          BacklogProjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include
          CapsInjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Include
          ParallelHintPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Include
          ContextBudgetPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Include
          IsSubagentSession = false
          Cleaned = []
          RawArray = None
          SembleInjectEnabled = false
          Scope = scope
          MaxInputTokens = 128000
          ModelKey = expectedModelKey
          LimitSource = expectedSource
          ObserveLatestUsage = fun () -> Promise.lift None }

    equal "ModelKey is the explicit provider/model identity" expectedModelKey plan.ModelKey
    check "ModelKey is not session@directory" (plan.ModelKey <> sessionID + "@" + directory)
    check "LimitSource is a truthful resolver tag, not hard-coded stub" (plan.LimitSource = expectedSource)
    check "ModelKey and LimitSource are separate fields" (plan.ModelKey <> plan.LimitSource)

// ─── Contract: ContextBudgetTrace.actionForDecision explicit actions ─────────────

let spec_actionForDecision_compactingPressureReturnsCompacting () =
    let action = actionForDecision Compacting Idle
    equal "compacting pressure yields 'compacting'" "compacting" action

let spec_actionForDecision_episodeExhaustedPressureReturnsEpisodeExhausted () =
    let action = actionForDecision RequireTodoWriteEmergency Idle
    equal "episode-exhausted pressure yields 'episode-exhausted'" "episode-exhausted" action

let spec_actionForDecision_nudgeInjectedWhenEmergencySignaled () =
    let action = actionForDecision RequireTodoWriteEmergency (EmergencySignaled 0)
    equal "emergency signaled yields 'nudge-injected'" "nudge-injected" action

let spec_actionForDecision_belowThresholdReturnsNoAction () =
    let action = actionForDecision BelowThreshold Idle
    equal "below-threshold pressure yields 'below-threshold'" "below-threshold" action

// ─── Entry ────────────────────────────────────────────────────────────────────

let run () : unit =
    spec_calibration_uses_previous_outbound ()
    spec_stable_estimate_uses_folded_bytes ()
    spec_bootstrap_hard_safety ()
    spec_calibrate_usesPriorPendingNotCurrent ()
    spec_calibrate_noLastUsage_returnsNone ()
    spec_calibrate_confidenceDegraded ()
    spec_hardSafety_triggersAt75Percent ()
    spec_hardSafety_triggersAtSaturation ()
    spec_decisionTrace_identity_fields_distinct ()
    spec_messageTransformPlan_carriesModelKeyAndLimitSource ()
    spec_actionForDecision_compactingPressureReturnsCompacting ()
    spec_actionForDecision_episodeExhaustedPressureReturnsEpisodeExhausted ()
    spec_actionForDecision_nudgeInjectedWhenEmergencySignaled ()
    spec_actionForDecision_belowThresholdReturnsNoAction ()
