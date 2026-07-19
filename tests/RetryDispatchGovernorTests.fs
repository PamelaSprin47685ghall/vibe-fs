module Wanxiangshu.Tests.RetryDispatchGovernorTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.Fallback.RetryDispatchGovernor

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let test_session_serialization () =
    promise {
        let governor = RetryDispatchGovernor(rateLimitMs = 0L)
        let key1 = RetryModelKey.Create("ws1", "sess1", "provider1", "model1")

        let mutable step1 = false
        let mutable step2 = false
        let mutable concurrentRunning = false

        let d1 () =
            promise {
                concurrentRunning <- true
                do! Promise.sleep 50
                step1 <- true
                concurrentRunning <- false
            }

        let d2 () =
            promise {
                if concurrentRunning then
                    failwith "D2 ran concurrently with D1!"

                step2 <- true
            }

        let p1 = governor.RunWhenAllowed(key1, (fun () -> true), d1)
        let p2 = governor.RunWhenAllowed(key1, (fun () -> true), d2)

        let! r1 = p1
        let! r2 = p2

        equal "r1 is Dispatched" Dispatched r1
        equal "r2 is Dispatched" Dispatched r2
        check "step1 completed" step1
        check "step2 completed" step2
    }

let test_session_cancelled_before_dispatch () =
    promise {
        let governor = RetryDispatchGovernor(rateLimitMs = 0L)
        let key1 = RetryModelKey.Create("ws1", "sess1", "provider1", "model1")

        let mutable d1Run = false
        let mutable d2Run = false

        let d1 () =
            promise {
                do! Promise.sleep 50
                d1Run <- true
            }

        let d2 () = promise { d2Run <- true }

        let p1 = governor.RunWhenAllowed(key1, (fun () -> true), d1)

        // During d1's execution, session becomes invalid, so stillValid returns false for d2
        let mutable stillValidVal = true
        let p2 = governor.RunWhenAllowed(key1, (fun () -> stillValidVal), d2)

        do! Promise.sleep 10
        stillValidVal <- false

        let! r1 = p1
        let! r2 = p2

        equal "r1 is Dispatched" Dispatched r1
        equal "r2 is CancelledBeforeDispatch" CancelledBeforeDispatch r2
        check "d1 ran" d1Run
        check "d2 did not run" (not d2Run)
    }

let test_provider_rate_limiting () =
    promise {
        // Temporarily clear WANXIANGSHU_TEST
        let originalEnv = nodeProcess?env?("WANXIANGSHU_TEST")
        nodeProcess?env?("WANXIANGSHU_TEST") <- "false"

        try
            // rate limit = 100ms
            let governor = RetryDispatchGovernor(rateLimitMs = 100L)

            // Two different physical sessions, but same provider/model
            let key1 = RetryModelKey.Create("ws1", "sess1", "provider1", "model1")
            let key2 = RetryModelKey.Create("ws1", "sess2", "provider1", "model1")

            let mutable d1Time = 0.0
            let mutable d2Time = 0.0

            let d1 () =
                promise { d1Time <- JS.Constructors.Date.now () }

            let d2 () =
                promise { d2Time <- JS.Constructors.Date.now () }

            let! r1 = governor.RunWhenAllowed(key1, (fun () -> true), d1)
            let! r2 = governor.RunWhenAllowed(key2, (fun () -> true), d2)

            equal "r1 Dispatched" Dispatched r1
            equal "r2 Dispatched" Dispatched r2

            let elapsed = d2Time - d1Time
            // Since they are different sessions, they can start concurrently from a session perspective,
            // but provider rate limiting forces the second one to delay by at least 100ms
            check $"elapsed {elapsed} >= 80ms (with tolerance)" (elapsed >= 80.0)
        finally
            nodeProcess?env?("WANXIANGSHU_TEST") <- originalEnv
    }

let test_cleanup () =
    promise {
        let governor = RetryDispatchGovernor(rateLimitMs = 10L)
        let key1 = RetryModelKey.Create("ws1", "sess1", "provider1", "model1")

        let! r1 = governor.RunWhenAllowed(key1, (fun () -> true), (fun () -> Promise.lift ()))
        equal "Dispatched" Dispatched r1

        // Wait a bit, then cleanup with staleThresholdMs = 50ms (not stale yet)
        do! Promise.sleep 10
        governor.Cleanup(50L)

        // Wait more, then cleanup with staleThresholdMs = 10ms (should be stale)
        do! Promise.sleep 50
        governor.Cleanup(10L)

        governor.Reset()
    }

let run () =
    promise {
        do! test_session_serialization ()
        do! test_session_cancelled_before_dispatch ()
        do! test_provider_rate_limiting ()
        do! test_cleanup ()
    }
