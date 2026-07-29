namespace Wanxiangshu.Next.Journal

open System
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome

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

/// The single durable journal for one runtime.
///
/// PERSIST-008: `Snapshot` is integrated state, never a replay. Appending folds
/// exactly one envelope into the projection it already holds.
type AgentJournal internal (writer: JournalWriter, initialProjection: ProjectionSet) =
    let gate = obj ()
    let mutable projection = initialProjection
    let mutable rejected: (EventId * FoldRejection) option = None

    member _.Writer = writer
    member _.RuntimeId = writer.RuntimeId

    /// PERSIST-003: a poisoned writer or a rejected fact both mean this journal
    /// may no longer be appended to.
    member _.IsPoisoned = lock gate (fun () -> writer.IsPoisoned || Option.isSome rejected)

    member _.Snapshot: ProjectionSet = lock gate (fun () -> projection)

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
    member _.AppendAgent
        (stream: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: AgentFact)
        : Result<ProjectionSet, JournalAppendFailure> =
        lock gate (fun () ->
            match rejected with
            | Some(eventId, rejection) -> Error(FactRejected(eventId, rejection))
            | None ->
                match writer.Append stream providerRun (Fact.Agent fact) with
                | CommitUnknown(eventId, failure) -> Error(WriteUnknown(eventId, failure))
                | Committed envelope ->
                    match Fold.foldEnvelope projection envelope with
                    | Ok updated ->
                        projection <- updated
                        Ok updated
                    | Error rejection ->
                        rejected <- Some(envelope.EventId, rejection)
                        Error(FactRejected(envelope.EventId, rejection)))

    interface IDisposable with
        member _.Dispose() = (writer :> IDisposable).Dispose()

    interface IAsyncDisposable with
        member _.DisposeAsync() =
            (writer :> IAsyncDisposable).DisposeAsync()

module AgentJournal =

    /// PERSIST-004: a journal that cannot be folded stops startup. Recovering
    /// "as much as folded cleanly" would build the runtime on a prefix no writer
    /// ever produced.
    let createFromBoot
        (directory: string)
        (runtimeId: RuntimeId)
        (processId: int)
        (startedAt: DateTimeOffset)
        (boot: BootSnapshot)
        : Result<AgentJournal, FoldRejection> =
        Fold.apply Fold.empty boot.Envelopes
        |> Result.bind (fun replayed ->
            let writer, initEnvelope =
                JournalWriter.create directory runtimeId processId startedAt

            Fold.foldEnvelope replayed initEnvelope
            |> Result.map (fun withRuntime -> new AgentJournal(writer, withRuntime)))

    let create
        (directory: string)
        (runtimeId: RuntimeId)
        (processId: int)
        (startedAt: DateTimeOffset)
        : Result<AgentJournal, FoldRejection> =
        let writer, initEnvelope =
            JournalWriter.create directory runtimeId processId startedAt

        Fold.foldEnvelope Fold.empty initEnvelope
        |> Result.map (fun projection -> new AgentJournal(writer, projection))

    let appendAgent
        (stream: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: AgentFact)
        (journal: AgentJournal)
        : Result<ProjectionSet, JournalAppendFailure> =
        journal.AppendAgent stream providerRun fact

    let snapshot (journal: AgentJournal) : ProjectionSet = journal.Snapshot

    let runtimeId (journal: AgentJournal) : RuntimeId = journal.RuntimeId

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
