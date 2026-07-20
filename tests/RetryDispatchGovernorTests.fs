module Wanxiangshu.Tests.RetryDispatchGovernorTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.RetryDispatchGovernorTestSupport
open Wanxiangshu.Runtime.Fallback.RetryDispatchGovernor

/// Two concurrent schedule calls on the same key cannot both send before the first completes.
let test_same_key_concurrent_serial_no_overlap () =
    promise {
        let governor = RetryDispatchGovernor(rateLimitMs = 0L)
        let key = transportKey "ws1" "provider1" "model1"
        let mutable activeCount = 0
        let mutable maxActiveCount = 0
        let mutable runOrder = ResizeArray<int>()

        let makeDispatch (id: int) () =
            promise {
                activeCount <- activeCount + 1

                if activeCount > maxActiveCount then
                    maxActiveCount <- activeCount

                equal "active count <= 1" true (activeCount <= 1)
                do! Promise.sleep 20
                runOrder.Add(id)
                activeCount <- activeCount - 1
            }

        let tasks =
            [ 1..5 ]
            |> List.map (fun id -> governor.RunWhenAllowed(key, (fun () -> true), makeDispatch id))

        let! results = Promise.all (Array.ofList tasks)

        for res in results do
            equal "Dispatched" Dispatched res

        equal "no overlapping dispatches" 1 maxActiveCount
        equal "executed in order" [ 1; 2; 3; 4; 5 ] (List.ofSeq runOrder)
    }

/// Concurrent same-key callers must not both observe empty stamp and fire together.
/// Virtual clock + sleeper: second wait is computed only after first stamps.
let test_same_key_concurrent_rate_limit_serial () =
    promise {
        let clock = MockClock(1000.0)
        let sleeper = MockSleeper(clock)
        let governor = makeGovernor 100L clock sleeper
        let key = transportKey "ws1" "provider1" "model1"
        let mutable sendTimes = ResizeArray<float>()
        let mutable inFlight = 0
        let mutable maxInFlight = 0

        let dispatch () =
            promise {
                inFlight <- inFlight + 1

                if inFlight > maxInFlight then
                    maxInFlight <- inFlight

                sendTimes.Add(clock.GetMonotonicTimeMs())
                do! Promise.sleep 5
                inFlight <- inFlight - 1
            }

        let p1 = governor.RunWhenAllowed(key, (fun () -> true), dispatch)
        let p2 = governor.RunWhenAllowed(key, (fun () -> true), dispatch)
        let! r1 = p1
        let! r2 = p2

        equal "r1 Dispatched" Dispatched r1
        equal "r2 Dispatched" Dispatched r2
        equal "never concurrent send" 1 maxInFlight
        equal "two sends" 2 sendTimes.Count
        equal "first send immediate" 1000.0 sendTimes.[0]
        equal "second send after rate window" 1100.0 sendTimes.[1]
        equal "one rate-limit sleep" 1 sleeper.SleepCount
        equal "slept full window" 100.0 sleeper.TotalSleptMs
    }

let test_cancelled_before_dispatch () =
    promise {
        let governor = RetryDispatchGovernor(rateLimitMs = 0L)
        let key = transportKey "ws1" "provider1" "model1"
        let mutable d1Run = false
        let mutable d2Run = false
        let mutable stillValidVal = true

        let d1 () =
            promise {
                do! Promise.sleep 30
                d1Run <- true
            }

        let d2 () = promise { d2Run <- true }

        let p1 = governor.RunWhenAllowed(key, (fun () -> true), d1)
        let p2 = governor.RunWhenAllowed(key, (fun () -> stillValidVal), d2)

        do! Promise.sleep 5
        stillValidVal <- false

        let! r1 = p1
        let! r2 = p2

        equal "r1 is Dispatched" Dispatched r1
        equal "r2 is CancelledBeforeDispatch" CancelledBeforeDispatch r2
        check "d1 ran" d1Run
        check "d2 did not run" (not d2Run)
    }

/// Same provider/model across sessions shares one transport key (credential scope).
let test_same_provider_shared_rate_limit_across_sessions () =
    promise {
        let clock = MockClock(1000.0)
        let sleeper = MockSleeper(clock)
        let governor = makeGovernor 100L clock sleeper
        let key = transportKey "ws1" "provider1" "model1"
        let mutable d1Time = 0.0
        let mutable d2Time = 0.0

        let d1 () = promise { d1Time <- clock.GetMonotonicTimeMs() }
        let d2 () = promise { d2Time <- clock.GetMonotonicTimeMs() }

        let p1 = governor.RunWhenAllowed(key, (fun () -> true), d1)
        let p2 = governor.RunWhenAllowed(key, (fun () -> true), d2)
        let! r1 = p1
        let! r2 = p2

        equal "r1 Dispatched" Dispatched r1
        equal "r2 Dispatched" Dispatched r2
        equal "elapsed matches rate limit" 100.0 (d2Time - d1Time)
    }

