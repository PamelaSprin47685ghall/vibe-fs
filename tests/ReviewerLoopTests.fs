module Wanxiangshu.Tests.ReviewerLoopTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Tests.TestWorkspace
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
                  Promise.lift (store.resolvePendingReview (childID, Accepted "looks good") |> ignore))
              "abort", box (fun (arg: obj) -> Promise.lift ())
              "delete", box (fun (arg: obj) -> Promise.lift ()) ]

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
        let! result = runReviewerLoop registry client store "/tmp" "child-1" [ "reviewerPrompt test" ] null
        equal "resolved verdict" (Accepted "looks good") result
    }

let runReviewerLoop_rebindsPendingEachRound () =
    promise {
        let store = createReviewStore ()
        let registry = ChildAgentRegistry.Create()
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

                          Promise.lift ())
                  "abort", box (fun (arg: obj) -> Promise.lift ())
                  "delete", box (fun (arg: obj) -> Promise.lift ()) ]

        let client = createObj [ "session", box session ]
        let! result = runReviewerLoop registry client store "/tmp" "child-1" [ "p1" ] null
        check "nudge round ran" (promptCount >= 2)
        check "second prompt could resolve pending" secondRoundHadPending
        equal "verdict from nudge round" (Accepted "nudge-round") result
    }

let runReviewerLoop_promptFailedReturnsTerminated () =
    promise {
        let registry = ChildAgentRegistry.Create()

        let failingClient =
            let session =
                createObj
                    [ "prompt", box (fun (_: obj) -> Promise.reject (exn "prompt error"))
                      "abort", box (fun (arg: obj) -> Promise.lift ())
                      "delete", box (fun (arg: obj) -> Promise.lift ()) ]

            createObj [ "session", box session ]

        let store = createReviewStore ()
        store.applyReviewTaskProjection ("s-1", Some "task")
        let! result = runReviewerLoop registry failingClient store "/tmp" "s-1" [ "reviewerPrompt test" ] null
        equal "prompt fail -> Terminated" Terminated result
    }

let runReviewerLoop_verdictAbortsPromptWithoutWaiting () =
    promise {
        let registry = ChildAgentRegistry.Create()
        let mutable promptResolved = false
        let neverResolving = Promise.create (fun _ _ -> ())

        let session =
            createObj
                [ "prompt",
                  box (fun (_: obj) ->
                      promise {
                          do! neverResolving
                          promptResolved <- true
                      })
                  "abort", box (fun (arg: obj) -> Promise.lift ())
                  "delete", box (fun (arg: obj) -> Promise.lift ()) ]

        let client = createObj [ "session", box session ]

        let store = createReviewStore ()
        store.applyReviewTaskProjection ("child-1", Some "task")

        // Start the loop in the background; the pending prompt will never
        // resolve on its own.  Resolve the review verdict in a later microtask
        // so the abort suppressor fires while the prompt is still pending.
        let loopTask =
            runReviewerLoop registry client store "/tmp" "child-1" [ "reviewerPrompt test" ] null

        let! () = yieldMicrotask ()
        store.resolvePendingReview ("child-1", Accepted "abort verdict") |> ignore
        let! result = loopTask

        equal "abort returns accepted verdict" (Accepted "abort verdict") result
        check "prompt never resolved on its own" (not promptResolved)
    }

let createMockAbortSignal () =
    let handlers = ResizeArray<unit -> unit>()

    let signal =
        createObj
            [ "aborted", box false
              "addEventListener",
              box (
                  System.Action<string, unit -> unit>(fun event handler ->
                      printfn "[DEBUG] mockAbortSignal: addEventListener called for event %s" event

                      if event = "abort" then
                          handlers.Add(handler) |> ignore)
              )
              "removeEventListener",
              box (
                  System.Action<string, unit -> unit>(fun event handler ->
                      printfn "[DEBUG] mockAbortSignal: removeEventListener called for event %s" event

                      if event = "abort" then
                          handlers.Remove(handler) |> ignore)
              ) ]

    let trigger () =
        printfn "[DEBUG] mockAbortSignal: trigger called, handlers count = %d" handlers.Count
        signal?aborted <- true

        for h in handlers do
            h ()

    signal, trigger

