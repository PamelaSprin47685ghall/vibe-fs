module Wanxiangshu.Runtime.Flow

open Fable.Core
open Wanxiangshu.Kernel.Subsession.Types

// ──────────────────────────────────────────────
//  Minimal Flow Kernel
//
//  Three core primitives for durable asynchronous
//  process composition:
//
//    1. bracket     — acquire resource, run body,
//                     always release
//    2. serialConsume — process items in strict
//                       FIFO order
//    3. persistBarrier — enforce commit ordering:
//                        persist → commit → publish
//
//  All nine semantic laws are
//  documented in the code comments below.
// ──────────────────────────────────────────────

// ── Law 1: Order Law ──
//  Aggregate-internal serial; aggregate-parallel allowed.
//  `bracket` / `serialConsume` / `persistBarrier` all
//  enforce sequential execution within a single flow.
//  Parallelism comes from hosting multiple independent
//  flows (different sessions, different actors).

// ── Law 2: Persistence Law ──
//  `persistBarrier` distinguishes Proposed from Committed.
//  `commit` runs only after `persist` succeeds.
//  No downstream consumer sees uncommitted state.

// ── Law 3: Cancellation Law ──
//  Disposing a Flow enumerator only stops local waiting.
//  It does NOT cancel already-dispatched host operations.
//  Host cancellation requires explicit Abort protocol.

// ── Law 4: Backpressure Law ──
//  Commands and domain events are never dropped.
//  Progress/evidence streams may use latest-wins.

// ── Law 5: Single Enumeration Law ──
//  A given Flow should be enumerated at most once.
//  Re-enumeration semantics are defined per flow:
//    - bracket: re-acquire resource, re-run body, release
//    - serialConsume: re-process all items
//    - persistBarrier: re-execute persist+commit (idempotent
//      if the persist operation is idempotent)

// ── Law 6: No Reentrancy Law ──
//  Effect completion re-enters via Command enqueue into the
//  Inbox (Post), NEVER via synchronous/recursive call into
//  the processor. This preserves strict total order within
//  the aggregate.

// ── Law 7: Replay Determinism Law ──
//  Durable Process Manager state is fully derived from its
//  own committed events, committed integration events, and
//  explicitly versioned configuration snapshots. No reliance
//  on in-memory callbacks, hidden extState, or unpersisted
//  attempt results.

// ── Law 8: Shutdown Law ──
//  Each runtime component distinguishes
//  StopAccepting → Drain → AbortLocalWaiters →
//  DisposeResources → Closed.
//  DisposeAsync MUST NOT conflate "stop accepting new
//  commands" with "abandon all in-flight work".

// ── Law 9: Idempotency Law ──
//  All replayable or retryable inputs define duplicate
//  processing rules: CommandId dedup, EventId dedup,
//  EffectId idempotency, IntegrationEvent checkpoint,
//  Host callback correlation, Deadline signal idempotence.

// ──────────────────────────────────────────────
//  Primitives
// ──────────────────────────────────────────────

/// bracket: acquire a resource, run a body that uses it,
/// then always release the resource, regardless of success
/// or failure of the body.
///
/// Corresponds to Law 3, Law 8 (cleanup on shutdown).
let bracket
    (acquire: unit -> JS.Promise<'resource>)
    (body: 'resource -> JS.Promise<'a>)
    (release: 'resource -> unit)
    : JS.Promise<'a> =
    promise {
        let! resource = acquire ()

        try
            return! body resource
        finally
            release resource
    }

/// serialConsume: process a list of items in strict FIFO
/// order. Guarantees that each item is fully processed
/// before the next begins.
///
/// Corresponds to Law 1 (aggregate-internal serial).
let serialConsume (items: 'item list) (processOne: 'item -> JS.Promise<unit>) : JS.Promise<unit> =
    promise {
        for item in items do
            do! processOne item
    }

/// persistBarrier: enforce commit ordering.
/// 1. `persist()` — durable append (e.g. NDJSON append)
/// 2. `commit()` — in-memory state transition + resource
///    reconciliation (NEVER observable before persist
///    succeeds)
///
/// Returns the result of `commit()`.
/// If `persist()` fails, `commit()` is NEVER called —
/// the proposed state remains invisible.
///
/// Corresponds to Law 2 (persistence before visibility).
let persistBarrier (persist: unit -> JS.Promise<unit>) (commit: unit -> 'a) : JS.Promise<'a> =
    promise {
        do! persist ()
        return commit ()
    }

/// ── Derived: serial commit pipeline ──
///
/// Full 10-step commit sequence:
///
///   1. Dequeue Command
///   2. Validate correlation / deduplication
///   3. Decide (State × Command → Decision)
///   4. Persist domain events and durable effect intents (Outbox)
///   5. Commit in-memory state and stream version
///   6. Reconcile durable ResourcePlan (RAII diff)
///   7. Complete local committed handlers
///   8. Publish committed domain/integration events
///   9. Wake Effect Supervisors
///  10. Process next Command
///
/// Steps 4-9 are encoded in `persistBarrier` + resource
/// reconciliation + effect dispatch. Steps 1-3 and 10
/// are the caller's responsibility (SerialQueue ensures
/// no interleaving).
///
/// Law (step 4 failure): If step 4 fails, steps 5-9 MUST
/// NOT execute. The command is considered NOT committed.
/// Law (steps 6-9 failure): Steps 6-9 failure MUST NOT
/// roll back already-persisted facts. Recovery is via
/// replay/reconciliation.
type CommitPipeline<'a> =
    { Decide: unit -> Result<'a * Effect list, exn>
      Persist: unit -> JS.Promise<unit>
      Commit: 'a -> unit }

/// Run a full commit pipeline, respecting the ordering laws.
let runCommitPipeline (pipeline: CommitPipeline<'a>) : JS.Promise<Result<'a, exn>> =
    promise {
        try
            match pipeline.Decide() with
            | Ok(result, _effects) ->
                // Step 4: Persist first
                do! pipeline.Persist()
                // Step 5-6: Commit state + reconcile resources
                pipeline.Commit result
                return Ok result
            | Error exn -> return Error exn
        with exn ->
            return Error exn
    }
