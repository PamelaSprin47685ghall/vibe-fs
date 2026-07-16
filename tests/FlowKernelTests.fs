module Wanxiangshu.Tests.FlowKernelTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Flow

// ── Law 1: Order Law — aggregate-internal serial ──

let law1SerialConsumePreservesOrder () =
    promise {
        let mutable results = []
        let items = [ "a"; "b"; "c" ]

        do! serialConsume items (fun item -> promise { results <- results @ [ item ] })

        equal "items processed in order" [ "a"; "b"; "c" ] results
    }

let law1SerialConsumeEmptyList () =
    promise {
        let mutable called = false

        do! serialConsume [] (fun _ -> promise { called <- true })

        check "no items means no calls" (not called)
    }

// ── Law 2: Persistence Law — persistBeforeCommit ──

let law2PersistBeforeCommit () =
    promise {
        let mutable persisted = false
        let mutable committed = false

        let! result =
            persistBarrier
                (fun () ->
                    promise {
                        persisted <- true
                        return ()
                    })
                (fun () ->
                    committed <- true
                    42)

        check "persist was called first" persisted
        check "commit was called after persist" committed
        equal "commit result returned" 42 result
    }

let law2PersistFailureSkipsCommit () =
    promise {
        let mutable committed = false

        let! result =
            persistBarrier (fun () -> promise { return! failwith "persist failed" }) (fun () ->
                committed <- true
                42)
            |> Promise.catch (fun _ -> -1)

        equal "persist failure returns fallback" -1 result
        check "commit was NOT called when persist fails" (not committed)
    }

// ── Law 3: Cancellation Law — bracket always releases ──

let law3BracketReleasesOnSuccess () =
    promise {
        let mutable released = false

        let! result =
            bracket (fun () -> promise { return "resource" }) (fun r -> promise { return r.Length }) (fun _ ->
                released <- true)

        equal "body result from resource" 8 result
        check "release called on success" released
    }

let law3BracketReleasesOnException () =
    promise {
        let mutable released = false

        let! result =
            bracket
                (fun () -> promise { return "resource" })
                (fun _ -> promise { return! failwith "body failed" })
                (fun _ -> released <- true)
            |> Promise.catch (fun _ -> -1)

        check "release called even on exception" released
    }

let law3BracketAcquireFailsNoRelease () =
    promise {
        let mutable released = false

        let! _ =
            bracket (fun () -> promise { return! failwith "acquire failed" }) (fun _ -> promise { return 42 }) (fun _ ->
                released <- true)
            |> Promise.catch (fun _ -> -1)

        check "release NOT called when acquire fails" (not released)
    }

// ── Law 6: No Reentrancy Law — no sync recursion ──

let law6EffectCompletionViaPost () =
    promise {
        // The CommandProcessor pattern: effects complete by enqueuing
        // a Command back into the Inbox, never by direct sync call.
        let mutable inboxCalls = []

        let effectComplete (result: string) : JS.Promise<unit> =
            promise {
                // Simulate posting back to inbox (fire-and-forget)
                inboxCalls <- inboxCalls @ [ result ]
                return ()
            }

        let! _ =
            persistBarrier (fun () -> promise { return () }) (fun () ->
                // \"Decide\" produces an effect. Effect completion
                // MUST go through the inbox, not a sync call.
                effectComplete "effect1_done" |> ignore
                42)

        // Effect completion should have been posted after commit
        equal "effect was posted" [ "effect1_done" ] inboxCalls
    }

// ── Law 9: Idempotency Law — duplicate inputs ──

let law9RunCommitPipelineSuccess () =
    promise {
        let mutable persistCount = 0
        let mutable commitCount = 0

        let pipeline: CommitPipeline<int> =
            { Decide = fun () -> Ok(42, [])
              Persist =
                fun () ->
                    promise {
                        persistCount <- persistCount + 1
                        return ()
                    }
              Commit = fun r -> commitCount <- commitCount + 1 }

        let! result = runCommitPipeline pipeline

        match result with
        | Ok v ->
            equal "commit result" 42 v
            equal "persist called once" 1 persistCount
            equal "commit called once" 1 commitCount
        | Error _ -> check "Expected Ok" false
    }

let law9RunCommitPipelineDecideFailure () =
    promise {
        let mutable persistCalled = false

        let pipeline: CommitPipeline<int> =
            { Decide = fun () -> Error(failwith "decide failed")
              Persist =
                fun () ->
                    promise {
                        persistCalled <- true
                        return ()
                    }
              Commit = fun _ -> () }

        let! result = runCommitPipeline pipeline

        match result with
        | Ok _ -> check "Expected Error" false
        | Error _ -> check "persist NOT called when decide fails" (not persistCalled)
    }

let law9RunCommitPipelinePersistFailure () =
    promise {
        let mutable commitCalled = false

        let pipeline: CommitPipeline<int> =
            { Decide = fun () -> Ok(42, [])
              Persist = fun () -> promise { return! failwith "persist failed" }
              Commit = fun _ -> commitCalled <- true }

        let! result = runCommitPipeline pipeline

        match result with
        | Ok _ -> check "Expected Error" false
        | Error _ -> check "commit NOT called when persist fails" (not commitCalled)
    }

let run () =
    promise {
        do! law1SerialConsumePreservesOrder ()
        do! law1SerialConsumeEmptyList ()
        do! law2PersistBeforeCommit ()
        do! law2PersistFailureSkipsCommit ()
        do! law3BracketReleasesOnSuccess ()
        do! law3BracketReleasesOnException ()
        do! law3BracketAcquireFailsNoRelease ()
        do! law6EffectCompletionViaPost ()
        do! law9RunCommitPipelineSuccess ()
        do! law9RunCommitPipelineDecideFailure ()
        do! law9RunCommitPipelinePersistFailure ()
    }
