namespace Wanxiangshu.Journal

open System
open System.Collections.Generic
open System.Text
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Infrastructure.Persist
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Outcome

/// Store-backed blob writer for EventStore journals.
///
/// BlobRef mapping (documented contract for AgentJournal.WriteBlob callers):
/// - Scheme prefix remains `blobs/<handle>` so existing readers that strip
///   `blobs/` keep working.
/// - Under EventStore, `<handle>` is the Git blob OID hex returned by
///   `IGitRawStore.WriteBlob` (same text as Domain `PayloadRef` / Persist oid).
/// - `BlobDigest` remains SHA-256 of the UTF-8 content bytes (HostDigest), used
///   for integrity checks — it is not the Git OID.
/// - MUST NOT create a `blobs/` directory on disk; bodies live in the Git ODB.
type EventStoreBlobWriter private (raw: IGitRawStore) =
    member _.Write(content: string) : Task<Result<BlobWriteReceipt, string>> =
        task {
            try
                let digest = BlobDigest.create (HostDigest.sha256Hex content)
                let bytes = Encoding.UTF8.GetBytes content
                let! oid = raw.WriteBlob bytes
                let blobRef = BlobRef.create ("blobs/" + GitObjectId.value oid)

                return
                    Ok
                        { BlobRef = blobRef
                          BlobDigest = digest }
            with ex ->
                return Error(sprintf "event-store blob write failed: %s" ex.Message)
        }

    member _.Read(blobRef: BlobRef) : Task<Result<string, string>> =
        task {
            let relative = BlobRef.value blobRef
            let prefix = "blobs/"

            if not (relative.StartsWith(prefix, StringComparison.Ordinal)) then
                return Error(sprintf "invalid blob reference: %s" relative)
            else
                let oidHex = relative.Substring(prefix.Length)

                if String.IsNullOrWhiteSpace oidHex || oidHex.Contains "/" then
                    return Error(sprintf "invalid blob reference: %s" relative)
                else
                    match! raw.ReadObject(GitObjectId.create oidHex) with
                    | None -> return Error(sprintf "event-store blob missing: %s" oidHex)
                    | Some bytes ->
                        try
                            return Ok(Encoding.UTF8.GetString bytes)
                        with ex ->
                            return Error(sprintf "event-store blob read failed: %s" ex.Message)
        }

    interface IBlobWriter with
        member this.Write(content) = this.Write content
        member this.Read(blobRef) = this.Read blobRef

    static member Create(raw: IGitRawStore) : IBlobWriter =
        EventStoreBlobWriter(raw) :> IBlobWriter

type private UnavailableBlobWriter() =
    interface IBlobWriter with
        member _.Write(_content) =
            Task.FromResult(Error "EventStore journal writer has no IGitRawStore for blobs")

        member _.Read(_blobRef) =
            Task.FromResult(Error "EventStore journal writer has no IGitRawStore for blobs")

