module Wanxiangshu.Tests.FallbackKernelTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.Decision
open Wanxiangshu.Kernel.FallbackKernel.Recovery
open Wanxiangshu.Kernel.FallbackKernel.StateMachine
open Wanxiangshu.Tests.FallbackKernelTestsPart2


let mkModel (pid: string) (mid: string)
            (variant: ModelVariant option)
            (temp: float option)
            (topP: float option)
            (maxT: int option)
            (reason: string option)
            (thinking: bool) =
    { ProviderID      = pid
      ModelID         = mid
      Variant         = variant
      Temperature     = temp
      TopP            = topP
      MaxTokens       = maxT
      ReasoningEffort = reason
      Thinking        = thinking }

let chain xs = xs

let mkState
        (phase: FallbackPhase)
        (currentIndex: int)
        (failureCount: int)
        (cancelled: bool)
        (taskComplete: bool)
        (continueCount: int) =
    { Phase         = phase
      CurrentIndex  = currentIndex
      FailureCount  = failureCount
      Cancelled     = cancelled
      TaskComplete  = taskComplete
      ContinueCount = continueCount }

let mkConfig (maxRetries: int) (loopMax: int) =
    { DefaultChain      = []
      AgentChains       = Map.empty
      MaxRetries        = maxRetries
      LoopMaxContinues  = loopMax }

let mkError (name: string) (msg: string) (sc: int option) (ret: bool option) (domainErr: DomainError option) =
    { ErrorName   = name
      DomainError = domainErr
      Message     = msg
      StatusCode  = sc
      IsRetryable = ret }

let err401  = mkError "err" "401" (Some 401) None None
let err429  = mkError "rate" "429" (Some 429) None None
let err500  = mkError "srv" "500" (Some 500) None None
let errRetry = mkError "retry" "retry" None (Some true) (Some (UnknownJsError "retry"))
let errNonRetry = mkError "nonretry" "nonretry" None (Some false) (Some (UnknownJsError "nonretry"))
let errAbort = mkError "MessageAbortedError" "abort" None None (Some MessageAborted)
let errAbort2 = mkError "AbortError" "abort2" None None (Some (ClientCancellation "abort2"))


let isPerfectSquareBoundary () =
    check "n=0  isPerfectSquare" (not (isPerfectSquare 0))
    check "n=-1 isPerfectSquare" (not (isPerfectSquare -1))
    check "n=1  isPerfectSquare" (isPerfectSquare 1)
    check "n=4  isPerfectSquare" (isPerfectSquare 4)
    check "n=9  isPerfectSquare" (isPerfectSquare 9)
    check "n=16 isPerfectSquare" (isPerfectSquare 16)
    check "n=2  notPerfectSquare" (not (isPerfectSquare 2))
    check "n=3  notPerfectSquare" (not (isPerfectSquare 3))
    check "n=15 notPerfectSquare" (not (isPerfectSquare 15))
    check "n=17 notPerfectSquare" (not (isPerfectSquare 17))
    check "n=10000 isPerfectSquare" (isPerfectSquare 10000)
    check "n=9999  notPerfectSquare" (not (isPerfectSquare 9999))

let scanStartIndexPerfectSquares () =
    equal "failure=1  square→0" 0 (scanStartIndex 1 5)
    equal "failure=4  square→0" 0 (scanStartIndex 4 7)
    equal "failure=9  square→0" 0 (scanStartIndex 9 3)
    equal "failure=16 square→0" 0 (scanStartIndex 16 99)

let scanStartIndexNonSquares () =
    equal "failure=2  non-square→currentIndex"  5  (scanStartIndex 2 5)
    equal "failure=3  non-square→currentIndex" 10  (scanStartIndex 3 10)
    equal "failure=5  non-square→currentIndex"  0  (scanStartIndex 5 0)
    equal "failure=15 non-square→currentIndex"  7  (scanStartIndex 15 7)

let updateFailureCountBranches () =
    equal "fallback cheaper" 0 (updateFailureCount 1 5 7)
    equal "fallback pricier" 8 (updateFailureCount 7 5 7)
    equal "same model"      5 (updateFailureCount 5 5 5)


let classifyErrorPriority () =
    let cfg = mkConfig 2 3
    let baseState = mkState FallbackPhase.Idle 0 0 false false 0

    let stCancelled = mkState FallbackPhase.Idle 0 0 true false 0
    equal "cancelled overrides all" ErrorClass.Ignore
        (classifyError err401 stCancelled cfg)

    let stDone = mkState FallbackPhase.Idle 0 0 false true 0
    equal "taskComplete overrides all" ErrorClass.Ignore
        (classifyError err401 stDone cfg)

    equal "MessageAbortedError" ErrorClass.Ignore
        (classifyError errAbort baseState cfg)
    equal "AbortError" ErrorClass.Ignore
        (classifyError errAbort2 baseState cfg)

    equal "401 → ImmediateFallback" ErrorClass.ImmediateFallback
        (classifyError err401 baseState cfg)

    equal "isRetryable=false → ImmediateFallback" ErrorClass.ImmediateFallback
        (classifyError errNonRetry baseState cfg)

    let stRetry = { baseState with Phase = FallbackPhase.Retrying 1 }
    equal "retryable with count<max → RetrySame" ErrorClass.RetrySame
        (classifyError errRetry stRetry cfg)

    equal "429 with count=0 → RetrySame" ErrorClass.RetrySame
        (classifyError err429 baseState cfg)

    let stExh = { baseState with Phase = FallbackPhase.Retrying 2 }
    equal "retryCount>=max → Exhausted" ErrorClass.Exhausted
        (classifyError errRetry stExh cfg)

    let unknown = mkError "unknown" "?" None None (Some (UnknownJsError "?"))
    equal "unknown default → RetrySame" ErrorClass.RetrySame
        (classifyError unknown baseState cfg)


let run () =
    isPerfectSquareBoundary ()
    scanStartIndexPerfectSquares ()
    scanStartIndexNonSquares ()
    updateFailureCountBranches ()
    classifyErrorPriority ()
    FallbackKernelTestsPart2.run ()
