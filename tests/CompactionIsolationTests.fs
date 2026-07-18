module Wanxiangshu.Tests.CompactionIsolationTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.CompactionTransitions
open Wanxiangshu.Kernel.FallbackKernel.Types

let testCompactionSummaryTransformPendingFlag () =
    promise {
        let store = FallbackRuntimeStore()
        let sessionID = "test-session-1"

        // Initially, the flag should be false
        check "flag initially false" (not (store.IsCompactionSummaryTransformPending(sessionID)))

        // TryConsume should return false when flag is not set
        check "tryConsume returns false when not set" (not (store.TryConsumeCompactionSummaryTransform(sessionID)))

        // Set the flag via beginCompaction
        let initial = store.GetSession(sessionID)
        let afterBegin = beginCompaction "compact-1" 1 initial
        store.UpdateSession(sessionID, (fun _ -> afterBegin))

        // Flag should now be true
        check "flag true after beginCompaction" (store.IsCompactionSummaryTransformPending(sessionID))

        // TryConsume should return true and clear the flag
        check "tryConsume returns true when set" (store.TryConsumeCompactionSummaryTransform(sessionID))
        check "flag cleared after tryConsume" (not (store.IsCompactionSummaryTransformPending(sessionID)))

        // Second tryConsume should return false
        check "second tryConsume returns false" (not (store.TryConsumeCompactionSummaryTransform(sessionID)))
    }

let testCompactionSummaryTransformClearedOnSettle () =
    promise {
        let store = FallbackRuntimeStore()
        let sessionID = "test-session-2"

        // Set the flag via beginCompaction
        let initial = store.GetSession(sessionID)
        let afterBegin = beginCompaction "compact-2" 1 initial
        store.UpdateSession(sessionID, (fun _ -> afterBegin))

        check "flag true after beginCompaction" (store.IsCompactionSummaryTransformPending(sessionID))

        // Settle should clear the flag
        let afterSettle = settleCompaction (store.GetSession(sessionID))
        store.UpdateSession(sessionID, (fun _ -> afterSettle))

        check "flag cleared after settle" (not (store.IsCompactionSummaryTransformPending(sessionID)))
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

        check "flag set manually" (store.IsCompactionSummaryTransformPending(sessionID))

        // Clear manually
        store.ClearCompactionSummaryTransformPending(sessionID)
        check "flag cleared manually" (not (store.IsCompactionSummaryTransformPending(sessionID)))
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
        check "session A flag set" (store.IsCompactionSummaryTransformPending(sessionA))
        check "session B flag not set" (not (store.IsCompactionSummaryTransformPending(sessionB)))

        // Consume A should not affect B
        check "consume A returns true" (store.TryConsumeCompactionSummaryTransform(sessionA))
        check "session A flag cleared" (not (store.IsCompactionSummaryTransformPending(sessionA)))
        check "session B still not set" (not (store.IsCompactionSummaryTransformPending(sessionB)))
    }

let run () =
    promise {
        do! testCompactionSummaryTransformPendingFlag ()
        do! testCompactionSummaryTransformClearedOnSettle ()
        do! testCompactionSummaryTransformClearedManually ()
        do! testCompactionIsolationTwoSessions ()
    }
