module Wanxiangshu.Tests.CompactionIsolationTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeTransitions
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeCompactionPure
open Wanxiangshu.Kernel.FallbackKernel.Types

let testCompactionSummaryTransformPendingFlag () =
    promise {
        let store = FallbackRuntimeStore()
        let sessionID = "test-session-1"

        // Initially, the flag should be false
        check "flag initially false" (not ((store.GetSession sessionID).CompactionSummaryTransformPending))

        // TryConsume should return false when flag is not set
        check
            "tryConsume returns false when not set"
            (not (store.UpdateSessionReturning(sessionID, tryConsumeCompactionSummaryTransformReturning)))

        // Set the flag via beginCompaction
        let initial = store.GetSession(sessionID)
        let afterBegin = beginCompaction "compact-1" 1 initial
        store.UpdateSession(sessionID, (fun _ -> afterBegin))

        // Flag should now be true
        check "flag true after beginCompaction" ((store.GetSession sessionID).CompactionSummaryTransformPending)

        // TryConsume should return true and clear the flag
        check
            "tryConsume returns true when set"
            (store.UpdateSessionReturning(sessionID, tryConsumeCompactionSummaryTransformReturning))

        check "flag cleared after tryConsume" (not ((store.GetSession sessionID).CompactionSummaryTransformPending))

        // Second tryConsume should return false
        check
            "second tryConsume returns false"
            (not (store.UpdateSessionReturning(sessionID, tryConsumeCompactionSummaryTransformReturning)))
    }

let testCompactionSummaryTransformClearedOnSettle () =
    promise {
        let store = FallbackRuntimeStore()
        let sessionID = "test-session-2"

        // Set the flag via beginCompaction
        let initial = store.GetSession(sessionID)
        let afterBegin = beginCompaction "compact-2" 1 initial
        store.UpdateSession(sessionID, (fun _ -> afterBegin))

        check "flag true after beginCompaction" ((store.GetSession sessionID).CompactionSummaryTransformPending)

        // Settle should clear the flag
        let afterSettle = settleCompaction (store.GetSession(sessionID))
        store.UpdateSession(sessionID, (fun _ -> afterSettle))

        check "flag cleared after settle" (not ((store.GetSession sessionID).CompactionSummaryTransformPending))
    }

let testCompactionSummaryTransformClearedManually () =
    promise {
        let store = FallbackRuntimeStore()
        let sessionID = "test-session-3"

        // Set the flag
        store.UpdateSession(
            sessionID,
            fun s ->
                { s with
                    CompactionSummaryTransformPending = true }
        )

        check "flag set manually" ((store.GetSession sessionID).CompactionSummaryTransformPending)

        // Clear manually
        store.UpdateSession(sessionID, clearCompactionSummaryTransformPending)
        check "flag cleared manually" (not ((store.GetSession sessionID).CompactionSummaryTransformPending))
    }

let testCompactionIsolationTwoSessions () =
    promise {
        let store = FallbackRuntimeStore()
        let sessionA = "session-a"
        let sessionB = "session-b"

        // Set flag for session A
        store.UpdateSession(
            sessionA,
            fun s ->
                { s with
                    CompactionSummaryTransformPending = true }
        )

        // Session B should not be affected
        check "session A flag set" ((store.GetSession sessionA).CompactionSummaryTransformPending)
        check "session B flag not set" (not ((store.GetSession sessionB).CompactionSummaryTransformPending))

        // Consume A should not affect B
        check
            "consume A returns true"
            (store.UpdateSessionReturning(sessionA, tryConsumeCompactionSummaryTransformReturning))

        check "session A flag cleared" (not ((store.GetSession sessionA).CompactionSummaryTransformPending))
        check "session B still not set" (not ((store.GetSession sessionB).CompactionSummaryTransformPending))
    }

let run () =
    promise {
        do! testCompactionSummaryTransformPendingFlag ()
        do! testCompactionSummaryTransformClearedOnSettle ()
        do! testCompactionSummaryTransformClearedManually ()
        do! testCompactionIsolationTwoSessions ()
    }
