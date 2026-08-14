namespace Wanxiangshu.Persistence.Journal

open System
open System.Text
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Infrastructure.Persist
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Outcome
open Wanxiangshu.Composition.Durable

/// Local payload writer for EventStore journals.
/// `BlobRef` keeps the long-standing `blobs/<handle>` application shape, while
/// `<handle>` is now the local content-addressed PayloadRef. Git OIDs do not
/// participate in runtime durability.
type EventStoreBlobWriter private (store: IEventStore) =
    member _.Write(content: string) : Task<Result<BlobWriteReceipt, string>> =
        task {
            try
                let digest = BlobDigest.create (HostDigest.sha256Hex content)
                let bytes = Encoding.UTF8.GetBytes content

                match! store.WritePayload bytes with
                | Error error -> return Error error
                | Ok payloadRef ->
                    return
                        Ok
                            { BlobRef = BlobRef.create ("blobs/" + PayloadRef.value payloadRef)
                              BlobDigest = digest }
            with ex ->
                return Error(sprintf "event-store payload write failed: %s" ex.Message)
        }

    member _.Read(blobRef: BlobRef) : Task<Result<string, string>> =
        task {
            let relative = BlobRef.value blobRef
            let prefix = "blobs/"

            if not (relative.StartsWith(prefix, StringComparison.Ordinal)) then
                return Error(sprintf "invalid blob reference: %s" relative)
            else
                let handle = relative.Substring(prefix.Length)

                if String.IsNullOrWhiteSpace handle || handle.Contains "/" then
                    return Error(sprintf "invalid blob reference: %s" relative)
                else
                    match! store.ReadPayload(PayloadRef.create handle) with
                    | Error error -> return Error error
                    | Ok None -> return Error(sprintf "event-store payload missing: %s" handle)
                    | Ok(Some bytes) ->
                        try
                            return Ok(Encoding.UTF8.GetString bytes)
                        with ex ->
                            return Error(sprintf "event-store payload read failed: %s" ex.Message)
        }

    interface IBlobWriter with
        member this.Write(content) = this.Write content
        member this.Read(blobRef) = this.Read blobRef

    static member Create(store: IEventStore) : IBlobWriter =
        EventStoreBlobWriter(store) :> IBlobWriter

