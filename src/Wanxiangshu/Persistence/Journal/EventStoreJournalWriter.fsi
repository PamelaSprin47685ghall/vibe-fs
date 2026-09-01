namespace Wanxiangshu.Persistence.Journal

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Persistence.EventStore

[<Sealed>]
type EventStoreBlobWriter =
    private new: store: IEventStore -> EventStoreBlobWriter

    interface IBlobWriter

    member Write: content: string -> Task<Result<BlobWriteReceipt, string>>
    member Read: blobRef: BlobRef -> Task<Result<string, string>>

    static member Create: store: IEventStore -> IBlobWriter

[<RequireQualifiedAccess>]
module JournalPayloadClosure =
    val ofFact: fact: Fact -> PayloadRef list

[<Sealed>]
type EventStoreJournalWriter =
    private new:
        runtimeId: RuntimeId * init: Envelope * blobWriter: IBlobWriter * store: IEventStore -> EventStoreJournalWriter

    interface IJournalWriter

    member RuntimeId: RuntimeId
    member BlobWriter: IBlobWriter
    member LocalSeq: int64
    member LastCommittedLocalSeq: int64
    member IsPoisoned: bool
    member TryCurrent: key: string -> obj option

    member Append:
        stream: StreamId -> providerRun: ProviderRunIdentity option -> fact: Fact -> Task<CommitResult<Envelope>>

    member Release: unit -> unit
    member ReleaseAsync: unit -> ValueTask

    static member create:
        runtimeId: RuntimeId * processId: int * startedAt: DateTimeOffset * store: IEventStore ->
            Task<IJournalWriter * Envelope>

    static member resumeOrCreate:
        runtimeId: RuntimeId * processId: int * startedAt: DateTimeOffset * store: IEventStore ->
            Task<Result<IJournalWriter * Envelope * ProjectionSet, FoldRejection>>
