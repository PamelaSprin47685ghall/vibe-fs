module Wanxiangshu.Tests.FallbackRecoveryWaitTestsPart2

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackRecoveryWait

let waitForSubagentSettle_completesWhenTaskCompleteDespiteRetrying () =
    promise {
        let rt = FallbackRuntimeState()
        let sid = "wait-retrying-complete"
        let s0 = rt.GetOrCreateState sid

        rt.UpdateState
            sid
            { s0 with
                Phase = FallbackPhase.Retrying 1 }

        let completed = ref false

        let waitP =
            promise {
                do! waitForSubagentSettle rt sid ""
                completed.Value <- true
            }

        do! yieldMicrotask ()
        check "Retrying without completion still waits" (not completed.Value)
        rt.SetTaskComplete sid true
        do! waitP
        check "TaskComplete releases Retrying wait" completed.Value
    }

let run () =
    waitForSubagentSettle_completesWhenTaskCompleteDespiteRetrying ()
