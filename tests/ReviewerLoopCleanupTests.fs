module Wanxiangshu.Tests.ReviewerLoopCleanupTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Hosts.Opencode.ReviewerLoop
open Wanxiangshu.Tests.SubagentPromptAbortTests

let runReviewerLoop_parentAbortSignalPropagates () =
    promise {
        let! directory = mkdtempAsync "test-abort-propagate-"
        let mutable loopError = None
        try
            let registry = ChildAgentRegistry.Create()
            let store = createReviewStore ()
            store.applyReviewTaskProjection ("child-1", Some "task")
            let session = createObj [ "prompt", box (fun (_: obj) -> Promise.create (fun _ _ -> ())); "abort", box (fun (_: obj) -> Promise.lift ()) ]
            let client = createObj [ "session", box session ]
            let parentSignal, triggerAbort = createMockAbortSignal ()
            let loopTask = runReviewerLoop registry client store directory "child-1" [ "reviewerPrompt test" ] parentSignal
            let! () = yieldMicrotask ()
            triggerAbort ()
            let mutable threwAbort = false
            try
                let! _ = loopTask
                ()
            with err ->
                match Wanxiangshu.Runtime.ErrorClassify.translateJsError err with
                | Wanxiangshu.Kernel.Errors.DomainError.ClientCancellation _
                | Wanxiangshu.Kernel.Errors.DomainError.MessageAborted -> threwAbort <- true
                | _ -> ()
            check "loopTask threw abort exception" threwAbort
        with ex -> loopError <- Some ex

        do! rmAsync directory
        match loopError with Some ex -> raise ex | None -> ()
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
                    [ "create", box (fun (_: obj) -> Promise.lift (box {| data = box {| id = "child-1" |} |}))
                      "prompt", box (fun (_: obj) -> Promise.lift (store.resolvePendingReview ("child-1", Accepted [ "looks good" ]) |> ignore))
                      "abort", box (fun (_: obj) -> abortCalled <- true; Promise.lift ())
                      "delete", box (fun (_: obj) -> deleteCalled <- true; Promise.lift ()) ]
            let client = createObj [ "session", box session ]
            let! _ = createReviewerChild registry client store directory (Some "parent-1") "parent-1" "Reviewer"
            check "initially registered" (registry.LookupChildAgent "child-1" = Some "reviewer")
            let! _ = runReviewerLoop registry client store directory "child-1" [ "reviewerPrompt test" ] null
            check "unregistered from registry" (registry.LookupChildAgent "child-1" = None)
            check "host abort called on success cleanup" abortCalled
            check "delete was NOT called on host" (not deleteCalled)
            check "reviewState is cleaned up in store" (store.getReviewState "child-1" = None)
        with ex -> loopError <- Some ex

        do! rmAsync directory
        match loopError with Some ex -> raise ex | None -> ()
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
                    [ "create", box (fun (_: obj) -> Promise.lift (box {| data = box {| id = "child-1" |} |}))
                      "prompt", box (fun (_: obj) -> Promise.reject (exn "prompt error"))
                      "abort", box (fun (_: obj) -> abortCalled <- true; Promise.lift ())
                      "delete", box (fun (_: obj) -> deleteCalled <- true; Promise.lift ()) ]
            let client = createObj [ "session", box session ]
            let! _ = createReviewerChild registry client store directory (Some "parent-1") "parent-1" "Reviewer"
            let! _ = runReviewerLoop registry client store directory "child-1" [ "reviewerPrompt test" ] null
            check "unregistered from registry on error" (registry.LookupChildAgent "child-1" = None)
            check "host abort called on error cleanup" abortCalled
            check "delete was NOT called on host on error" (not deleteCalled)
            check "reviewState is cleaned up in store on error" (store.getReviewState "child-1" = None)
        with ex -> loopError <- Some ex

        do! rmAsync directory
        match loopError with Some ex -> raise ex | None -> ()
    }

let runReviewerLoop_cleanupExceptionDoesNotMaskOriginalError () =
    promise {
        let! directory = mkdtempAsync "test-cleanup-mask-"
        let mutable loopError = None
        try
            let registry = ChildAgentRegistry.Create()
            let store = createReviewStore ()
            store.applyReviewTaskProjection ("child-1", Some "task")
            let session =
                createObj
                    [ "create", box (fun (_: obj) -> Promise.lift (box {| data = box {| id = "child-1" |} |}))
                      "prompt", box (fun (_: obj) -> Promise.reject (unbox<exn> (createObj [ "name", box "MessageAbortedError"; "message", box "original prompt error" ])))
                      "abort", box (fun (_: obj) -> Promise.reject (exn "cleanup abort error")) ]
            let client = createObj [ "session", box session ]
            let! _ = createReviewerChild registry client store directory (Some "parent-1") "parent-1" "Reviewer"
            let mutable threwOriginalError = false
            try
                let! _ = runReviewerLoop registry client store directory "child-1" [ "reviewerPrompt test" ] null
                ()
            with err ->
                if err.Message.Contains "original prompt error" then threwOriginalError <- true

            check "threw the original error, not the cleanup error" threwOriginalError
        with ex -> loopError <- Some ex

        do! rmAsync directory
        match loopError with Some ex -> raise ex | None -> ()
    }

let run () : JS.Promise<unit> =
    promise {
        do! runReviewerLoop_parentAbortSignalPropagates ()
        do! runReviewerLoop_finallyCleansUpEverything ()
        do! runReviewerLoop_finallyCleansUpEverythingOnError ()
        do! runReviewerLoop_cleanupExceptionDoesNotMaskOriginalError ()
    }