/// EventStore-backed journal writer (W1). Success path never writes NDJSON or
/// `blobs/<sha>` files — only `IEventStore` / `IGitRawStore`.
type EventStoreJournalWriter
    private
    (
        runtimeId: RuntimeId,
        blobWriter: IBlobWriter,
        store: IEventStore,
        initialSnapshot: StoreSnapshot,
        initialLastByStream: IDictionary<string, EventId>,
        initialCurrentSeq: int64
    ) =
    let gate = obj ()
    // DSL-MUTABLE: resource — next local sequence number under writer gate
    let mutable currentSeq = initialCurrentSeq
    // DSL-MUTABLE: resource — writer poison latch after write failure
    let mutable poisoned = false
    // DSL-MUTABLE: resource — writer dispose latch
    let mutable disposed = false
    // DSL-MUTABLE: resource — CAS base StoreSnapshot after last successful append
    let mutable baseSnapshot = initialSnapshot
    // DSL-MUTABLE: resource — last EventId per EventStreamId for parents
    let lastByStream = initialLastByStream
    let mutable serial = Task.FromResult(())
    /// No NDJSON path on the EventStore success path (field kept for IJournalWriter / test helpers).
    let filePath = ""

    member _.RuntimeId = runtimeId
    member _.BlobWriter = blobWriter
    member _.FilePath = filePath
    member this.LocalSeq = lock gate (fun () -> currentSeq)
    member this.LastCommittedLocalSeq = lock gate (fun () -> currentSeq - 1L)
    member this.IsPoisoned = lock gate (fun () -> poisoned)
    member _.StoreSnapshot = lock gate (fun () -> baseSnapshot)

    static member private formatAppendError(error: AppendError) : string =
        match error with
        | AppendError.StorageInvalid detail -> sprintf "storage invalid: %A" detail
        | AppendError.AppendCasRejected -> "append CAS rejected"
        | AppendError.AppendRetryExhausted -> "append retry exhausted"

    static member private streamKey(stream: StreamId) : string =
        EventStreamId.value (EventStoreJournalCodec.encodeStreamId stream)

    static member private commitEnvelope
        (store: IEventStore)
        (baseSnapshot: StoreSnapshot)
        (lastByStream: IDictionary<string, EventId>)
        (envelope: Envelope)
        : Task<Result<StoreSnapshot, AppendError>> =
        let key = EventStoreJournalWriter.streamKey envelope.Stream

        let parents =
            match lastByStream.TryGetValue key with
            | true, prev -> [ prev ]
            | false, _ -> []

        let encoded = EventStoreJournalCodec.encode parents [] envelope
        store.Append(baseSnapshot, [ encoded ])

    /// Load journal envelopes from a store snapshot (W1-boot).
    /// Non-journal EventTypes (Job*, etc.) are skipped; decode failures fail closed.
    ///
    /// Walks `events/` linearly via GitRawStore.loadEventEnvelopes. Must not
    /// go through EventStoreMergeSpec — that module is the contract-test
    /// set-union oracle, and its previous list-append decoder was O(|history|²).
    static member loadJournalEnvelopes (raw: IGitRawStore) (snapshot: StoreSnapshot) : Task<Result<Envelope list, string>> =
        task {
            match! GitRawStore.loadEventEnvelopes raw snapshot.RootOid with
            | Error detail -> return Error(sprintf "storage invalid: %A" detail)
            | Ok events ->
                let rec decodeAll remaining acc =
                    match remaining with
                    | [] -> Ok(List.rev acc)
                    | head :: tail ->
                        if head.EventType <> EventStoreJournalCodec.JournalEnvelopeEventType then
                            decodeAll tail acc
                        else
                            match EventStoreJournalCodec.tryDecode head with
                            | Error err -> Error err
                            | Ok env -> decodeAll tail (env :: acc)

                match decodeAll events [] with
                | Error err -> return Error err
                | Ok envelopes -> return Ok(List.sortWith Envelope.compareSortKey envelopes)
        }

    /// create(runtimeId, processId, startedAt, store, raw) → writer * RuntimeStarted envelope.
    /// Pass `None` for raw when blob writes are unavailable (tests that only append facts).
    static member create
        (runtimeId: RuntimeId, processId: int, startedAt: DateTimeOffset, store: IEventStore, raw: IGitRawStore option)
        : Task<IJournalWriter * Envelope> =
        task {
            let blobWriter =
                match raw with
                | Some gitRaw -> EventStoreBlobWriter.Create gitRaw
                | None -> UnavailableBlobWriter() :> IBlobWriter

            let initEventId = EventId.create (Guid.NewGuid().ToString("N"))

            let initFact =
                Fact.Runtime(
                    RuntimeStarted
                        {| RuntimeId = runtimeId
                           ProcessId = processId
                           StartedAt = startedAt |}
                )

            let initEnvelope: Envelope =
                { RuntimeId = runtimeId
                  LocalSeq = LocalSeq.create 1L
                  ObservedAt = startedAt
                  EventId = initEventId
                  Stream = StreamId.Workspace
                  ProviderRun = None
                  Fact = initFact }

            let! baseSnapshot = store.OpenSnapshot()
            let lastByStream = Dictionary<string, EventId>() :> IDictionary<string, EventId>

            match! EventStoreJournalWriter.commitEnvelope store baseSnapshot lastByStream initEnvelope with
            | Error err ->
                return
                    failwith (
                        sprintf
                            "EventStore journal create failed to publish RuntimeStarted: %s"
                            (EventStoreJournalWriter.formatAppendError err)
                    )
            | Ok published ->
                lastByStream.[EventStoreJournalWriter.streamKey initEnvelope.Stream] <- initEnvelope.EventId

                let writer =
                    new EventStoreJournalWriter(runtimeId, blobWriter, store, published, lastByStream, 2L)

                return writer :> IJournalWriter, initEnvelope
        }

    /// Resume from an existing store snapshot, or boot empty like `create`.
    /// Replays prior journal envelopes, publishes a fresh RuntimeStarted, and
    /// returns writer + init envelope + folded projection.
    static member resumeOrCreate
        (runtimeId: RuntimeId, processId: int, startedAt: DateTimeOffset, store: IEventStore, raw: IGitRawStore)
        : Task<Result<IJournalWriter * Envelope * ProjectionSet, FoldRejection>> =
        task {
            let blobWriter = EventStoreBlobWriter.Create raw
            let! baseSnapshot = store.OpenSnapshot()

            match! EventStoreJournalWriter.loadJournalEnvelopes raw baseSnapshot with
            | Error msg -> return Error { Fact = "Boot"; Reason = msg }
            | Ok prior ->
                match Fold.apply Fold.empty prior with
                | Error rejection -> return Error rejection
                | Ok replayed ->
                    let lastByStream = Dictionary<string, EventId>() :> IDictionary<string, EventId>

                    for env in prior do
                        lastByStream.[EventStoreJournalWriter.streamKey env.Stream] <- env.EventId

                    let nextLocalSeq =
                        if List.isEmpty prior then
                            1L
                        else
                            (prior |> List.map (fun e -> LocalSeq.value e.LocalSeq) |> List.max) + 1L

                    let initEventId = EventId.create (Guid.NewGuid().ToString("N"))

                    let initFact =
                        Fact.Runtime(
                            RuntimeStarted
                                {| RuntimeId = runtimeId
                                   ProcessId = processId
                                   StartedAt = startedAt |}
                        )

                    let initEnvelope: Envelope =
                        { RuntimeId = runtimeId
                          LocalSeq = LocalSeq.create nextLocalSeq
                          ObservedAt = startedAt
                          EventId = initEventId
                          Stream = StreamId.Workspace
                          ProviderRun = None
                          Fact = initFact }

                    match! EventStoreJournalWriter.commitEnvelope store baseSnapshot lastByStream initEnvelope with
                    | Error err ->
                        return
                            failwith (
                                sprintf
                                    "EventStore journal resumeOrCreate failed to publish RuntimeStarted: %s"
                                    (EventStoreJournalWriter.formatAppendError err)
                            )
                    | Ok published ->
                        lastByStream.[EventStoreJournalWriter.streamKey initEnvelope.Stream] <- initEnvelope.EventId

                        let writer =
                            new EventStoreJournalWriter(
                                runtimeId,
                                blobWriter,
                                store,
                                published,
                                lastByStream,
                                nextLocalSeq + 1L
                            )

                        return
                            Fold.foldEnvelope replayed initEnvelope
                            |> Result.map (fun projection -> writer :> IJournalWriter, initEnvelope, projection)
        }

    member private this.AppendLocked
        (streamKind: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: Fact)
        : Task<CommitResult<Envelope>> =
        task {
            let eventId = EventId.create (Guid.NewGuid().ToString("N"))

            if poisoned || disposed then
                return CommitUnknown(eventId, WriteFailed "Writer is poisoned or disposed")
            else
                let env: Envelope =
                    { RuntimeId = runtimeId
                      LocalSeq = LocalSeq.create currentSeq
                      ObservedAt = DateTimeOffset.UtcNow
                      EventId = eventId
                      Stream = streamKind
                      ProviderRun = providerRun
                      Fact = fact }

                match! EventStoreJournalWriter.commitEnvelope store baseSnapshot lastByStream env with
                | Ok published ->
                    baseSnapshot <- published
                    lastByStream.[EventStoreJournalWriter.streamKey env.Stream] <- env.EventId
                    currentSeq <- currentSeq + 1L
                    return Committed env
                | Error err ->
                    poisoned <- true
                    return CommitUnknown(eventId, WriteFailed(EventStoreJournalWriter.formatAppendError err))
        }

    member private this.Enqueue(work: unit -> Task<'T>) : Task<'T> =
        lock gate (fun () ->
            let prev = serial

            let running =
                task {
                    do! prev
                    return! work ()
                }

            serial <-
                task {
                    try
                        let! _ = running
                        return ()
                    with _ ->
                        return ()
                }

            running)

    member this.Append
        (streamKind: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: Fact)
        : Task<CommitResult<Envelope>> =
        this.Enqueue(fun () -> this.AppendLocked streamKind providerRun fact)

    member private this.DisposeInternal() =
        lock gate (fun () ->
            if not disposed then
                disposed <- true)

    interface IJournalWriter with
        member this.RuntimeId = this.RuntimeId
        member this.BlobWriter = this.BlobWriter
        member this.FilePath = this.FilePath
        member this.LocalSeq = this.LocalSeq
        member this.LastCommittedLocalSeq = this.LastCommittedLocalSeq
        member this.IsPoisoned = this.IsPoisoned
        member this.Append streamKind providerRun fact = this.Append streamKind providerRun fact
        member this.Release() = this.DisposeInternal()

        member this.ReleaseAsync() =
            this.DisposeInternal()
            Fable.Core.JS.Constructors.Promise.resolve () |> unbox<ValueTask>

    interface IDisposable with
        member this.Dispose() = this.DisposeInternal()

    interface IAsyncDisposable with
        member this.DisposeAsync() =
            this.DisposeInternal()
            Fable.Core.JS.Constructors.Promise.resolve () |> unbox<ValueTask>
