/// Minimal regression demo for EventDrivenHarness primitives.
module Wanxiangshu.Tests.EventDrivenHarnessDemo

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.EventDrivenHarness
open Fable.Core.JsInterop

let run () : JS.Promise<unit> =
    promise {
        // 1. yieldMicrotask advances one chained promise
        let mutable flag = false

        let! _ =
            promise {
                do! yieldMicrotask ()
                flag <- true
            }

        check "yieldMicrotaskAdvances" flag

        // 2. drainMicrotasks 3 advances three chained promises
        let mutable count = 0

        let! _ =
            promise {
                do! drainMicrotasks 3
                count <- 3
            }

        check "drainMicrotasks3" (count = 3)

        // 3. EventBus: emit seqId monotonic + items preserve order
        let bus = EventBus<int>()
        let s0 = bus.emit 10
        let s1 = bus.emit 20
        let s2 = bus.emit 30
        check "busSeqMonotonic" (s0 = 0UL && s1 = 1UL && s2 = 2UL)
        check "busItemsOrder" (bus.items () = [ 10; 20; 30 ])
        check "busCount" (bus.count () = 3)

        // 4. assertEventSequence: length match + every predicate matches
        let bus2 = EventBus<string>()
        bus2.emit "a" |> ignore
        bus2.emit "b" |> ignore
        assertEventSequence "seqMatch" bus2 [ (fun s -> s = "a"); (fun s -> s = "b") ]

        // 5. MuxHarness streamEnd→nudge capture: nudgeBus contains one LoopNudge
        let nudgeBus = EventBus<NudgeEvent>()
        let capturedHook = ResizeArray<obj * obj>()

        let fakeReg =
            createObj
                [ "eventHook",
                  box (
                      System.Func<obj, obj, JS.Promise<unit>>(fun ev helpers ->
                          capturedHook.Add(ev, helpers)
                          // ponytail: mirror real MuxNudgeHelpers — nudge(ev, msg) must fire synchronously
                          // inside the hook so the fire-and-forget call from streamEnd actually enqueues nudgeBus
                          let nudgeFn = helpers?nudge |> unbox<System.Func<obj, obj, JS.Promise<bool>>>
                          nudgeFn.Invoke(ev, box "loop nudge test") |> ignore
                          Promise.lift ())
                  ) ]

        let harness = MuxHarness(fakeReg, nudgeBus, "test-session")
        do! harness.streamEnd (createObj [])
        assertEventSequence "streamEndLoopNudge" nudgeBus [ (fun ev -> ev.kind = LoopNudge) ]
        check "streamEndHookCalled" (capturedHook.Count = 1)
    }
