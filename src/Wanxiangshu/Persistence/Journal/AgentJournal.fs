namespace Wanxiangshu.Persistence.Journal

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Participant.Provider.Attempt.Fallback

/// One successful fold after append. Wake payload for revision subscribers.
/// No retained history: only the latest change is kept for the recheck path.
type JournalChange =
    { Revision: JournalRevision
      Envelope: Envelope }

type MagicTodoAppendReceipt =
    { EventId: EventId
      Projection: ProjectionSet }

type JournalAppendFailure =
    /// PERSIST-002 / PERSIST-003: the write did not complete cleanly. Whether it
    /// landed is unknown, so the runtime must fail closed and reconcile.
    | WriteUnknown of EventId * JournalFailure
    /// The caller's event never reached the physical append boundary. This is
    /// known-not-committed and must not masquerade as storage uncertainty.
    | WriterUnavailable of EventId * JournalUnavailable
    /// The fact and its ProjectionCutTail reset are both durable. The writer may
    /// be reusable after restart, but the current process is no longer trusted:
    /// live append admission trips the process-level invariant fuse immediately.
    | FactRejected of EventId * FoldRejection

module JournalAppendFailure =

    /// Diagnostic rendering (HOST-007). The ONE place a failed append becomes a
    /// string.
    ///
    /// Nine call sites wrote `sprintf "%A" failure.Failure` — a field that does not
    /// exist on this union. Each was independently wrong in the same way, which is
    /// what a missing function looks like. `%A` is also reflection-based: under Fable
    /// it renders whatever the emitted shape happens to be, so the operator-facing
    /// text would drift with the compiler rather than with the domain.
    ///
    /// The two cases read differently on purpose. `WriteUnknown` is a physical
    /// uncertainty; `FactRejected` is a durable semantic cut and is fatal to the
    /// current process at the append boundary.
    let describe (failure: JournalAppendFailure) : string =
        match failure with
        | WriteUnknown(eventId, WriteFailed reason) ->
            sprintf "append outcome unknown for %s: write failed: %s" (EventId.value eventId) reason
        | WriteUnknown(eventId, FlushFailed reason) ->
            sprintf "append outcome unknown for %s: flush failed: %s" (EventId.value eventId) reason
        | WriterUnavailable(eventId, WriterPoisoned firstFailure) ->
            sprintf
                "append not attempted for %s: writer poisoned by prior failure: %s"
                (EventId.value eventId)
                firstFailure
        | WriterUnavailable(eventId, WriterClosing) ->
            sprintf "append not attempted for %s: writer is closing" (EventId.value eventId)
        | WriterUnavailable(eventId, WriterDisposed) ->
            sprintf "append not attempted for %s: writer is disposed" (EventId.value eventId)
        | FactRejected(eventId, rejection) ->
            sprintf
                "journal semantic cut at %s: fact '%s' rejected: %s"
                (EventId.value eventId)
                rejection.Fact
                rejection.Reason

module private AgentJournalInternals =

    let registerWaiter
        (fromRevision: JournalRevision)
        (waiters: ResizeArray<JournalRevision * TaskCompletionSource<JournalChange option>>)
        =
        let tcs =
            TaskCompletionSource<JournalChange option>(TaskCreationOptions.RunContinuationsAsynchronously)

        waiters.Add(fromRevision, tcs)
        tcs

    let removeWaiter
        (fromRevision: JournalRevision)
        (tcs: TaskCompletionSource<JournalChange option>)
        (waiters: ResizeArray<JournalRevision * TaskCompletionSource<JournalChange option>>)
        =
        waiters.Remove((fromRevision, tcs)) |> ignore

    let partitionWaiters
        (revision: JournalRevision)
        (change: JournalChange)
        (waiters: ResizeArray<JournalRevision * TaskCompletionSource<JournalChange option>>)
        =
        let ready, kept =
            waiters
            |> Seq.toList
            |> List.partition (fun (subRev, _) -> JournalRevision.isAfter revision subRev)

        ready |> List.map (fun (_, tcs) -> tcs, change), kept

    let notifyWaiters (notify: (TaskCompletionSource<JournalChange option> * JournalChange) list) =
        for tcs, change in notify do
            AsyncSupport.trySetResult tcs (Some change) |> ignore

open AgentJournalInternals

