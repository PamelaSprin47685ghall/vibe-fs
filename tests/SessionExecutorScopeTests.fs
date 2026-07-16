module Wanxiangshu.Tests.SessionExecutorScopeTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.SessionExecutor

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

let rec flushMicrotasks n =
    promise {
        if n <= 0 then
            ()
        else
            do! yieldMicrotask ()
            do! flushMicrotasks (n - 1)
    }

let readerWriterLockConcurrencyAndMutualExclusion () =
    promise {
        let scope = create ()
        let exec = createForScope scope
        let sessionId = "rw-lock-test"

        let seen = System.Collections.Generic.List<string>()

        let mutable resolveGate1 = fun () -> ()
        let gate1 = Promise.create (fun r _ -> resolveGate1 <- r)

        let ro1 =
            exec.EnqueueExecutor(
                sessionId,
                "ro",
                fun () ->
                    promise {
                        seen.Add "ro1-start"
                        do! gate1
                        seen.Add "ro1-end"
                        return ()
                    }
            )

        let mutable resolveGate2 = fun () -> ()
        let gate2 = Promise.create (fun r _ -> resolveGate2 <- r)

        let ro2 =
            exec.EnqueueExecutor(
                sessionId,
                "ro",
                fun () ->
                    promise {
                        seen.Add "ro2-start"
                        do! gate2
                        seen.Add "ro2-end"
                        return ()
                    }
            )

        let mutable resolveGate3 = fun () -> ()
        let gate3 = Promise.create (fun r _ -> resolveGate3 <- r)

        let rw1 =
            exec.EnqueueExecutor(
                sessionId,
                "rw",
                fun () ->
                    promise {
                        seen.Add "rw1-start"
                        do! gate3
                        seen.Add "rw1-end"
                        return ()
                    }
            )

        let ro3 =
            exec.EnqueueExecutor(
                sessionId,
                "ro",
                fun () ->
                    promise {
                        seen.Add "ro3-start"
                        return ()
                    }
            )

        do! flushMicrotasks 30

        check "ro1 and ro2 concurrent started" (seen.Contains "ro1-start" && seen.Contains "ro2-start")
        check "rw1 not started yet" (not (seen.Contains "rw1-start"))
        check "ro3 not started yet" (not (seen.Contains "ro3-start"))

        resolveGate1 ()
        do! ro1
        do! flushMicrotasks 30
        check "rw1 still waiting on ro2" (not (seen.Contains "rw1-start"))

        resolveGate2 ()
        do! ro2
        do! flushMicrotasks 30
        check "rw1 started after all readers done" (seen.Contains "rw1-start")
        check "ro3 still waiting on rw1" (not (seen.Contains "ro3-start"))

        resolveGate3 ()
        do! rw1
        do! ro3
        check "ro3 completed last" (seen.Contains "ro3-start")

        let idxRo1End = seen.IndexOf "ro1-end"
        let idxRo2End = seen.IndexOf "ro2-end"
        let idxRw1Start = seen.IndexOf "rw1-start"
        let idxRw1End = seen.IndexOf "rw1-end"
        let idxRo3Start = seen.IndexOf "ro3-start"

        check "rw1 after ro1-end" (idxRw1Start > idxRo1End)
        check "rw1 after ro2-end" (idxRw1Start > idxRo2End)
        check "ro3 after rw1-end" (idxRo3Start > idxRw1End)
    }

let run () =
    promise {
        do! twoScopesSameSessionIdQueuesIsolate ()
        do! readerWriterLockConcurrencyAndMutualExclusion ()
    }
