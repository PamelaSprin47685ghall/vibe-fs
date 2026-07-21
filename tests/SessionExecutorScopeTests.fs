module Wanxiangshu.Tests.SessionExecutorScopeTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.SessionExecutor

let rec private flushMicrotasks n =
    promise {
        if n <= 0 then
            ()
        else
            do! yieldMicrotask ()
            do! flushMicrotasks (n - 1)
    }

let twoScopesSameSessionIdQueuesIsolate () =
    promise {
        let scopeA = create ()
        let scopeB = create ()
        let execA = createForScope scopeA
        let execB = createForScope scopeB
        let seenA = System.Collections.Generic.List<string>()
        let seenB = System.Collections.Generic.List<string>()
        let releaseRequested = ref false
        let gateResolve = ref (fun () -> ())

        let gateAsync: JS.Promise<unit> =
            Promise.create (fun resolve _ ->
                gateResolve.Value <- resolve

                if releaseRequested.Value then
                    resolve ())

        let sessionId = "shared-session-id"

        let firstA =
            execA.EnqueuePerSession(
                sessionId,
                fun () ->
                    promise {
                        seenA.Add "a-start"
                        do! gateAsync
                        seenA.Add "a-end"
                        return "a"
                    }
            )

        let firstB =
            execB.EnqueuePerSession(
                sessionId,
                fun () ->
                    promise {
                        seenB.Add "b-start"
                        do! gateAsync
                        seenB.Add "b-end"
                        return "b"
                    }
            )

        releaseRequested.Value <- true
        gateResolve.Value()
        let! _ = firstA
        let! _ = firstB
        check "scope A serial order" (seenA |> Seq.toArray = [| "a-start"; "a-end" |])
        check "scope B serial order" (seenB |> Seq.toArray = [| "b-start"; "b-end" |])
    }

let testSameSessionStrictFifoSerial () =
    promise {
        let scope = create ()
        let exec = createForScope scope
        let sessionId = "fifo-test-session"
        let seen = System.Collections.Generic.List<string>()

        let mutable resolveGate1 = fun () -> ()
        let gate1 = Promise.create (fun r _ -> resolveGate1 <- r)

        let item1 =
            exec.EnqueueExecutor(
                sessionId,
                fun () ->
                    promise {
                        seen.Add "1-start"
                        do! gate1
                        seen.Add "1-end"
                        return "1"
                    }
            )

        let item2 =
            exec.EnqueueExecutor(
                sessionId,
                fun () ->
                    promise {
                        seen.Add "2-start"
                        seen.Add "2-end"
                        return "2"
                    }
            )

        do! flushMicrotasks 20
        check "item1 started, item2 waiting" (seen.Contains "1-start" && not (seen.Contains "2-start"))

        resolveGate1 ()
        let! r1 = item1
        let! r2 = item2
        equal "item1 result" "1" r1
        equal "item2 result" "2" r2
        equal "execution order" [| "1-start"; "1-end"; "2-start"; "2-end" |] (seen |> Seq.toArray)
    }

let testDifferentSessionsParallel () =
    promise {
        let scope = create ()
        let exec = createForScope scope
        let seen = System.Collections.Generic.List<string>()

        let mutable resolveGateA = fun () -> ()
        let gateA = Promise.create (fun r _ -> resolveGateA <- r)

        let itemA =
            exec.EnqueueExecutor(
                "session-A",
                fun () ->
                    promise {
                        seen.Add "A-start"
                        do! gateA
                        seen.Add "A-end"
                        return "A"
                    }
            )

        let itemB =
            exec.EnqueueExecutor(
                "session-B",
                fun () ->
                    promise {
                        seen.Add "B-start"
                        seen.Add "B-end"
                        return "B"
                    }
            )

        do! flushMicrotasks 20
        check "session B runs in parallel with session A" (seen.Contains "A-start" && seen.Contains "B-start")

        resolveGateA ()
        let! _ = itemA
        let! _ = itemB
        ()
    }

let testPredecessorErrorDoesNotBreakTail () =
    promise {
        let scope = create ()
        let exec = createForScope scope
        let sessionId = "error-tail-session"

        let item1 =
            exec.EnqueueExecutor(sessionId, (fun () -> promise { return raise (exn "item1 failed") }))

        let item2 =
            exec.EnqueueExecutor(sessionId, (fun () -> promise { return "item2 success" }))

        let mutable item1Failed = false

        try
            let! _ = item1
            ()
        with ex ->
            item1Failed <- ex.Message.Contains "item1 failed"

        check "item1 raised exception" item1Failed

        let! r2 = item2
        equal "item2 executed successfully despite item1 error" "item2 success" r2
    }

let testCloseSessionExecutorRejectsNewEnqueues () =
    promise {
        let scope = create ()
        let exec = createForScope scope
        let sessionId = "close-session"

        let! firstRes = exec.EnqueuePerSession(sessionId, (fun () -> promise { return "first" }))
        equal "first execution" "first" firstRes

        scope.CloseSessionExecutor(sessionId)
        let mutable rejected = false

        try
            let! _ = exec.EnqueuePerSession(sessionId, (fun () -> promise { return "second" }))
            ()
        with ex ->
            rejected <- ex.Message.Contains "session executor is closed"

        check "new enqueue rejected after CloseSessionExecutor" rejected
    }

let testActiveRunIdUnregister () =
    promise {
        let scope = create ()
        let sid = "active-run-test"
        check "initially inactive" (not (hasActiveExecutorRun scope sid))

        let unreg1 = registerActiveRun scope sid (fun () -> ())
        let unreg2 = registerActiveRun scope sid (fun () -> ())
        check "active after register" (hasActiveExecutorRun scope sid)

        unreg1 ()
        check "still active with 1 run remaining" (hasActiveExecutorRun scope sid)

        unreg2 ()
        check "inactive after all unregistered" (not (hasActiveExecutorRun scope sid))
    }

let run () =
    promise {
        do! twoScopesSameSessionIdQueuesIsolate ()
        do! testSameSessionStrictFifoSerial ()
        do! testDifferentSessionsParallel ()
        do! testPredecessorErrorDoesNotBreakTail ()
        do! testCloseSessionExecutorRejectsNewEnqueues ()
        do! testActiveRunIdUnregister ()
    }