/// The single durable journal for one runtime.
///
/// PERSIST-008: `Snapshot` is integrated state, never a replay. Appending folds
/// exactly one envelope into the projection it already holds.
///
/// Revision subscription: append+fsync → fold → revision advances → wake waiters.
/// Correctness does not require every wake to be delivered: Join uses
/// check → subscribe → recheck → await (see `AwaitChangeFrom`).
type AgentJournal internal (writer: IJournalWriter, initialProjection: ProjectionSet) =
    let gate = obj ()
    // DSL-MUTABLE: resource — journal revision cursor
    let mutable revision = JournalRevision.create writer.LastCommittedLocalSeq
    // DSL-MUTABLE: resource — last journal change notification payload
    let mutable lastChange: JournalChange option = None

    let waiters =
        ResizeArray<JournalRevision * TaskCompletionSource<JournalChange option>>()

    member _.Writer = writer
    member _.RuntimeId = writer.RuntimeId
    member _.WriteBlob(content: string) : Task<Result<BlobWriteReceipt, string>> = writer.BlobWriter.Write content

    /// Physical write uncertainty may poison the writer. A semantic cut does not
    /// poison persisted bytes, but it separately trips FatalProcess for this process.
    member _.IsPoisoned = lock gate (fun () -> writer.IsPoisoned)

    member private _.CanonicalProjection: ProjectionSet =
        match writer.TryCurrent "Journal" with
        | Some current -> unbox<ProjectionSet> current
        | None -> initialProjection

    member this.Snapshot: ProjectionSet = lock gate (fun () -> this.CanonicalProjection)

    /// Current revision under the same gate as Snapshot (Join handshake).
    member _.Revision: JournalRevision = lock gate (fun () -> revision)

    /// Projection and revision read under one lock so Join cannot observe a split.
    member this.SnapshotWithRevision: ProjectionSet * JournalRevision =
        lock gate (fun () -> this.Snapshot, revision)



    member _.AwaitChangeFromOrCancel
        (fromRevision: JournalRevision, cancellation: CancellationToken)
        : Task<JournalChange option> =
        task {
            let pending, registered =
                lock gate (fun () ->
                    match JournalRevision.isAfter revision fromRevision, lastChange with
                    | true, Some change -> Task.FromResult(Some change), None
                    | _ ->
                        let waiter = registerWaiter fromRevision waiters
                        waiter.Task, Some waiter)

            match registered with
            | None -> return! pending
            | Some waiter ->
                use _registration =
                    cancellation.Register(fun () ->
                        lock gate (fun () -> removeWaiter fromRevision waiter waiters)
                        AsyncSupport.trySetResult waiter None |> ignore)

                return! pending
        }

    /// Wait until a successful fold advances past `fromRevision`.
    member this.AwaitChangeFrom(fromRevision: JournalRevision) : Task<JournalChange> =
        task {
            let! change = this.AwaitChangeFromOrCancel(fromRevision, CancellationToken.None)

            match change with
            | Some committed -> return committed
            | None -> return failwith "unreachable: uncancelled journal wait"
        }

    /// Append one fact and fold it.
    ///
    /// Deduplication is deliberately absent here. FALLBACK-003 names the
    /// FallbackController as the single place that decides whether a failed
    /// attempt advances the cursor, and REVIEW-004 gives review dedupe to the
    /// projection. A second dedupe at the append boundary would be the same
    /// knowledge in a second place — and the previous version proved the cost: it
    /// re-implemented the dedupe key as `sprintf "%s|%s|%s"`, so the journal and
    /// the fold each had their own idea of what identified an attempt.
    ///
    /// Replaying a duplicate is safe: the fold returns the projection unchanged.
    member this.AppendAgent
        (stream: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: AgentFact)
        : Task<Result<ProjectionSet, JournalAppendFailure>> =
        task {
            match! this.AppendEnvelope stream providerRun (Fact.Agent fact) with
            | Ok(updated, _) -> return Ok updated
            | Error err -> return Error err
        }

    /// Append a Magic Todo fact and return its durable envelope identity.
    ///
    /// `TodoWriteAccepted` must name the exact Prepared envelope; returning the
    /// receipt here prevents a caller from inventing or rediscovering that ref.
    member this.AppendMagicTodo
        (stream: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: MagicTodoFact)
        : Task<Result<MagicTodoAppendReceipt, JournalAppendFailure>> =
        task {
            match! this.AppendEnvelope stream providerRun (Fact.MagicTodo fact) with
            | Ok(updated, envelope) ->
                return
                    Ok
                        { EventId = envelope.EventId
                          Projection = updated }
            | Error err -> return Error err
        }

    /// GLORY-010: append one Manager lifecycle fact. Envelope `ProviderRun` is
    /// `None` — the payload carries its own run identities (FinalityRequested).
    member this.AppendManagerLifecycle
        (stream: StreamId)
        (fact: ManagerLifecycleFact)
        : Task<Result<ProjectionSet, JournalAppendFailure>> =
        task {
            match! this.AppendEnvelope stream None (Fact.ManagerLifecycle fact) with
            | Ok(updated, _) -> return Ok updated
            | Error err -> return Error err
        }

    member private this.PublishCommitted(envelope: Envelope) =
        lock gate (fun () ->
            // The EventStore append already validated and committed the
            // canonical Integrator transition. AgentJournal only observes
            // Current and publishes its process-local revision wake.
            let updated = this.Snapshot
            revision <- JournalRevision.create (LocalSeq.value envelope.LocalSeq)

            let change =
                { Revision = revision
                  Envelope = envelope }

            lastChange <- Some change
            let ready, kept = partitionWaiters revision change waiters
            waiters.Clear()

            for item in kept do
                waiters.Add item

            (updated, envelope), ready)

    member private this.AppendAndPublish
        (stream: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: Fact)
        : Task<
              Result<
                  (ProjectionSet * Envelope) * (TaskCompletionSource<JournalChange option> * JournalChange) list,
                  JournalAppendFailure
               >
           >
        =
        task {
            let! appended = writer.Append stream providerRun fact

            match appended with
            | CommitUnknown(eventId, failure) -> return Error(WriteUnknown(eventId, failure))
            | NotAttempted(eventId, unavailable) -> return Error(WriterUnavailable(eventId, unavailable))
            | Rejected(eventId, reason) ->
                let failure =
                    FactRejected(
                        eventId,
                        { Fact = "semantic-cut"
                          Reason = reason }
                    )

                // A durable cut-tail makes the history recoverable; it does
                // NOT make the current in-memory runtime trustworthy. The
                // exact bug class that produced the cut may already have
                // mutated process-local ownership, caches or pending tasks.
                // Kill now rather than letting a caller downgrade this to a
                // tool consequence and continue emitting effects.
                FatalProcess.trip "journal-semantic-cut" (JournalAppendFailure.describe failure)
                return Error failure
            | Committed envelope -> return Ok(this.PublishCommitted envelope)
        }

    member private this.AppendEnvelope
        (stream: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: Fact)
        : Task<Result<ProjectionSet * Envelope, JournalAppendFailure>> =
        task {
            let! result = this.AppendAndPublish stream providerRun fact

            match result with
            | Error failure -> return Error failure
            | Ok((updated, envelope), notify) ->
                notifyWaiters notify
                return Ok(updated, envelope)
        }

    interface IDisposable with
        member _.Dispose() = writer.Release()

    interface IAsyncDisposable with
        member _.DisposeAsync() = writer.ReleaseAsync()

