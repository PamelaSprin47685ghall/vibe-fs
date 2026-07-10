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

let isSubagentSettled_falseWhenAwaitingBusy () =
    let rt = FallbackRuntimeState()
    let sid = "t-sess-awaiting"
    rt.SetAwaitingBusy sid true
    check "subagent not settled while awaiting busy" (not (isSubagentSettled rt sid))

/// Regression: after a fallback continue receives a busy event, phase becomes
/// Idle and consumed remains true while the continued model turn is still
/// running.  Idle + Consumed + TaskComplete=false must NOT be considered settled,
/// otherwise result extraction can race ahead of fallback work.
let isSubagentSettled_falseAfterFallbackContinueWhileTaskIncomplete () =
    let rt = FallbackRuntimeState()
    let sid = "t-sess-continue-incomplete"
    rt.SetConsumed sid true
    let s0 = rt.GetOrCreateState sid

    rt.UpdateState
        sid
        { s0 with
            Phase = FallbackPhase.Idle
            TaskComplete = false }

    check "Idle + Consumed + TaskComplete=false not settled" (not (isSubagentSettled rt sid))

let waitForSubagentSettle_waitsUntilAwaitingBusyInactive () =
    promise {
        let rt = FallbackRuntimeState()
        let sid = "t-sess-awaiting-wait"
        rt.SetAwaitingBusy sid true
        let resolved = ref false

        let waitP =
            promise {
                do! waitForSubagentSettle rt sid
                resolved.Value <- true
            }

        do! yieldMicrotask ()
        check "wait is pending while awaiting busy" (not resolved.Value)
        rt.SetAwaitingBusy sid false
        do! yieldMicrotask ()
        do! waitP
        check "wait completed after awaiting busy finished" resolved.Value
    }

/// Terminal observation: consumed=false + Phase=Idle + no pending/busy/nudge
/// → gate model resolves to Resolve → settled.
let isSubagentSettled_trueWhenConsumedFalseAndIdle () =
    let rt = FallbackRuntimeState()
    let sid = "t-sess-consumed-false"
    rt.SetConsumed sid false
    let s0 = rt.GetOrCreateState sid

    rt.UpdateState
        sid
        { s0 with
            Phase = FallbackPhase.Idle
            TaskComplete = false }

    check "Consumed=false + Idle + no active gates → settled" (isSubagentSettled rt sid)

/// Regression: nested gate loop must close fallback gate first, then nudge gate,
/// then resolve only when terminal observation holds.  Opening event-handling
/// (fallback gate) + nudge (todo gate), then clearing fallback first must keep
/// the promise pending; only after nudge clears and consumed=false does it resolve.
let waitForSubagentSettle_nestedGateLoopResolvesInOrder () =
    promise {
        let rt = FallbackRuntimeState()
        let sid = "nested-gate-sess"
        rt.GetOrCreateState sid |> ignore
        rt.SetEventHandlingActive sid true
        rt.SetNudgeActive sid true
        let resolved = ref false

        let waitP =
            promise {
                do! waitForSubagentSettle rt sid
                resolved.Value <- true
            }

        do! yieldMicrotask ()
        check "pending while both gates open" (not resolved.Value)

        // Close fallback gate only — nudge still active → still pending.
        rt.SetEventHandlingActive sid false
        do! yieldMicrotask ()
        check "still pending after fallback gate closes" (not resolved.Value)

        // Close nudge gate — consumed still None → not terminal → still pending.
        rt.SetNudgeActive sid false
        do! yieldMicrotask ()
        check "still pending after nudge gate closes (no consumed)" (not resolved.Value)

        // Mark consumed=false → terminal observation + Resolve → settles.
        rt.SetConsumed sid false
        do! waitP
        check "resolved after terminal observation" resolved.Value
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
        isSubagentSettled_falseWhenAwaitingBusy ()
        isSubagentSettled_falseAfterFallbackContinueWhileTaskIncomplete ()
        do! waitForSubagentSettle_waitsUntilAwaitingBusyInactive ()
        isSubagentSettled_trueWhenConsumedFalseAndIdle ()
        do! waitForSubagentSettle_nestedGateLoopResolvesInOrder ()
    }
