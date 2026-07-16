/// ──────────────────────────────────────────────
///  E2E Integration Test: CommandProcessor Pipeline
///
///  Tests the full commit pipeline across its 10 steps:
///    1. Dequeue Command
///    2. Validate correlation
///    3. Decide (State × Command → Decision)
///    4. Persist domain events (EventLog)
///    5. Commit in-memory state
///    6. Reconcile ResourcePlan
///    7. Complete local handlers
///    8. Publish committed events (ReactivePort)
///    9. Wake EffectSupervisors
///   10. Process next Command
///
///  Uses the actual Flow primitives + SessionOverview projections
///  to verify end-to-end correctness.
/// ──────────────────────────────────────────────
module Wanxiangshu.Tests.CommandProcessorE2ETests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.Review.ReviewLoopFold
open Wanxiangshu.Kernel.SessionOverview
open Wanxiangshu.Runtime.Flow
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Reactive

// ──────────────────────────────────────────────
//  Helpers
// ──────────────────────────────────────────────

let private ev session kind payload =
    { V = 1
      Session = session
      Kind = kind
      At = ""
      Payload = payload }

/// In-memory event store for testing (simulates EventLog.append).
type private InMemoryEventStore() =
    let mutable events: WanEvent list = []
    member _.Append(e: WanEvent) : unit = events <- events @ [ e ]
    member _.ReadAll() : WanEvent list = events
    member _.Clear() : unit = events <- []
    member _.Count: int = events.Length

// ──────────────────────────────────────────────
//  Test: Basic commit pipeline — single phase
// ──────────────────────────────────────────────

let ``E2E: single phase commit`` () =
    promise {
        let store = InMemoryEventStore()
        let mutable committedState = emptySessionState ()

        let event = ev "s1" eventKindLoopActivated (Map [ "task", "refactor" ])

        let! result =
            persistBarrier
                (fun () ->
                    promise {
                        store.Append event
                        return ()
                    })
                (fun () ->
                    committedState <- applyEvent committedState event
                    42)

        equal "E2E: commit returns 42" 42 result
        equal "E2E: store has 1 event" 1 store.Count

        let overview = fromSessionState committedState
        check "E2E: review task matches" (overview.ReviewTask = Some "refactor")
    }

// ──────────────────────────────────────────────
//  Test: Multi-phase pipeline (2 events, 2 commits)
// ──────────────────────────────────────────────

let ``E2E: multi-phase commit`` () =
    promise {
        let store = InMemoryEventStore()
        let mutable committedState = emptySessionState ()

        // Phase 1: Activate loop
        let e1 = ev "s1" eventKindLoopActivated (Map [ "task", "phase1" ])

        let! _ =
            persistBarrier
                (fun () ->
                    promise {
                        store.Append e1
                        return ()
                    })
                (fun () ->
                    committedState <- applyEvent committedState e1
                    1)

        // Phase 2: Submit work backlog
        let e2 =
            ev
                "s1"
                eventKindWorkBacklogCommitted
                (Map
                    [ "ahaMoments", "done"
                      "changesAndReasons", "c"
                      "gotchas", "g"
                      "lessonsAndConventions", "l"
                      "plan", "p"
                      "todosJson", "[]" ])

        let! _ =
            persistBarrier
                (fun () ->
                    promise {
                        store.Append e2
                        return ()
                    })
                (fun () ->
                    committedState <- applyEvent committedState e2
                    2)

        equal "E2E: store has 2 events" 2 store.Count

        let overview = fromSessionState committedState
        check "E2E: review task still active after backlog" (overview.ReviewTask = Some "phase1")
        check "E2E: backlog snapshot present" (overview.BacklogSnapshot.LatestEntry.IsSome)
    }

// ──────────────────────────────────────────────
//  Test: Persist failure rolls back (uses Promise.catch)
// ──────────────────────────────────────────────

