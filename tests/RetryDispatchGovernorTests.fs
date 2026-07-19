module Wanxiangshu.Tests.RetryDispatchGovernorTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.Fallback.RetryDispatchGovernor
open Wanxiangshu.Runtime.ToolSequenceThrottle

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

type MockClock(initialTime: float) =
    let mutable current = initialTime
    member _.Advance(ms: float) = current <- current + ms
    member _.GetMonotonicTimeMs() = current

    interface IClock with
        member _.GetMonotonicTimeMs() = current

type MockSleeper(clock: MockClock) =
    let mutable totalSleptMs = 0.0
    member _.TotalSleptMs = totalSleptMs

    interface ISleeper with
        member _.Sleep(ms: int) =
            clock.Advance(float ms)
            totalSleptMs <- totalSleptMs + float ms
            Promise.lift ()

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
        let clock = MockClock(1000.0)
        let sleeper = MockSleeper(clock)

        let governor =
            RetryDispatchGovernor(rateLimitMs = 100L, clock = clock, sleeper = sleeper)

        // Two different physical sessions, but same provider/model
        let key1 = RetryModelKey.Create("ws1", "sess1", "provider1", "model1")
        let key2 = RetryModelKey.Create("ws1", "sess2", "provider1", "model1")

        let mutable d1Time = 0.0
        let mutable d2Time = 0.0

        let d1 () =
            promise { d1Time <- clock.GetMonotonicTimeMs() }

        let d2 () =
            promise { d2Time <- clock.GetMonotonicTimeMs() }

        let! r1 = governor.RunWhenAllowed(key1, (fun () -> true), d1)
        let! r2 = governor.RunWhenAllowed(key2, (fun () -> true), d2)

        equal "r1 Dispatched" Dispatched r1
        equal "r2 Dispatched" Dispatched r2

        let elapsed = d2Time - d1Time
        equal "elapsed matches rate limit exactly" 100.0 elapsed
    }

let test_concurrent_dispatches_no_overlap () =
    promise {
        let clock = MockClock(1000.0)
        let sleeper = MockSleeper(clock)

        let governor =
            RetryDispatchGovernor(rateLimitMs = 100L, clock = clock, sleeper = sleeper)

        let key = RetryModelKey.Create("ws1", "sess1", "provider1", "model1")
        let mutable activeCount = 0
        let mutable maxActiveCount = 0
        let mutable runOrder = ResizeArray<int>()

        let makeDispatch (id: int) () =
            promise {
                activeCount <- activeCount + 1

                if activeCount > maxActiveCount then
                    maxActiveCount <- activeCount

                equal "active count <= 1" true (activeCount <= 1)

                do! Promise.sleep 10 // allow yielding of microtasks

                runOrder.Add(id)
                activeCount <- activeCount - 1
            }

        let tasks =
            [ 1..5 ]
            |> List.map (fun id -> governor.RunWhenAllowed(key, (fun () -> true), makeDispatch id))

        let! results = Promise.all tasks

        for res in results do
            equal "Dispatched" Dispatched res

        equal "no overlapping dispatches" 1 maxActiveCount
        equal "executed in order" [ 1; 2; 3; 4; 5 ] (List.ofSeq runOrder)
        equal "slept 400ms across 5 tasks" 400.0 sleeper.TotalSleptMs
    }

let test_different_sessions_do_not_block_each_other () =
    promise {
        let clock = MockClock(1000.0)
        let sleeper = MockSleeper(clock)

        let governor =
            RetryDispatchGovernor(rateLimitMs = 100L, clock = clock, sleeper = sleeper)

        let key1 = RetryModelKey.Create("ws1", "sess1", "provider1", "model1")
        let key2 = RetryModelKey.Create("ws1", "sess2", "provider2", "model2")

        let completed = ResizeArray<string>()

        let d1 () =
            promise {
                do! Promise.sleep 50
                completed.Add("sess1")
            }

        let d2 () = promise { completed.Add("sess2") }

        let p1 = governor.RunWhenAllowed(key1, (fun () -> true), d1)
        let p2 = governor.RunWhenAllowed(key2, (fun () -> true), d2)

        let! _ = Promise.all [ p1; p2 ]

        equal "completed in non-blocking order" [ "sess2"; "sess1" ] (List.ofSeq completed)
    }

let test_cleanup () =
    promise {
        let clock = MockClock(1000.0)
        let sleeper = MockSleeper(clock)

        let governor =
            RetryDispatchGovernor(rateLimitMs = 10L, clock = clock, sleeper = sleeper)

        let key1 = RetryModelKey.Create("ws1", "sess1", "provider1", "model1")

        let! r1 = governor.RunWhenAllowed(key1, (fun () -> true), (fun () -> Promise.lift ()))
        equal "Dispatched" Dispatched r1

        clock.Advance(10.0)
        governor.Cleanup(50L)

        clock.Advance(50.0)
        governor.Cleanup(10L)

        governor.Reset()
    }

let run () =
    promise {
        do! test_session_serialization ()
        do! test_session_cancelled_before_dispatch ()
        do! test_provider_rate_limiting ()
        do! test_concurrent_dispatches_no_overlap ()
        do! test_different_sessions_do_not_block_each_other ()
        do! test_cleanup ()
    }