/// Different provider/model keys never block each other.
let test_different_keys_independent () =
    promise {
        let clock = MockClock(1000.0)
        let sleeper = MockSleeper(clock)
        let governor = makeGovernor 100L clock sleeper
        let key1 = transportKey "ws1" "provider1" "model1"
        let key2 = transportKey "ws1" "provider2" "model2"
        let completed = ResizeArray<string>()

        let d1 () =
            promise {
                do! Promise.sleep 40
                completed.Add("k1")
            }

        let d2 () = promise { completed.Add("k2") }

        let p1 = governor.RunWhenAllowed(key1, (fun () -> true), d1)
        let p2 = governor.RunWhenAllowed(key2, (fun () -> true), d2)
        let! _ = Promise.all [| p1; p2 |]

        equal "independent keys complete without mutual blocking" [ "k2"; "k1" ] (List.ofSeq completed)
        equal "no cross-key rate sleep" 0 sleeper.SleepCount
    }

/// Workspace is part of the transport key — no cross-workspace interference.
let test_different_workspaces_independent () =
    promise {
        let clock = MockClock(1000.0)
        let sleeper = MockSleeper(clock)
        let governor = makeGovernor 100L clock sleeper
        let keyA = transportKey "wsA" "provider1" "model1"
        let keyB = transportKey "wsB" "provider1" "model1"
        let mutable tA = 0.0
        let mutable tB = 0.0
        let mutable inFlight = 0
        let mutable sawConcurrent = false

        let dA () =
            promise {
                inFlight <- inFlight + 1

                if inFlight > 1 then
                    sawConcurrent <- true

                tA <- clock.GetMonotonicTimeMs()
                do! Promise.sleep 20
                inFlight <- inFlight - 1
            }

        let dB () =
            promise {
                inFlight <- inFlight + 1

                if inFlight > 1 then
                    sawConcurrent <- true

                tB <- clock.GetMonotonicTimeMs()
                do! Promise.sleep 20
                inFlight <- inFlight - 1
            }

        let pA = governor.RunWhenAllowed(keyA, (fun () -> true), dA)
        let pB = governor.RunWhenAllowed(keyB, (fun () -> true), dB)
        let! _ = Promise.all [| pA; pB |]

        check "workspaces may run concurrently" sawConcurrent
        equal "both immediate on virtual clock" 1000.0 tA
        equal "no shared rate stamp across workspaces" 1000.0 tB
        equal "no rate sleep across workspaces" 0 sleeper.SleepCount
    }

let test_variant_is_part_of_key () =
    promise {
        let clock = MockClock(1000.0)
        let sleeper = MockSleeper(clock)
        let governor = makeGovernor 100L clock sleeper
        let keyBase = ProviderModelTransportKey.Create("ws1", "p", "m")
        let keyVar = ProviderModelTransportKey.Create("ws1", "p", "m", "high")
        let mutable concurrent = false
        let mutable active = 0

        let d () =
            promise {
                active <- active + 1

                if active > 1 then
                    concurrent <- true

                do! Promise.sleep 15
                active <- active - 1
            }

        let! _ =
            Promise.all
                [| governor.RunWhenAllowed(keyBase, (fun () -> true), d)
                   governor.RunWhenAllowed(keyVar, (fun () -> true), d) |]

        check "variant splits transport key" concurrent
        equal "no shared rate sleep for different variants" 0 sleeper.SleepCount
    }

let test_cleanup_and_reset () =
    promise {
        let clock = MockClock(1000.0)
        let sleeper = MockSleeper(clock)
        let governor = makeGovernor 10L clock sleeper
        let key = transportKey "ws1" "provider1" "model1"

        let! r1 = governor.RunWhenAllowed(key, (fun () -> true), (fun () -> Promise.lift ()))
        equal "Dispatched" Dispatched r1

        clock.Advance(10.0)
        governor.Cleanup(50L)
        clock.Advance(50.0)
        governor.Cleanup(10L)
        governor.Reset()

        let! r2 = governor.RunWhenAllowed(key, (fun () -> true), (fun () -> Promise.lift ()))
        equal "after reset still Dispatched" Dispatched r2
        equal "reset cleared rate stamp" 0 sleeper.SleepCount
    }

let run () =
    promise {
        do! test_same_key_concurrent_serial_no_overlap ()
        do! test_same_key_concurrent_rate_limit_serial ()
        do! test_cancelled_before_dispatch ()
        do! test_same_provider_shared_rate_limit_across_sessions ()
        do! test_different_keys_independent ()
        do! test_different_workspaces_independent ()
        do! test_variant_is_part_of_key ()
        do! test_cleanup_and_reset ()
    }
