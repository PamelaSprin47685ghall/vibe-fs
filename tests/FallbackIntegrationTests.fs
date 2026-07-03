module Wanxiangshu.Tests.FallbackIntegrationTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.StateMachine

let private mkModel (pid: string) (mid: string) : FallbackModel =
    { ProviderID = pid; ModelID = mid; Variant = None
      Temperature = None; TopP = None; MaxTokens = None
      ReasoningEffort = None; Thinking = false }

let private mkChain () = [ mkModel "a" "m1"; mkModel "b" "m2"; mkModel "c" "m3" ]

let private mkCfg () =
    { DefaultChain = []; AgentChains = Map.ofList []
      MaxRetries = 2; LoopMaxContinues = 3 }

let private retryErr =
    { ErrorName = "err"; DomainError = Some (UnknownJsError "fail")
      Message = "fail"; StatusCode = None; IsRetryable = Some true }

let private abortErr =
    { ErrorName = "MessageAbortedError"; DomainError = Some MessageAborted
      Message = "abort"; StatusCode = None; IsRetryable = None }

let private authErr =
    { ErrorName = "AuthError"; DomainError = None
      Message = "401"; StatusCode = Some 401; IsRetryable = None }

let private mkState phase idx fc cc =
    { Phase = phase; CurrentIndex = idx; FailureCount = fc
      Cancelled = false; TaskComplete = false; ContinueCount = cc }

let scan_k1_startsFromZero () =
    let s1, _ = transition (mkState FallbackPhase.Idle 1 0 0) (SessionError authErr) (mkCfg ()) (mkChain ())
    match s1.Phase with
    | FallbackPhase.Scanning (scanIdx, _) ->
        equal "k=1 scanStart=0" 0 scanIdx
        equal "k=1 failureCount=1" 1 s1.FailureCount
    | _ -> failwith "expected Scanning"

let scan_k2_startsFromCurrent () =
    let s1, _ = transition (mkState FallbackPhase.Idle 2 1 0) (SessionError authErr) (mkCfg ()) (mkChain ())
    match s1.Phase with
    | FallbackPhase.Scanning (scanIdx, _) -> equal "k=2 scanStart=2" 2 scanIdx
    | _ -> failwith "expected Scanning"

let scan_k4_startsFromZero () =
    let s1, _ = transition (mkState FallbackPhase.Idle 2 3 0) (SessionError authErr) (mkCfg ()) (mkChain ())
    match s1.Phase with
    | FallbackPhase.Scanning (scanIdx, _) -> equal "k=4 scanStart=0" 0 scanIdx
    | _ -> failwith "expected Scanning"

let scanning_advancesOnError () =
    let s0 = mkState (FallbackPhase.Scanning (0, 1)) 1 1 0
    let s1, _ = transition s0 (SessionError retryErr) (mkCfg ()) (mkChain ())
    match s1.Phase with
    | FallbackPhase.Scanning (scanIdx, _) -> equal "advanced to 1" 1 scanIdx
    | _ -> failwith "expected Scanning"

let scanning_busy_resetsFailure () =
    let s0 = mkState (FallbackPhase.Scanning (0, 2)) 2 3 0
    let s1, _ = transition s0 SessionBusy (mkCfg ()) (mkChain ())
    equal "phase Idle" FallbackPhase.Idle s1.Phase
    equal "currentIndex=0" 0 s1.CurrentIndex
    equal "failureCount=0" 0 s1.FailureCount

let hallucination_exceedsThreshold () =
    let cfg = { (mkCfg ()) with LoopMaxContinues = 3 }
    let _, action = transition (mkState FallbackPhase.Idle 0 0 3) (SessionError retryErr) cfg (mkChain ())
    match action with
    | FallbackAction.AbortAndResume _ -> ()
    | _ -> failwith "expected AbortAndResume"

let hallucination_belowThreshold () =
    let cfg = { (mkCfg ()) with LoopMaxContinues = 3 }
    let _, action = transition (mkState FallbackPhase.Idle 0 0 2) (SessionError retryErr) cfg (mkChain ())
    match action with
    | FallbackAction.SendContinue _ -> ()
    | _ -> failwith "expected SendContinue"

