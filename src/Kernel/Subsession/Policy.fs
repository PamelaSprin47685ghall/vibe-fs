module Wanxiangshu.Kernel.Subsession.Policy

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.Decision
open Wanxiangshu.Kernel.FallbackKernel.Recovery
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.TranscriptDecision

/// Policy decision returned after an error or transcript analysis.
type PolicyDecision =
    | NextTurn of FallbackPolicyState * FallbackModel * prompt: string
    | StopWithFailure of RunFailure

/// Zero-width-space continuation prompt (same as existing codebase).
let private continuationPrompt = "\u200B"

/// Create initial policy state from config and chain.
let initialPolicy (_cfg: FallbackConfig) (_chain: FallbackChain) : FallbackPolicyState =
    { ModelIndex = 0
      RetryCount = 0
      FailureCount = 0
      ContinueCount = 0
      RecoveryCount = 0 }

/// Map FallbackPolicyState back to a SessionFallbackState for reuse of
/// the existing classifyError function.  This is an internal shim — the
/// new code never stores or observes the SessionFallbackState.
let private phaseFromPolicy (p: FallbackPolicyState) : FallbackPhase =
    if p.RetryCount > 0 then
        FallbackPhase.Retrying p.RetryCount
    elif p.RecoveryCount > 0 then
        FallbackPhase.RecoveringToolCallText
    else
        FallbackPhase.Idle

let private stateForClassification (p: FallbackPolicyState) : SessionFallbackState =
    { Phase = phaseFromPolicy p
      CurrentIndex = p.ModelIndex
      FailureCount = p.FailureCount
      Lifecycle = FallbackLifecycle.Active
      ContinueCount = p.ContinueCount
      RecoveryCount = p.RecoveryCount }

/// Decide next turn after an error has been observed and the turn has gone idle.
///
/// Reuses classifyError, isPerfectSquare, scanStartIndex, selectModel
/// from the existing FallbackKernel without coupling to lifecycle/phase.
let afterError
    (cfg: FallbackConfig)
    (chain: FallbackChain)
    (policy: FallbackPolicyState)
    (err: ErrorInput)
    : PolicyDecision =
    let shim = stateForClassification policy
    let errorClass = classifyError err shim cfg

    let trySendContinue (p: FallbackPolicyState) (model: FallbackModel) : PolicyDecision =
        if p.ContinueCount >= cfg.LoopMaxContinues then
            StopWithFailure(FallbackExhausted err)
        else
            NextTurn(
                { p with
                    ContinueCount = p.ContinueCount + 1 },
                model,
                continuationPrompt
            )

    match errorClass with
    | ErrorClass.Ignore ->
        // Abort / cancel — no model change, surface the failure.
        StopWithFailure(FallbackExhausted err)

    | ErrorClass.RetrySame when policy.RetryCount < cfg.MaxRetries ->
        match selectModel chain policy.ModelIndex with
        | Some model ->
            trySendContinue
                { policy with
                    RetryCount = policy.RetryCount + 1 }
                model
        | None -> StopWithFailure(FallbackExhausted err)

    | ErrorClass.RetrySame
    | ErrorClass.ImmediateFallback
    | ErrorClass.Exhausted ->
        // Retries exhausted or immediate fallback → scan chain.
        let k = policy.FailureCount + 1
        let start = scanStartIndex k policy.ModelIndex

        match selectModel chain start with
        | Some model ->
            trySendContinue
                { policy with
                    ModelIndex = start
                    FailureCount = k
                    RetryCount = 0 }
                model
        | None -> StopWithFailure(FallbackExhausted err)

/// Decide next turn after transcript analysis.
///
/// CompleteNaturally should be handled by the caller before invoking this
/// function (it produces a success, not a policy decision).
let afterTranscript
    (cfg: FallbackConfig)
    (chain: FallbackChain)
    (policy: FallbackPolicyState)
    (decision: TranscriptDecision)
    : PolicyDecision =
    match decision with
    | CompleteNaturally _ ->
        // Should never reach here — reducer handles success directly.
        StopWithFailure(ProtocolViolation "CompleteNaturally should not reach afterTranscript")

    | RecoverWithPrompt prompt ->
        if policy.RecoveryCount >= cfg.MaxRecoveries then
            StopWithFailure(RecoveryExhausted "max recoveries exceeded")
        else
            let policy2 =
                { policy with
                    RecoveryCount = policy.RecoveryCount + 1 }

            match selectModel chain policy.ModelIndex with
            | Some model -> NextTurn(policy2, model, prompt)
            | None -> StopWithFailure NoModelConfigured

    | ContinueNormally prompt ->
        if policy.ContinueCount >= cfg.LoopMaxContinues then
            StopWithFailure(RecoveryExhausted "max continues exceeded")
        else
            let policy2 =
                { policy with
                    ContinueCount = policy.ContinueCount + 1 }

            match selectModel chain policy.ModelIndex with
            | Some model -> NextTurn(policy2, model, prompt)
            | None -> StopWithFailure NoModelConfigured

    | IncompleteWithoutRecovery reason -> StopWithFailure(RecoveryExhausted reason)
