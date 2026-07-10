module Wanxiangshu.Tests.FallbackKernelTestsPart2

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.Decision
open Wanxiangshu.Kernel.FallbackKernel.Recovery
open Wanxiangshu.Kernel.FallbackKernel.StateMachine


let mkModel
    (pid: string)
    (mid: string)
    (variant: ModelVariant option)
    (temp: float option)
    (topP: float option)
    (maxT: int option)
    (reason: string option)
    (thinking: bool)
    =
    { ProviderID = pid
      ModelID = mid
      Variant = variant
      Temperature = temp
      TopP = topP
      MaxTokens = maxT
      ReasoningEffort = reason
      Thinking = thinking }

let chain xs = xs

let mkState
    (phase: FallbackPhase)
    (currentIndex: int)
    (failureCount: int)
    (lifecycle: FallbackLifecycle)
    (continueCount: int)
    (recoveryCount: int)
    =
    { Phase = phase
      CurrentIndex = currentIndex
      FailureCount = failureCount
      Lifecycle = lifecycle
      ContinueCount = continueCount
      RecoveryCount = recoveryCount }

let mkConfig (maxRetries: int) (loopMax: int) (maxRecoveries: int) =
    { DefaultChain = []
      AgentChains = Map.empty
      MaxRetries = maxRetries
      LoopMaxContinues = loopMax
      MaxRecoveries = maxRecoveries }

let mkError (name: string) (msg: string) (sc: int option) (ret: bool option) (domainErr: DomainError option) =
    { ErrorName = name
      DomainError = domainErr
      Message = msg
      StatusCode = sc
      IsRetryable = ret }

let errRetry =
    mkError "retry" "retry" None (Some true) (Some(UnknownJsError "retry"))

let errAbort = mkError "MessageAbortedError" "abort" None None (Some MessageAborted)

let errAbort2 =
    mkError "AbortError" "abort2" None None (Some(ClientCancellation "abort2"))


let transitionIdleErrorToRetrying () =
    let model = mkModel "oai" "gpt-5" None None None None None false
    let chain = chain [ model ]
    let cfg = mkConfig 2 3 5
    let state = mkState FallbackPhase.Idle 0 0 FallbackLifecycle.Active 0 0
    let err = mkError "err" "fail" None (Some true) (Some(UnknownJsError "fail"))

    let ns, action = transition state (SessionError err) cfg chain

    equal "phase becomes Retrying 1" (FallbackPhase.Retrying 1) ns.Phase
    equal "continueCount increments" 1 ns.ContinueCount

    match action with
    | FallbackAction.SendContinue m -> equal "action SendContinue" model m
    | _ -> check "action is SendContinue" false

let transitionRetryingBusyToIdle () =
    let model = mkModel "oai" "gpt-5" None None None None None false
    let chain = chain [ model ]
    let cfg = mkConfig 2 3 5
    let state = mkState (FallbackPhase.Retrying 1) 0 0 FallbackLifecycle.Active 0 0
    let ns, action = transition state SessionBusy cfg chain

    equal "phase back to Idle" FallbackPhase.Idle ns.Phase
    equal "action DoNothing" FallbackAction.DoNothing action

let transitionScanningErrorAdvances () =
    let models =
        [ mkModel "oai" "m1" None None None None None false
          mkModel "oai" "m2" None None None None None false
          mkModel "oai" "m3" None None None None None false ]

    let chain = chain models
    let cfg = mkConfig 2 3 5
    let state = mkState (FallbackPhase.Scanning(0, 0)) 0 0 FallbackLifecycle.Active 0 0
    let err = mkError "err" "fail" None (Some true) (Some(UnknownJsError "fail"))

    let ns, action = transition state (SessionError err) cfg chain

    equal "scanIdx advances to 1" (FallbackPhase.Scanning(1, 0)) ns.Phase
    equal "failureCount unchanged (scanning error does not bump k)" 0 ns.FailureCount

    match action with
    | FallbackAction.SendContinue m -> equal "next model is m2" models.[1] m
    | _ -> check "action is SendContinue" false

