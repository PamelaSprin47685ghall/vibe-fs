namespace Wanxiangshu.Journal

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Outcome
open Wanxiangshu.Domain.MagicTodoFacts

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
    /// The line was written but the fold refuses it.
    ///
    /// This is not a data problem — it means a writer produced a fact the domain
    /// forbids (FALLBACK-007's modulo-4 check, REVIEW-003's causal proof). The
    /// journal is now unfoldable, so it is poisoned deliberately rather than
    /// left to fail on the next boot.
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
    /// The two cases read differently on purpose. `WriteUnknown` means the runtime
    /// must reconcile (PERSIST-002/003); `FactRejected` means a writer produced a
    /// fact the domain forbids and the journal is now poisoned.
    let describe (failure: JournalAppendFailure) : string =
        match failure with
        | WriteUnknown(eventId, WriteFailed reason) ->
            sprintf "append outcome unknown for %s: write failed: %s" (EventId.value eventId) reason
        | WriteUnknown(eventId, FlushFailed reason) ->
            sprintf "append outcome unknown for %s: flush failed: %s" (EventId.value eventId) reason
        | FactRejected(eventId, rejection) ->
            sprintf
                "journal poisoned at %s: fact '%s' rejected: %s"
                (EventId.value eventId)
                rejection.Fact
                rejection.Reason

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
    // DSL-MUTABLE: resource — in-memory projection after last committed fold
    let mutable projection = initialProjection
    // DSL-MUTABLE: resource — fold rejection poison latch
    let mutable rejected: (EventId * FoldRejection) option = None
    // DSL-MUTABLE: resource — journal revision cursor
    let mutable revision = JournalRevision.create writer.LastCommittedLocalSeq
    // DSL-MUTABLE: resource — last journal change notification payload
    let mutable lastChange: JournalChange option = None
    let waiters = ResizeArray<JournalRevision * TaskCompletionSource<JournalChange>>()

    member _.Writer = writer
    member _.RuntimeId = writer.RuntimeId
    member _.WriteBlob(content: string) : Result<BlobWriteReceipt, string> = writer.BlobWriter.Write content

    /// PERSIST-003: a poisoned writer or a rejected fact both mean this journal
    /// may no longer be appended to.
    member _.IsPoisoned = lock gate (fun () -> writer.IsPoisoned || Option.isSome rejected)

    member _.Snapshot: ProjectionSet = lock gate (fun () -> projection)

    /// Current revision under the same gate as Snapshot (Join handshake).
    member _.Revision: JournalRevision = lock gate (fun () -> revision)

    /// Projection and revision read under one lock so Join cannot observe a split.
    member _.SnapshotWithRevision: ProjectionSet * JournalRevision =
        lock gate (fun () -> projection, revision)

    /// FALLBACK-004: apply the success transition derived from a completed Host
    /// snapshot. This is an in-memory projection update, not a journal fact: only
    /// an existing session and its existing Fallback option may be changed.
    member _.RecordDerivedFallbackSuccess(sessionId: SessionId) : unit =
        lock gate (fun () ->
            match AgentProjection.tryFind sessionId projection.AgentProjections with
            | None -> ()
            | Some session ->
                match session.Fallback with
                | None -> ()
                | Some fallback ->
                    let updatedSession =
                        { session with
                            Fallback = Some(FallbackProjection.recordSuccess fallback) }

                    projection <-
                        { projection with
                            AgentProjections =
                                { projection.AgentProjections with
                                    Sessions = Map.add sessionId updatedSession projection.AgentProjections.Sessions } })

    /// Wait until a successful fold advances past `fromRevision`.
    ///
    /// Recheck under lock before registering: if already advanced and lastChange
    /// exists, complete immediately (no full history replay).
    member _.AwaitChangeFrom(fromRevision: JournalRevision) : Task<JournalChange> =
        lock gate (fun () ->
            if JournalRevision.isAfter revision fromRevision then
                match lastChange with
                | Some change -> Task.FromResult change
                | None ->
                    // Revision advanced without a process-local lastChange (boot
                    // only). Wait for the next successful fold rather than hang
                    // on a synthetic envelope.
                    let tcs = TaskCompletionSource<JournalChange>()
                    waiters.Add(fromRevision, tcs)
                    tcs.Task
            else
                let tcs = TaskCompletionSource<JournalChange>()
                waiters.Add(fromRevision, tcs)
                tcs.Task)

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
        : Result<ProjectionSet, JournalAppendFailure> =
        this.AppendEnvelope stream providerRun (Fact.Agent fact) |> Result.map fst

    /// Append a Magic Todo fact and return its durable envelope identity.
    ///
    /// `TodoWriteAccepted` must name the exact Prepared envelope; returning the
    /// receipt here prevents a caller from inventing or rediscovering that ref.
    member this.AppendMagicTodo
        (stream: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: MagicTodoFact)
        : Result<MagicTodoAppendReceipt, JournalAppendFailure> =
        fact
        |> MagicTodoFactCodec.encode
        |> Fact.MagicTodo
        |> this.AppendEnvelope stream providerRun
        |> Result.map (fun (updated, envelope) ->
            { EventId = envelope.EventId
              Projection = updated })

    /// GLORY-010: append one Manager lifecycle fact. Envelope `ProviderRun` is
    /// `None` — the payload carries its own run identities (FinalityRequested).
    member this.AppendManagerLifecycle
        (stream: StreamId)
        (fact: ManagerLifecycleFact)
        : Result<ProjectionSet, JournalAppendFailure> =
        this.AppendEnvelope stream None (Fact.ManagerLifecycle fact) |> Result.map fst

    member private _.AppendEnvelope
        (stream: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: Fact)
        : Result<ProjectionSet * Envelope, JournalAppendFailure> =
        // DSL-MUTABLE: buffer — waiters to notify after lock release
        let mutable notify: (TaskCompletionSource<JournalChange> * JournalChange) list = []

        let result =
            lock gate (fun () ->
                match rejected with
                | Some(eventId, rejection) -> Error(FactRejected(eventId, rejection))
                | None ->
                    match writer.Append stream providerRun fact with
                    | CommitUnknown(eventId, failure) -> Error(WriteUnknown(eventId, failure))
                    | Committed envelope ->
                        match Fold.foldEnvelope projection envelope with
                        | Ok updated ->
                            projection <- updated
                            revision <- JournalRevision.create (LocalSeq.value envelope.LocalSeq)

                            let change =
                                { Revision = revision
                                  Envelope = envelope }

                            lastChange <- Some change

                            let ready = ResizeArray<TaskCompletionSource<JournalChange> * JournalChange>()
                            let kept = ResizeArray<JournalRevision * TaskCompletionSource<JournalChange>>()

                            for subRev, tcs in waiters do
                                if JournalRevision.isAfter revision subRev then
                                    ready.Add(tcs, change)
                                else
                                    kept.Add(subRev, tcs)

                            waiters.Clear()

                            for item in kept do
                                waiters.Add item

                            notify <- List.ofSeq ready
                            Ok(updated, envelope)
                        | Error rejection ->
                            rejected <- Some(envelope.EventId, rejection)
                            Error(FactRejected(envelope.EventId, rejection)))

        // Fire outside the gate: listeners must not run under the journal lock.
        for tcs, change in notify do
            AsyncSupport.trySetResult tcs change |> ignore

        result

    interface IDisposable with
        member _.Dispose() = writer.Release()

    interface IAsyncDisposable with
        member _.DisposeAsync() = writer.ReleaseAsync()

