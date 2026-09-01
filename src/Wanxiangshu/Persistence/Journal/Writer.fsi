namespace Wanxiangshu.Persistence.Journal

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome

type BlobWriteReceipt =
    { BlobRef: BlobRef
      BlobDigest: BlobDigest }

type IBlobWriter =
    abstract Write: string -> Task<Result<BlobWriteReceipt, string>>
    abstract Read: BlobRef -> Task<Result<string, string>>

type IJournalWriter =
    abstract RuntimeId: RuntimeId
    abstract BlobWriter: IBlobWriter
    abstract LocalSeq: int64
    abstract LastCommittedLocalSeq: int64
    abstract IsPoisoned: bool
    abstract TryCurrent: key: string -> obj option
    abstract Append: StreamId -> ProviderRunIdentity option -> Fact -> Task<CommitResult<Envelope>>
    abstract Release: unit -> unit
    abstract ReleaseAsync: unit -> ValueTask