module AgentJournal =

    /// Attach a writer to a projection already folded at EventStore boot
    /// (`resumeOrCreate`). Does not re-fold; does not open a store.
    let createFromProjection
        (writer: IJournalWriter)
        (projection: ProjectionSet)
        : Result<AgentJournal, FoldRejection> =
        Ok(new AgentJournal(writer, projection))

    let appendAgent
        (stream: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: AgentFact)
        (journal: AgentJournal)
        : Task<Result<ProjectionSet, JournalAppendFailure>> =
        journal.AppendAgent stream providerRun fact

    let appendMagicTodo
        (stream: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: MagicTodoFact)
        (journal: AgentJournal)
        : Task<Result<MagicTodoAppendReceipt, JournalAppendFailure>> =
        journal.AppendMagicTodo stream providerRun fact

    /// GLORY-010: append one Manager lifecycle fact.
    let appendManagerLifecycle
        (stream: StreamId)
        (fact: ManagerLifecycleFact)
        (journal: AgentJournal)
        : Task<Result<ProjectionSet, JournalAppendFailure>> =
        journal.AppendManagerLifecycle stream fact

    let snapshot (journal: AgentJournal) : ProjectionSet = journal.Snapshot

    let revision (journal: AgentJournal) : JournalRevision = journal.Revision

    let snapshotWithRevision (journal: AgentJournal) : ProjectionSet * JournalRevision = journal.SnapshotWithRevision

    let awaitChangeFrom (fromRevision: JournalRevision) (journal: AgentJournal) : Task<JournalChange> =
        journal.AwaitChangeFrom fromRevision

    let awaitChangeFromOrCancel
        (fromRevision: JournalRevision)
        (cancellation: CancellationToken)
        (journal: AgentJournal)
        : Task<JournalChange option> =
        journal.AwaitChangeFromOrCancel(fromRevision, cancellation)



    let handleProjection (journal: AgentJournal) (sessionId: SessionId) : AgentLinkageProjection =
        AgentProjection.tryFind sessionId (snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.Handles)
        |> Option.defaultValue HandleProjection.empty

    let runtimeId (journal: AgentJournal) : RuntimeId = journal.RuntimeId

    let writeBlob (content: string) (journal: AgentJournal) : Task<Result<BlobWriteReceipt, string>> =
        journal.WriteBlob content

    let isPoisoned (journal: AgentJournal) : bool = journal.IsPoisoned

    /// REVIEW-007: which human prompts in this session still await a confirmed
    /// review.
    ///
    /// Keyed directly by session (PERSIST-008). The previous version walked a
    /// parent chain with `Map.tryPick` to find a "review requirement scope",
    /// scanning every session at each step. That is gone because the reason for
    /// it is gone: requirements are created by the fold on the session that
    /// received the HumanRoot, and cleared by `ConfirmedReviewWitness` on the
    /// Manager session, so no ownership has to be rediscovered by search.
    let pendingReviewRequirements (journal: AgentJournal option) (sessionId: SessionId) : ReviewRequirementInput list =
        match journal with
        | None -> []
        | Some value ->
            AgentProjection.tryFind sessionId (snapshot value).AgentProjections
            |> Option.bind (fun session -> session.ReviewRequirements)
            |> Option.map ReviewRequirementProjection.inputs
            |> Option.defaultValue []