module AgentJournal =

    /// EventStore-backed journal for empty init-only tests. Caller builds the
    /// writer via the EventStore journal writer factory (keeps this module free
    /// of store write tokens for the unified-store dual-write gate).
    let createFromEventStore (writer: IJournalWriter) (initEnvelope: Envelope) : Result<AgentJournal, FoldRejection> =
        Fold.foldEnvelope Fold.empty initEnvelope
        |> Result.map (fun projection -> new AgentJournal(writer, projection))

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
        : Result<ProjectionSet, JournalAppendFailure> =
        journal.AppendAgent stream providerRun fact

    let appendMagicTodo
        (stream: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: MagicTodoFact)
        (journal: AgentJournal)
        : Result<MagicTodoAppendReceipt, JournalAppendFailure> =
        journal.AppendMagicTodo stream providerRun fact

    /// GLORY-010: append one Manager lifecycle fact.
    let appendManagerLifecycle
        (stream: StreamId)
        (fact: ManagerLifecycleFact)
        (journal: AgentJournal)
        : Result<ProjectionSet, JournalAppendFailure> =
        journal.AppendManagerLifecycle stream fact

    let snapshot (journal: AgentJournal) : ProjectionSet = journal.Snapshot

    let revision (journal: AgentJournal) : JournalRevision = journal.Revision

    let snapshotWithRevision (journal: AgentJournal) : ProjectionSet * JournalRevision = journal.SnapshotWithRevision

    let awaitChangeFrom (fromRevision: JournalRevision) (journal: AgentJournal) : Task<JournalChange> =
        journal.AwaitChangeFrom fromRevision

    /// FALLBACK-004: derive success from a valid completed turn without appending
    /// a fact. Missing journals, sessions, and fallback projections are all no-op.
    let recordDerivedFallbackSuccess (journal: AgentJournal option) (sessionId: SessionId) : unit =
        journal
        |> Option.iter (fun value -> value.RecordDerivedFallbackSuccess sessionId)

    let handleProjection (journal: AgentJournal) (sessionId: SessionId) : AgentLinkageProjection =
        AgentProjection.tryFind sessionId (snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.Handles)
        |> Option.defaultValue HandleProjection.empty

    let runtimeId (journal: AgentJournal) : RuntimeId = journal.RuntimeId

    let writeBlob (content: string) (journal: AgentJournal) : Result<BlobWriteReceipt, string> =
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
            |> Option.map (fun requirements -> requirements.HumanPromptInputs)
            |> Option.defaultValue []