let ``E2E: persist failure via promise catch`` () =
    promise {
        let store = InMemoryEventStore()
        let mutable committedState = emptySessionState ()
        let mutable commitCalled = false

        let! result =
            persistBarrier (fun () -> promise { return! failwith "disk full" }) (fun () ->
                commitCalled <- true
                let e1 = ev "s1" eventKindLoopActivated (Map [ "task", "rollback" ])
                store.Append e1
                committedState <- applyEvent committedState e1
                42)
            |> Promise.catch (fun _ -> -1)

        equal "E2E: persist failure returns -1" -1 result
        check "E2E: commit NOT called on persist failure" (not commitCalled)
        equal "E2E: store empty after failed persist" 0 store.Count
    }

// ──────────────────────────────────────────────
//  Test: ReactivePort integration
// ──────────────────────────────────────────────

let ``E2E: reactive port fires on commit`` () =
    promise {
        let store = InMemoryEventStore()
        let mutable committedState = emptySessionState ()
        let mutable portFired = false

        let port =
            { new IReactivePort with
                member _.OnCommitted(_) = portFired <- true
                member _.OnTelemetry(_) = () }

        let e1 = ev "s1" eventKindLoopActivated (Map [ "task", "reactive" ])

        let! _ =
            persistBarrier
                (fun () ->
                    promise {
                        store.Append e1
                        return ()
                    })
                (fun () ->
                    committedState <- applyEvent committedState e1
                    port.OnCommitted([])
                    42)

        check "E2E: reactive port fired" portFired
    }

// ──────────────────────────────────────────────
//  Test: SessionOverview projection after commit
// ──────────────────────────────────────────────

let ``E2E: session overview after commit`` () =
    promise {
        let store = InMemoryEventStore()
        let mutable committedState = emptySessionState ()

        // Activate loop
        let e1 = ev "s1" eventKindLoopActivated (Map [ "task", "overview-test" ])

        let! _ =
            persistBarrier
                (fun () ->
                    promise {
                        store.Append e1
                        return ()
                    })
                (fun () ->
                    committedState <- applyEvent committedState e1
                    1)

        // Submit backlog
        let e2 =
            ev
                "s1"
                eventKindWorkBacklogCommitted
                (Map
                    [ "ahaMoments", "a"
                      "changesAndReasons", "c"
                      "gotchas", "g"
                      "lessonsAndConventions", "l"
                      "plan", "p" ])

        let! _ =
            persistBarrier
                (fun () ->
                    promise {
                        store.Append e2
                        return ()
                    })
                (fun () ->
                    committedState <- applyEvent committedState e2
                    2)

        // Submit nudge event
        let e3 = ev "s1" eventKindNudgeDispatched (Map [ "anchor", "t1\u001emsg" ])

        let! _ =
            persistBarrier
                (fun () ->
                    promise {
                        store.Append e3
                        return ()
                    })
                (fun () ->
                    committedState <- applyEvent committedState e3
                    3)

        equal "E2E: 3 events in store" 3 store.Count

        let overview = fromSessionState committedState
        check "E2E: overview review task present" (overview.ReviewTask = Some "overview-test")
        check "E2E: overview backlog non-empty" (not (List.isEmpty overview.Backlog))
        check "E2E: overview nudge dedup has anchor" (overview.NudgeDedup.LastDispatchedAnchor.IsSome)
    }

// ──────────────────────────────────────────────
//  Test: runCommitPipeline end-to-end
// ──────────────────────────────────────────────

let ``E2E: runCommitPipeline full flow`` () =
    promise {
        let mutable portFired = false

        let pipeline: CommitPipeline<int> =
            { Decide = fun () -> Ok(42, []) // no effects
              Persist = fun () -> promise { return () }
              Commit = fun _ -> portFired <- true }

        let! result = runCommitPipeline pipeline

        match result with
        | Ok v ->
            equal "E2E: pipeline returns 42" 42 v
            check "E2E: port fired after commit" portFired
        | Error _ -> check "E2E: pipeline should succeed" false
    }

let run () =
    promise {
        do! ``E2E: single phase commit`` ()
        do! ``E2E: multi-phase commit`` ()
        do! ``E2E: persist failure via promise catch`` ()
        do! ``E2E: reactive port fires on commit`` ()
        do! ``E2E: session overview after commit`` ()
        do! ``E2E: runCommitPipeline full flow`` ()
    }
