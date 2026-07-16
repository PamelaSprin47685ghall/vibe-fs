module Wanxiangshu.Tests.ReviewerLoopTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Hosts.Opencode.ReviewerLoop

let makeFakeClient (store: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore) (childID: string) : obj =
    let session =
        createObj
            [ "create", box (fun (arg: obj) -> Promise.lift (box {| data = box {| id = childID |} |}))
              "prompt",
              box (fun (arg: obj) ->
                  Promise.lift (store.resolvePendingReview (childID, Accepted "looks good") |> ignore)) ]

    createObj [ "session", box session ]

let makeFakeClientNoSession () : obj = createObj []

let createReviewerChild_success () =
    promise {
        let store = createReviewStore ()
        let registry = ChildAgentRegistry.Create()
        let client = makeFakeClient store "child-1"
        let! childID = createReviewerChild registry client store "/tmp" (Some "parent-1") "parent-1" "Reviewer"
        equal "childID is child-1" "child-1" childID
        equal "child in registry" (Some "reviewer") (registry.LookupChildAgent "child-1")
    }

let createReviewerChild_noSessionApi () =
    promise {
        let store = createReviewStore ()
        let registry = ChildAgentRegistry.Create()
        let client = makeFakeClientNoSession ()
        let! childID = createReviewerChild registry client store "/tmp" None "s-1" "Reviewer"
        equal "no session returns empty" "" childID
    }

let runReviewerLoop_resolvesVerdict () =
    promise {
        let store = createReviewStore ()
        let registry = ChildAgentRegistry.Create()
        store.applyReviewTaskProjection ("child-1", Some "do review")
        let client = makeFakeClient store "child-1"
        let! childID = createReviewerChild registry client store "/tmp" (Some "parent-1") "parent-1" "Reviewer"
        let! result = runReviewerLoop client store "child-1" [ "reviewerPrompt test" ] null
        equal "resolved verdict" (Accepted "looks good") result
    }

let runReviewerLoop_rebindsPendingEachRound () =
    promise {
        let store = createReviewStore ()
        store.applyReviewTaskProjection ("child-1", Some "task")
        let mutable promptCount = 0
        let mutable secondRoundHadPending = false

        let session =
            createObj
                [ "prompt",
                  box (fun (_: obj) ->
                      promptCount <- promptCount + 1

                      if promptCount = 1 then
                          Promise.lift ()
                      else
                          secondRoundHadPending <- store.resolvePendingReview ("child-1", Accepted "nudge-round")

                          Promise.lift ()) ]

        let client = createObj [ "session", box session ]
        let! result = runReviewerLoop client store "child-1" [ "p1" ] null
        check "nudge round ran" (promptCount >= 2)
        check "second prompt could resolve pending" secondRoundHadPending
        equal "verdict from nudge round" (Accepted "nudge-round") result
    }

let runReviewerLoop_promptFailedReturnsTerminated () =
    promise {
        let failingClient =
            let session =
                createObj [ "prompt", box (fun (_: obj) -> Promise.reject (exn "prompt error")) ]

            createObj [ "session", box session ]

        let store = createReviewStore ()
        store.applyReviewTaskProjection ("s-1", Some "task")
        let! result = runReviewerLoop failingClient store "s-1" [ "reviewerPrompt test" ] null
        equal "prompt fail -> Terminated" Terminated result
    }

let runReviewerLoop_verdictAbortsPromptWithoutWaiting () =
    promise {
        let mutable promptResolved = false
        let neverResolving = Promise.create (fun _ _ -> ())

        let session =
            createObj
                [ "prompt",
                  box (fun (_: obj) ->
                      promise {
                          do! neverResolving
                          promptResolved <- true
                      }) ]

        let client = createObj [ "session", box session ]

        let store = createReviewStore ()
        store.applyReviewTaskProjection ("child-1", Some "task")

        // Start the loop in the background; the pending prompt will never
        // resolve on its own.  Resolve the review verdict in a later microtask
        // so the abort suppressor fires while the prompt is still pending.
        let loopTask = runReviewerLoop client store "child-1" [ "reviewerPrompt test" ] null

        let! () = yieldMicrotask ()
        store.resolvePendingReview ("child-1", Accepted "abort verdict") |> ignore
        let! result = loopTask

        equal "abort returns accepted verdict" (Accepted "abort verdict") result
        check "prompt never resolved on its own" (not promptResolved)
    }

let run () : JS.Promise<unit> =
    promise {
        do! createReviewerChild_success ()
        do! createReviewerChild_noSessionApi ()
        do! runReviewerLoop_resolvesVerdict ()
        do! runReviewerLoop_rebindsPendingEachRound ()
        do! runReviewerLoop_verdictAbortsPromptWithoutWaiting ()
        do! runReviewerLoop_promptFailedReturnsTerminated ()
    }