let runReviewerLoop_parentAbortSignalPropagates () =
    promise {
        let! directory = mkdtempAsync "test-abort-propagate-"
        let mutable loopError = None

        try
            let registry = ChildAgentRegistry.Create()
            let store = createReviewStore ()
            store.applyReviewTaskProjection ("child-1", Some "task")

            let session =
                createObj
                    [ "prompt",
                      box (fun (args: obj) ->
                          // Never resolve, simulating a pending prompt
                          Promise.create (fun _ _ -> ()))
                      "abort", box (fun (arg: obj) -> Promise.lift ())
                      "delete", box (fun (arg: obj) -> Promise.lift ()) ]

            let client = createObj [ "session", box session ]

            let parentSignal, triggerAbort = createMockAbortSignal ()

            let loopTask =
                runReviewerLoop registry client store directory "child-1" [ "reviewerPrompt test" ] parentSignal

            let! () = yieldMicrotask ()
            // Trigger abort on parent signal
            triggerAbort ()

            // Verify that loopTask rejects with AbortError/DOMException
            let mutable threwAbort = false

            try
                let! _ = loopTask
                ()
            with err ->
                let domainErr = Wanxiangshu.Runtime.ErrorClassify.translateJsError err

                match domainErr with
                | Wanxiangshu.Kernel.Errors.DomainError.ClientCancellation _
                | Wanxiangshu.Kernel.Errors.DomainError.MessageAborted -> threwAbort <- true
                | _ -> ()

            check "loopTask threw abort exception" threwAbort
        with ex ->
            loopError <- Some ex

        do! rmAsync directory

        match loopError with
        | Some ex -> raise ex
        | None -> ()
    }

let runReviewerLoop_parentAbortSignalAlreadyAborted () =
    promise {
        let! directory = mkdtempAsync "test-abort-already-"
        let mutable loopError = None

        try
            let registry = ChildAgentRegistry.Create()
            let store = createReviewStore ()
            store.applyReviewTaskProjection ("child-1", Some "task")

            let session =
                createObj
                    [ "prompt", box (fun (arg: obj) -> Promise.lift ())
                      "abort", box (fun (arg: obj) -> Promise.lift ())
                      "delete", box (fun (arg: obj) -> Promise.lift ()) ]

            let client = createObj [ "session", box session ]

            let parentSignal = createObj [ "aborted", box true ]

            let loopTask =
                runReviewerLoop registry client store directory "child-1" [ "reviewerPrompt test" ] parentSignal

            let mutable threwAbort = false

            try
                let! _ = loopTask
                ()
            with err ->
                let domainErr = Wanxiangshu.Runtime.ErrorClassify.translateJsError err

                match domainErr with
                | Wanxiangshu.Kernel.Errors.DomainError.ClientCancellation _
                | Wanxiangshu.Kernel.Errors.DomainError.MessageAborted -> threwAbort <- true
                | _ -> ()

            check "loopTask with already aborted signal threw abort exception" threwAbort
        with ex ->
            loopError <- Some ex

        do! rmAsync directory

        match loopError with
        | Some ex -> raise ex
        | None -> ()
    }

let runReviewerLoop_finallyCleansUpEverything () =
    promise {
        let! directory = mkdtempAsync "test-cleanup-success-"
        let mutable loopError = None

        try
            let registry = ChildAgentRegistry.Create()
            let store = createReviewStore ()
            store.applyReviewTaskProjection ("child-1", Some "task")

            let mutable abortCalled = false
            let mutable deleteCalled = false

            let session =
                createObj
                    [ "create", box (fun (arg: obj) -> Promise.lift (box {| data = box {| id = "child-1" |} |}))
                      "prompt",
                      box (fun (arg: obj) ->
                          Promise.lift (store.resolvePendingReview ("child-1", Accepted "looks good") |> ignore))
                      "abort",
                      box (fun (arg: obj) ->
                          abortCalled <- true
                          Promise.lift ())
                      "delete",
                      box (fun (arg: obj) ->
                          deleteCalled <- true
                          Promise.lift ()) ]

            let client = createObj [ "session", box session ]

            // Register the child first
            let! childID = createReviewerChild registry client store directory (Some "parent-1") "parent-1" "Reviewer"

            // Lock state should be active before running
            check "initially registered" (registry.LookupChildAgent "child-1" = Some "reviewer")

            let! _ = runReviewerLoop registry client store directory "child-1" [ "reviewerPrompt test" ] null

            // After completion:
            check "unregistered from registry" (registry.LookupChildAgent "child-1" = None)
            check "delete was called on host" deleteCalled
            check "reviewState is cleaned up in store" (store.getReviewState "child-1" = None)
        with ex ->
            loopError <- Some ex

        do! rmAsync directory

        match loopError with
        | Some ex -> raise ex
        | None -> ()
    }