let transitionScanningBusyUpdatesK () =
    let models =
        [ mkModel "oai" "m1" None None None None None false
          mkModel "oai" "m2" None None None None None false
          mkModel "oai" "m3" None None None None None false ]

    let chain = chain models
    let cfg = mkConfig 2 3 5
    let state = mkState (FallbackPhase.Scanning(2, 0)) 0 1 FallbackLifecycle.Active 0 0

    let ns, action = transition state SessionBusy cfg chain

    equal "phase → Idle" FallbackPhase.Idle ns.Phase
    equal "currentIndex updates to scanIdx" 2 ns.CurrentIndex
    equal "failureCount k+1" 2 ns.FailureCount
    equal "action DoNothing" FallbackAction.DoNothing action

let transitionExhaustedPropagates () =
    let model = mkModel "oai" "gpt-5" None None None None None false
    let chain = chain [ model ]
    let cfg = mkConfig 1 3 5
    let state = mkState (FallbackPhase.Retrying 1) 0 0 FallbackLifecycle.Active 0 0

    let ns, action = transition state (SessionError errRetry) cfg chain

    equal "phase → Scanning (0,0)" (FallbackPhase.Scanning(0, 0)) ns.Phase
    equal "failureCount = 1" 1 ns.FailureCount

    match action with
    | FallbackAction.SendContinue m -> equal "action SendContinue" model m
    | _ -> check "action is SendContinue" false

let transitionNewUserMessageResets () =
    let model = mkModel "oai" "gpt-5" None None None None None false
    let chain = chain [ model ]
    let cfg = mkConfig 2 3 5
    let state = mkState (FallbackPhase.Retrying 3) 0 5 FallbackLifecycle.Active 2 0

    let ns, action = transition state NewUserMessage cfg chain

    equal "phase → Idle" FallbackPhase.Idle ns.Phase
    equal "failureCount → 0" 0 ns.FailureCount
    equal "continueCount → 0" 0 ns.ContinueCount
    equal "action DoNothing" FallbackAction.DoNothing action

let transitionTaskCompleteStops () =
    let model = mkModel "oai" "gpt-5" None None None None None false
    let chain = chain [ model ]
    let cfg = mkConfig 2 3 5
    let state = mkState (FallbackPhase.Retrying 1) 0 0 FallbackLifecycle.Active 0 0

    let ns, action = transition state TaskCompleteCalled cfg chain

    equal "phase → Idle" FallbackPhase.Idle ns.Phase
    equal "lifecycle → TaskComplete" FallbackLifecycle.TaskComplete ns.Lifecycle
    equal "action DoNothing" FallbackAction.DoNothing action

let transitionMessageAbortedCancels () =
    let model = mkModel "oai" "gpt-5" None None None None None false
    let chain = chain [ model ]
    let cfg = mkConfig 2 3 5
    let state = mkState FallbackPhase.Idle 0 0 FallbackLifecycle.Active 0 0

    let ns, action = transition state (SessionError errAbort) cfg chain

    equal "lifecycle → Cancelled" FallbackLifecycle.Cancelled ns.Lifecycle
    equal "action DoNothing" FallbackAction.DoNothing action

    let ns2, _ = transition state (SessionError errAbort2) cfg chain
    equal "AbortError also cancels" FallbackLifecycle.Cancelled ns2.Lifecycle

let transitionLoopDetectionSendContinue () =
    let model = mkModel "oai" "gpt-5" None None None None None false
    let chain = chain [ model ]
    let cfg = mkConfig 2 3 5
    let state = mkState FallbackPhase.Idle 0 0 FallbackLifecycle.Active 3 0
    let err = mkError "retry" "retry" None (Some true) (Some(UnknownJsError "retry"))

    let ns, action = transition state (SessionError err) cfg chain

    equal "phase → Exhausted when ContinueCount >= LoopMaxContinues" FallbackPhase.Exhausted ns.Phase
    equal "continueCount resets to 0" 0 ns.ContinueCount
    equal "action is PropagateFailure" FallbackAction.PropagateFailure action

let run () =
    transitionIdleErrorToRetrying ()
    transitionRetryingBusyToIdle ()
    transitionScanningErrorAdvances ()
    transitionScanningBusyUpdatesK ()
    transitionExhaustedPropagates ()
    transitionNewUserMessageResets ()
    transitionTaskCompleteStops ()
    transitionMessageAbortedCancels ()
    transitionLoopDetectionSendContinue ()
