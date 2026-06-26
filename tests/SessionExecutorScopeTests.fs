module Wanxiangshu.Tests.SessionExecutorScopeTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.SessionExecutor

let twoScopesSameSessionIdQueuesIsolate () = promise {
    let scopeA = create ()
    let scopeB = create ()
    let execA = createForScope scopeA
    let execB = createForScope scopeB
    let seenA = System.Collections.Generic.List<string>()
    let seenB = System.Collections.Generic.List<string>()
    let releaseRequested = ref false
    let gateResolve = ref (fun () -> ())
    let gateAsync : JS.Promise<unit> =
        Promise.create (fun resolve _ ->
            gateResolve.Value <- resolve
            if releaseRequested.Value then resolve ())
    let sessionId = "shared-session-id"
    let firstA = execA.EnqueuePerSession(sessionId, fun () ->
        promise {
            seenA.Add "a-start"
            do! gateAsync
            seenA.Add "a-end"
            return "a"
        })
    let firstB = execB.EnqueuePerSession(sessionId, fun () ->
        promise {
            seenB.Add "b-start"
            do! gateAsync
            seenB.Add "b-end"
            return "b"
        })
    releaseRequested.Value <- true
    gateResolve.Value ()
    let! _ = firstA
    let! _ = firstB
    check "scope A serial order" (seenA |> Seq.toArray = [| "a-start"; "a-end" |])
    check "scope B serial order" (seenB |> Seq.toArray = [| "b-start"; "b-end" |])
}

let run () = promise {
    do! twoScopesSameSessionIdQueuesIsolate ()
}