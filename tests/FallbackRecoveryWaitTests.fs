module Wanxiangshu.Tests.FallbackRecoveryWaitTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.FallbackRecoveryWait

let isSettled_falseWhenFresh () =
    let rt = FallbackRuntimeStore()
    check "fresh session not settled" (not (isRecoverySettled rt "s1"))

let isSettled_trueWhenConsumed () =
    let rt = FallbackRuntimeStore()
    rt.Update("s1", recordConsumed true)
    check "consumed true settled" (isRecoverySettled rt "s1")

let isSettled_trueWhenExhausted () =
    let rt = FallbackRuntimeStore()
    let s0 = rt.GetOrCreateState "s1"

    rt.Update(
        "s1",
        setCore
            { s0 with
                Phase = FallbackPhase.Exhausted }
    )

    check "exhausted settled" (isRecoverySettled rt "s1")

let isSettled_trueWhenCancelled () =
    let rt = FallbackRuntimeStore()
    let s0 = rt.GetOrCreateState "s1"

    rt.Update(
        "s1",
        setCore
            { s0 with
                Lifecycle = FallbackLifecycle.Cancelled }
    )

    check "cancelled settled" (isRecoverySettled rt "s1")

let isSettled_trueWhenTaskComplete () =
    let rt = FallbackRuntimeStore()
    let s0 = rt.GetOrCreateState "s1"

    rt.Update(
        "s1",
        setCore
            { s0 with
                Lifecycle = FallbackLifecycle.TaskComplete }
    )

    check "task complete settled" (isRecoverySettled rt "s1")

let waitCompletesAfterConsumedSetAsync () =
    promise {
        let rt = FallbackRuntimeStore()
        let sid = "wait-sid"
        let waitP = waitForRecovery rt sid 32
        do! yieldMicrotask ()
        rt.Update(sid, recordConsumed true)
        do! waitP
        check "wait finished after consumed" true
    }

let isToolCallTextRecovery_inProgressWhenScanning () =
    let rt = FallbackRuntimeStore()
    let s0 = rt.GetOrCreateState "s1"

    rt.Update(
        "s1",
        setCore
            { s0 with
                Phase = FallbackPhase.ScanningToolCallText }
    )

    check "ScanningToolCallText in progress" (isToolCallTextRecoveryInProgress rt "s1")

let isToolCallTextRecovery_inProgressWhenRecovering () =
    let rt = FallbackRuntimeStore()
    let s0 = rt.GetOrCreateState "s1"

    rt.Update(
        "s1",
        setCore
            { s0 with
                Phase = FallbackPhase.RecoveringToolCallText }
    )

    check "RecoveringToolCallText in progress" (isToolCallTextRecoveryInProgress rt "s1")

let isToolCallTextRecovery_notInProgressWhenIdle () =
    let rt = FallbackRuntimeStore()
    check "Idle not in progress" (not (isToolCallTextRecoveryInProgress rt "s1"))

let waitForToolCallTextRecovery_completesWhenPhaseClears () =
    promise {
        let rt = FallbackRuntimeStore()
        let sid = "wait-tct"
        let s0 = rt.GetOrCreateState sid

        rt.Update(
            sid,
            setCore
                { s0 with
                    Phase = FallbackPhase.RecoveringToolCallText }
        )

        let resolved = ref false

        let waitP =
            promise {
                do! waitForToolCallTextRecovery rt sid
                resolved.Value <- true
            }

        do! yieldMicrotask ()
        check "wait is pending" (not resolved.Value)

        rt.Update(sid, setCore { s0 with Phase = FallbackPhase.Idle })

        do! waitP
        check "wait finished after phase cleared" true
    }

let waitForToolCallTextRecovery_returnsImmediatelyWhenIdle () =
    promise {
        let rt = FallbackRuntimeStore()
        do! waitForToolCallTextRecovery rt "idle-sess"
        check "returned immediately" true
    }

let run () =
    promise {
        isSettled_falseWhenFresh ()
        isSettled_trueWhenConsumed ()
        isSettled_trueWhenExhausted ()
        isSettled_trueWhenCancelled ()
        isSettled_trueWhenTaskComplete ()
        do! waitCompletesAfterConsumedSetAsync ()
        isToolCallTextRecovery_inProgressWhenScanning ()
        isToolCallTextRecovery_inProgressWhenRecovering ()
        isToolCallTextRecovery_notInProgressWhenIdle ()
        do! waitForToolCallTextRecovery_completesWhenPhaseClears ()
        do! waitForToolCallTextRecovery_returnsImmediatelyWhenIdle ()
    }