/// Journal writer backed by the local process EventStore.
/// It never enumerates history and never folds facts itself. CanonicalIntegrator
/// owns both boot replay and live integration; this type only assigns journal
/// envelope identity/sequence and appends one universal EventEnvelope.
type EventStoreJournalWriter
    private
    (
        runtimeId: RuntimeId,
        blobWriter: IBlobWriter,
        store: IEventStore,
        initialCurrentSeq: int64
    ) =
    let gate = obj ()
    // DSL-MUTABLE: resource — next LocalSeq for this fresh RuntimeId only.
    let mutable currentSeq = initialCurrentSeq
    // DSL-MUTABLE: resource — local append poison latch.
    let mutable poisoned = false
    // DSL-MUTABLE: resource — dispose latch.
    let mutable disposed = false
    // DSL-MUTABLE: resource — serialized process writer operations.
    let mutable serial = Task.FromResult(())

    member _.RuntimeId = runtimeId
    member _.BlobWriter = blobWriter
    member _.FilePath = ""
    member _.LocalSeq = lock gate (fun () -> currentSeq)
    member _.LastCommittedLocalSeq = lock gate (fun () -> currentSeq - 1L)
    member _.IsPoisoned = lock gate (fun () -> poisoned)
    member _.TryCurrent(key: string) = store.TryCurrent key

    static member private formatAppendError(error: AppendError) : string =
        match error with
        | AppendError.StorageInvalid detail -> sprintf "storage invalid: %A" detail
        | AppendError.AppendFailed reason -> "append failed: " + reason

    static member private commitEnvelope
        (store: IEventStore)
        (envelope: Envelope)
        : Task<Result<unit, AppendError>> =
        let streamId = EventStoreJournalCodec.encodeStreamId envelope.Stream
        let parents = store.TryHead streamId |> Option.toList
        let encoded = EventStoreJournalCodec.encode parents [] envelope
        store.Append [ encoded ]

    static member private currentJournalProjection(store: IEventStore) : Result<ProjectionSet, FoldRejection> =
        match store.TryCurrent "Journal" with
        | Some current -> Ok(unbox<ProjectionSet> current)
        | None -> Ok Fold.empty

    static member private initEnvelope
        (runtimeId: RuntimeId)
        (processId: int)
        (startedAt: DateTimeOffset)
        : Envelope =
        { RuntimeId = runtimeId
          LocalSeq = LocalSeq.create 1L
          ObservedAt = startedAt
          EventId = EventId.create (Guid.NewGuid().ToString("N"))
          Stream = StreamId.Workspace
          ProviderRun = None
          Fact =
            Fact.Runtime(
                RuntimeStarted
                    {| RuntimeId = runtimeId
                       ProcessId = processId
                       StartedAt = startedAt |}
            ) }

    /// Fresh runtime: every process receives a fresh RuntimeId and therefore
    /// LocalSeq starts at 1. Prior history is already in CanonicalIntegrator.Current.
    static member create
        (runtimeId: RuntimeId, processId: int, startedAt: DateTimeOffset, store: IEventStore)
        : Task<IJournalWriter * Envelope> =
        task {
            let init = EventStoreJournalWriter.initEnvelope runtimeId processId startedAt

            match! EventStoreJournalWriter.commitEnvelope store init with
            | Error error ->
                return
                    failwith (
                        sprintf
                            "EventStore journal create failed to append RuntimeStarted: %s"
                            (EventStoreJournalWriter.formatAppendError error)
                    )
            | Ok() ->
                let writer =
                    EventStoreJournalWriter(runtimeId, EventStoreBlobWriter.Create store, store, 2L)

                return writer :> IJournalWriter, init
        }

    /// Boot never reads history here. WorkspaceEventStore already replayed every
    /// process writer stream through CanonicalIntegrator before this call.
    static member resumeOrCreate
        (runtimeId: RuntimeId, processId: int, startedAt: DateTimeOffset, store: IEventStore)
        : Task<Result<IJournalWriter * Envelope * ProjectionSet, FoldRejection>> =
        task {
            let init = EventStoreJournalWriter.initEnvelope runtimeId processId startedAt

            match! EventStoreJournalWriter.commitEnvelope store init with
            | Error error ->
                return
                    failwith (
                        sprintf
                            "EventStore journal resumeOrCreate failed to append RuntimeStarted: %s"
                            (EventStoreJournalWriter.formatAppendError error)
                    )
            | Ok() ->
                match EventStoreJournalWriter.currentJournalProjection store with
                | Error rejection -> return Error rejection
                | Ok projection ->
                    let writer =
                        EventStoreJournalWriter(runtimeId, EventStoreBlobWriter.Create store, store, 2L)

                    return Ok(writer :> IJournalWriter, init, projection)
        }

    member private _.AppendLocked
        (stream: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: Fact)
        : Task<CommitResult<Envelope>> =
        task {
            let eventId = EventId.create (Guid.NewGuid().ToString("N"))

            if poisoned || disposed then
                return CommitUnknown(eventId, WriteFailed "Writer is poisoned or disposed")
            else
                let envelope: Envelope =
                    { RuntimeId = runtimeId
                      LocalSeq = LocalSeq.create currentSeq
                      ObservedAt = DateTimeOffset.UtcNow
                      EventId = eventId
                      Stream = stream
                      ProviderRun = providerRun
                      Fact = fact }

                match! EventStoreJournalWriter.commitEnvelope store envelope with
                | Ok() ->
                    currentSeq <- currentSeq + 1L
                    return Committed envelope
                | Error error ->
                    poisoned <- true
                    return CommitUnknown(eventId, WriteFailed(EventStoreJournalWriter.formatAppendError error))
        }

    member private _.Enqueue(work: unit -> Task<'T>) : Task<'T> =
        lock gate (fun () ->
            let previous = serial

            let running =
                task {
                    do! previous
                    return! work ()
                }

            serial <- task { let! _ = running in return () }
            running)

    member this.Append
        (stream: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: Fact)
        : Task<CommitResult<Envelope>> =
        this.Enqueue(fun () -> this.AppendLocked stream providerRun fact)

    member _.Release() = lock gate (fun () -> disposed <- true)

    member this.ReleaseAsync() =
        this.Release()
        ValueTask()

    interface IJournalWriter with
        member this.RuntimeId = this.RuntimeId
        member this.BlobWriter = this.BlobWriter
        member this.FilePath = this.FilePath
        member this.LocalSeq = this.LocalSeq
        member this.LastCommittedLocalSeq = this.LastCommittedLocalSeq
        member this.IsPoisoned = this.IsPoisoned
        member this.TryCurrent(key) = this.TryCurrent key
        member this.Append stream providerRun fact = this.Append stream providerRun fact
        member this.Release() = this.Release()
        member this.ReleaseAsync() = this.ReleaseAsync()
