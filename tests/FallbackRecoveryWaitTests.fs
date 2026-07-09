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

    rt.UpdateState
        "s1"
        { s0 with
            Phase = FallbackPhase.Exhausted }

    check "exhausted settled" (isRecoverySettled rt "s1")

let waitCompletesAfterConsumedSetAsync () =
    promise {
        let rt = FallbackRuntimeState()
        let sid = "wait-sid"
        let waitP = waitForRecovery rt sid 32
        do! yieldMicrotask ()
        rt.SetConsumed sid true
        do! waitP
        check "wait finished after consumed" true
    }

let isToolCallTextRecovery_inProgressWhenScanning () =
    let rt = FallbackRuntimeState()
    let s0 = rt.GetOrCreateState "s1"

    rt.UpdateState
        "s1"
        { s0 with
            Phase = FallbackPhase.ScanningToolCallText }

    check "ScanningToolCallText in progress" (isToolCallTextRecoveryInProgress rt "s1")

let isToolCallTextRecovery_inProgressWhenRecovering () =
    let rt = FallbackRuntimeState()
    let s0 = rt.GetOrCreateState "s1"

    rt.UpdateState
        "s1"
        { s0 with
            Phase = FallbackPhase.RecoveringToolCallText }

    check "RecoveringToolCallText in progress" (isToolCallTextRecoveryInProgress rt "s1")

let isToolCallTextRecovery_notInProgressWhenIdle () =
    let rt = FallbackRuntimeState()
    check "Idle not in progress" (not (isToolCallTextRecoveryInProgress rt "s1"))

let waitForToolCallTextRecovery_completesWhenPhaseClears () =
    promise {
        let rt = FallbackRuntimeState()
        let sid = "wait-tct"
        let s0 = rt.GetOrCreateState sid

        rt.UpdateState
            sid
            { s0 with
                Phase = FallbackPhase.RecoveringToolCallText }

        let resolved = ref false

        let waitP =
            promise {
                do! waitForToolCallTextRecovery rt sid
                resolved.Value <- true
            }

        do! yieldMicrotask ()
        check "wait is pending" (not resolved.Value)

        rt.UpdateState sid { s0 with Phase = FallbackPhase.Idle }

        do! waitP
        check "wait finished after phase cleared" true
    }

let waitForToolCallTextRecovery_returnsImmediatelyWhenIdle () =
    promise {
        let rt = FallbackRuntimeState()
        do! waitForToolCallTextRecovery rt "idle-sess"
        check "returned immediately" true
    }

let isSubagentSettled_falseWhenEventHandlingActive () =
    let rt = FallbackRuntimeState()
    let sid = "t-sess-event"
    rt.SetEventHandlingActive sid true
    check "subagent not settled while event handling is active" (not (isSubagentSettled rt sid))

let waitForSubagentSettle_waitsUntilEventHandlingInactive () =
    promise {
        let rt = FallbackRuntimeState()
        let sid = "t-sess-event-wait"
        rt.SetEventHandlingActive sid true
        let resolved = ref false

        let waitP =
            promise {
                do! waitForSubagentSettle rt sid
                resolved.Value <- true
            }

        do! yieldMicrotask ()
        check "wait is pending while handling active" (not resolved.Value)
        rt.SetEventHandlingActive sid false
        do! yieldMicrotask ()
        do! waitP
        check "wait completed after handling finished" resolved.Value
    }

let run () =
    promise {
        isSettled_falseWhenFresh ()
        isSettled_trueWhenConsumed ()
        isSettled_trueWhenExhausted ()
        do! waitCompletesAfterConsumedSetAsync ()
        isToolCallTextRecovery_inProgressWhenScanning ()
        isToolCallTextRecovery_inProgressWhenRecovering ()
        isToolCallTextRecovery_notInProgressWhenIdle ()
        do! waitForToolCallTextRecovery_completesWhenPhaseClears ()
        do! waitForToolCallTextRecovery_returnsImmediatelyWhenIdle ()
        isSubagentSettled_falseWhenEventHandlingActive ()
        do! waitForSubagentSettle_waitsUntilEventHandlingInactive ()
    }