let runReviewerLoop_finallyCleansUpEverythingOnError () =
    promise {
        let! directory = mkdtempAsync "test-cleanup-error-"
        let mutable loopError = None

        try
            let registry = ChildAgentRegistry.Create()
            let store = createReviewStore ()
            store.applyReviewTaskProjection ("child-1", Some "task")

            let mutable abortCalled = false
            let mutable deleteCalled = false

            let session =
                createObj
                    [ "create", box (fun (arg: obj) -> Promise.lift (box {| data = box {| id = "child-1" |} |}))
                      "prompt", box (fun (arg: obj) -> Promise.reject (exn "prompt error"))
                      "abort",
                      box (fun (arg: obj) ->
                          abortCalled <- true
                          Promise.lift ())
                      "delete",
                      box (fun (arg: obj) ->
                          deleteCalled <- true
                          Promise.lift ()) ]

            let client = createObj [ "session", box session ]

            let! childID = createReviewerChild registry client store directory (Some "parent-1") "parent-1" "Reviewer"

            let! _ = runReviewerLoop registry client store directory "child-1" [ "reviewerPrompt test" ] null

            check "unregistered from registry on error" (registry.LookupChildAgent "child-1" = None)
            check "delete was called on host on error" deleteCalled
            check "reviewState is cleaned up in store on error" (store.getReviewState "child-1" = None)
        with ex ->
            loopError <- Some ex

        do! rmAsync directory

        match loopError with
        | Some ex -> raise ex
        | None -> ()
    }

let runReviewerLoop_finallyCleansUpEverythingOnAbort () =
    promise {
        let! directory = mkdtempAsync "test-cleanup-abort-"
        let mutable loopError = None

        try
            let registry = ChildAgentRegistry.Create()
            let store = createReviewStore ()
            store.applyReviewTaskProjection ("child-1", Some "task")

            let mutable abortCalled = false
            let mutable deleteCalled = false

            let session =
                createObj
                    [ "create", box (fun (arg: obj) -> Promise.lift (box {| data = box {| id = "child-1" |} |}))
                      "prompt", box (fun (arg: obj) -> Promise.create (fun _ _ -> ()))
                      "abort",
                      box (fun (arg: obj) ->
                          abortCalled <- true
                          Promise.lift ())
                      "delete",
                      box (fun (arg: obj) ->
                          deleteCalled <- true
                          Promise.lift ()) ]

            let client = createObj [ "session", box session ]

            let! childID = createReviewerChild registry client store directory (Some "parent-1") "parent-1" "Reviewer"

            let parentSignal, triggerAbort = createMockAbortSignal ()

            let loopTask =
                runReviewerLoop registry client store directory "child-1" [ "reviewerPrompt test" ] parentSignal

            let! () = yieldMicrotask ()
            triggerAbort ()

            try
                let! _ = loopTask
                ()
            with _ ->
                ()

            check "unregistered from registry on abort" (registry.LookupChildAgent "child-1" = None)
            check "delete was called on host on abort" deleteCalled
            check "reviewState is cleaned up in store on abort" (store.getReviewState "child-1" = None)
        with ex ->
            loopError <- Some ex

        do! rmAsync directory

        match loopError with
        | Some ex -> raise ex
        | None -> ()
    }

let run () : JS.Promise<unit> =
    promise {
        printfn "[TEST] createReviewerChild_success"
        do! createReviewerChild_success ()
        printfn "[TEST] createReviewerChild_noSessionApi"
        do! createReviewerChild_noSessionApi ()
        printfn "[TEST] runReviewerLoop_resolvesVerdict"
        do! runReviewerLoop_resolvesVerdict ()
        printfn "[TEST] runReviewerLoop_rebindsPendingEachRound"
        do! runReviewerLoop_rebindsPendingEachRound ()
        printfn "[TEST] runReviewerLoop_verdictAbortsPromptWithoutWaiting"
        do! runReviewerLoop_verdictAbortsPromptWithoutWaiting ()
        printfn "[TEST] runReviewerLoop_promptFailedReturnsTerminated"
        do! runReviewerLoop_promptFailedReturnsTerminated ()
        printfn "[TEST] runReviewerLoop_parentAbortSignalPropagates"
        do! runReviewerLoop_parentAbortSignalPropagates ()
        printfn "[TEST] runReviewerLoop_parentAbortSignalAlreadyAborted"
        do! runReviewerLoop_parentAbortSignalAlreadyAborted ()
        printfn "[TEST] runReviewerLoop_finallyCleansUpEverything"
        do! runReviewerLoop_finallyCleansUpEverything ()
        printfn "[TEST] runReviewerLoop_finallyCleansUpEverythingOnError"
        do! runReviewerLoop_finallyCleansUpEverythingOnError ()
        printfn "[TEST] runReviewerLoop_finallyCleansUpEverythingOnAbort"
        do! runReviewerLoop_finallyCleansUpEverythingOnAbort ()
        printfn "[TEST] All tests inside ReviewerLoopTests finished"
    }
