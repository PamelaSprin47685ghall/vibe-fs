module Wanxiangshu.Kernel.Subsession.Policy

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.Decision
open Wanxiangshu.Kernel.FallbackKernel.Recovery
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.TranscriptDecision

type PolicyDecision =
    | NextTurn of FallbackPolicyState * FallbackModel * prompt: string
    | StopWithFailure of RunFailure

let private continuationPrompt = "\u200B"

/// Sentinel model with empty IDs — signals "delegate model selection to host".
let private delegateToHostSentinel: FallbackModel =
    { ProviderID = ""
      ModelID = ""
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let initialPolicy (_cfg: FallbackConfig) (_chain: FallbackChain) : FallbackPolicyState =
    { Selection = StableAt 0
      FailureCount = 0
      ContinueCount = 0
      RecoveryCount = 0 }

let modelIndexOf (p: FallbackPolicyState) : int =
    match p.Selection with
    | StableAt i -> i
    | RetryingAt(i, _) -> i
    | Scanning(c, _) -> c

let private phaseFromPolicy (p: FallbackPolicyState) : FallbackPhase =
    match p.Selection with
    | RetryingAt(_, c) -> FallbackPhase.Retrying c
    | Scanning(c, o) -> FallbackPhase.Scanning(c, o)
    | StableAt _ when p.RecoveryCount > 0 -> FallbackPhase.RecoveringToolCallText
    | StableAt _ -> FallbackPhase.Idle

let private stateForClassification (p: FallbackPolicyState) : SessionFallbackState =
    { Phase = phaseFromPolicy p
      CurrentIndex = modelIndexOf p
      FailureCount = p.FailureCount
      Lifecycle = FallbackLifecycle.Active
      ContinueCount = p.ContinueCount
      RecoveryCount = p.RecoveryCount }

let private trySendContinue
    (cfg: FallbackConfig)
    (err: ErrorInput)
    (p: FallbackPolicyState)
    (model: FallbackModel)
    : PolicyDecision =
    if p.ContinueCount >= cfg.LoopMaxContinues then
        StopWithFailure(FallbackExhausted err)
    else
        NextTurn(
            { p with
                ContinueCount = p.ContinueCount + 1 },
            model,
            continuationPrompt
        )

/// After a turn finishes successfully while Scanning, stabilize at the candidate.
let afterSuccessfulTurn (policy: FallbackPolicyState) : FallbackPolicyState =
    match policy.Selection with
    | Scanning(c, orig) ->
        let k = updateFailureCount c orig policy.FailureCount

        { policy with
            Selection = StableAt c
            FailureCount = k
            ContinueCount = 0 }
    | RetryingAt(i, _) ->
        { policy with
            Selection = StableAt i
            ContinueCount = 0 }
    | StableAt _ -> { policy with ContinueCount = 0 }

let private handleErrorIgnore (err: ErrorInput) : PolicyDecision = StopWithFailure(FallbackExhausted err)

let private handleErrorScanning
    (cfg: FallbackConfig)
    (chain: FallbackChain)
    (err: ErrorInput)
    (policy: FallbackPolicyState)
    (scanIdx: int)
    (origIdx: int)
    : PolicyDecision =
    let nextIdx = scanIdx + 1

    match selectModel chain nextIdx with
    | Some model ->
        trySendContinue
            cfg
            err
            { policy with
                Selection = Scanning(nextIdx, origIdx) }
            model
    | None -> StopWithFailure(FallbackExhausted err)

let private handleErrorRetry
    (cfg: FallbackConfig)
    (chain: FallbackChain)
    (err: ErrorInput)
    (policy: FallbackPolicyState)
    (errorClass: ErrorClass)
    : PolicyDecision =
    match policy.Selection, errorClass with
    | StableAt i, ErrorClass.RetrySame when 0 < cfg.MaxRetries ->
        match selectModel chain i with
        | Some model ->
            trySendContinue
                cfg
                err
                { policy with
                    Selection = RetryingAt(i, 1) }
                model
        | None -> StopWithFailure(FallbackExhausted err)
    | RetryingAt(i, count), ErrorClass.RetrySame when count < cfg.MaxRetries ->
        match selectModel chain i with
        | Some model ->
            trySendContinue
                cfg
                err
                { policy with
                    Selection = RetryingAt(i, count + 1) }
                model
        | None -> StopWithFailure(FallbackExhausted err)
    | (StableAt i | RetryingAt(i, _)), (ErrorClass.RetrySame | ErrorClass.ImmediateFallback | ErrorClass.Exhausted) ->
        let k = policy.FailureCount + 1
        let start = scanStartIndex k i

        match selectModel chain start with
        | Some model ->
            trySendContinue
                cfg
                err
                { policy with
                    Selection = Scanning(start, i)
                    FailureCount = k }
                model
        | None -> StopWithFailure(FallbackExhausted err)
    | _ -> StopWithFailure(FallbackExhausted err)

let afterError
    (cfg: FallbackConfig)
    (chain: FallbackChain)
    (policy: FallbackPolicyState)
    (err: ErrorInput)
    : PolicyDecision =
    let shim = stateForClassification policy
    let errorClass = classifyError err shim cfg

    match policy.Selection, errorClass with
    | _, ErrorClass.Ignore -> handleErrorIgnore err
    | Scanning(scanIdx, origIdx), _ -> handleErrorScanning cfg chain err policy scanIdx origIdx
    | _, _ -> handleErrorRetry cfg chain err policy errorClass

let afterTranscript
    (cfg: FallbackConfig)
    (chain: FallbackChain)
    (policy: FallbackPolicyState)
    (decision: TranscriptDecision)
    : PolicyDecision =
    match decision with
    | CompleteNaturally _ -> StopWithFailure(ProtocolViolation "CompleteNaturally should not reach afterTranscript")
    | RecoverWithPrompt prompt ->
        if policy.RecoveryCount >= cfg.MaxRecoveries then
            StopWithFailure(RecoveryExhausted "max recoveries exceeded")
        else
            let idx = modelIndexOf policy

            let policy2 =
                { policy with
                    RecoveryCount = policy.RecoveryCount + 1
                    Selection =
                        match policy.Selection with
                        | Scanning _ as s -> s
                        | _ -> StableAt idx }

            match selectModel chain idx with
            | Some model -> NextTurn(policy2, model, prompt)
            | None -> NextTurn(policy2, delegateToHostSentinel, prompt)
    | ContinueNormally prompt ->
        if policy.ContinueCount >= cfg.LoopMaxContinues then
            StopWithFailure(RecoveryExhausted "max continues exceeded")
        else
            let idx = modelIndexOf policy

            let policy2 =
                { policy with
                    ContinueCount = policy.ContinueCount + 1 }

            match selectModel chain idx with
            | Some model -> NextTurn(policy2, model, prompt)
            | None -> NextTurn(policy2, delegateToHostSentinel, prompt)
    | IncompleteWithoutRecovery reason -> StopWithFailure(RecoveryExhausted reason)
