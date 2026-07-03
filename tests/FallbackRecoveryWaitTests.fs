module Wanxiangshu.Tests.FallbackRecoveryWaitTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackRecoveryWait

let isSettled_falseWhenFresh () =
    let rt = FallbackRuntimeState()
    check "fresh session not settled" (not (isRecoverySettled rt "s1"))

let isSettled_trueWhenConsumed () =
    let rt = FallbackRuntimeState()
    rt.SetConsumed "s1" true
    check "consumed true settled" (isRecoverySettled rt "s1")

let isSettled_trueWhenExhausted () =
    let rt = FallbackRuntimeState()
    let s0 = rt.GetOrCreateState "s1"
    rt.UpdateState "s1" { s0 with Phase = FallbackPhase.Exhausted }
    check "exhausted settled" (isRecoverySettled rt "s1")

let waitCompletesAfterConsumedSetAsync () = promise {
    let rt = FallbackRuntimeState()
    let sid = "wait-sid"
    let waitP = waitForRecovery rt sid 32
    do! yieldMicrotask ()
    rt.SetConsumed sid true
    do! waitP
    check "wait finished after consumed" true
}

let run () = promise {
    isSettled_falseWhenFresh ()
    isSettled_trueWhenConsumed ()
    isSettled_trueWhenExhausted ()
    do! waitCompletesAfterConsumedSetAsync ()
}