let taskComplete_stopsRecovery () =
    let s1, _ = transition (mkState FallbackPhase.Idle 0 0 0) TaskCompleteCalled (mkCfg ()) (mkChain ())
    check "TaskComplete=true" s1.TaskComplete
    let _, action = transition s1 (SessionError retryErr) (mkCfg ()) (mkChain ())
    equal "DoNothing after TaskComplete" FallbackAction.DoNothing action

let escCancel_stopsRecovery () =
    let s1, _ = transition (mkState FallbackPhase.Idle 0 0 0) (SessionError abortErr) (mkCfg ()) (mkChain ())
    check "Cancelled=true" s1.Cancelled
    let _, action = transition s1 (SessionError retryErr) (mkCfg ()) (mkChain ())
    equal "DoNothing after Cancelled" FallbackAction.DoNothing action

let sessionIdle_idlePhase_returnsDoNothing () =
    let _, action = transition (mkState FallbackPhase.Idle 0 0 0) SessionIdle (mkCfg ()) (mkChain ())
    equal "DoNothing" FallbackAction.DoNothing action

let sessionIdle_taskComplete_doNothing () =
    let s0 = { (mkState FallbackPhase.Idle 0 0 0) with TaskComplete = true }
    let _, action = transition s0 SessionIdle (mkCfg ()) (mkChain ())
    equal "DoNothing" FallbackAction.DoNothing action

let scanning_completesOnIdle () =
    let state = mkState (FallbackPhase.Scanning (1, 0)) 0 1 0
    let s1, action = transition state SessionIdle (mkCfg ()) (mkChain ())
    equal "Idle after scanning idle" FallbackPhase.Idle s1.Phase
    equal "CurrentIndex updated" 1 s1.CurrentIndex
    equal "DoNothing" FallbackAction.DoNothing action

let retrying_recoversOnIdle () =
    let state = mkState (FallbackPhase.Retrying 1) 0 0 0
    let s1, action = transition state SessionIdle (mkCfg ()) (mkChain ())
    equal "Idle after retrying idle" FallbackPhase.Idle s1.Phase
    equal "DoNothing" FallbackAction.DoNothing action

let continueCount_noDoubleIncrement () =
    let state = mkState (FallbackPhase.Retrying 1) 0 0 1
    let s1, _ = transition state SessionBusy (mkCfg ()) (mkChain ())
    equal "ContinueCount unchanged on busy" 1 s1.ContinueCount

let exhausted_propagates () =
    let chain = [ mkModel "a" "m1" ]
    let s1, action = transition (mkState (FallbackPhase.Scanning (0, 0)) 0 1 0) (SessionError retryErr) (mkCfg ()) chain
    equal "Exhausted" FallbackPhase.Exhausted s1.Phase
    equal "PropagateFailure" FallbackAction.PropagateFailure action

let newUserMessage_resets () =
    let s0 = mkState (FallbackPhase.Retrying 2) 1 3 5
    let s1, _ = transition s0 NewUserMessage (mkCfg ()) (mkChain ())
    equal "phase Idle" FallbackPhase.Idle s1.Phase
    equal "currentIndex 0" 0 s1.CurrentIndex
    equal "failureCount 0" 0 s1.FailureCount
    equal "continueCount 0" 0 s1.ContinueCount

let run () =
    scan_k1_startsFromZero ()
    scan_k2_startsFromCurrent ()
    scan_k4_startsFromZero ()
    scanning_advancesOnError ()
    scanning_busy_resetsFailure ()
    hallucination_exceedsThreshold ()
    hallucination_belowThreshold ()
    taskComplete_stopsRecovery ()
    escCancel_stopsRecovery ()
    sessionIdle_idlePhase_returnsDoNothing ()
    sessionIdle_taskComplete_doNothing ()
    scanning_completesOnIdle ()
    retrying_recoversOnIdle ()
    continueCount_noDoubleIncrement ()
    exhausted_propagates ()
    newUserMessage_resets ()
