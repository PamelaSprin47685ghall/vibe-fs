namespace Wanxiangshu.Journal

open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Outcome

/// Byte-offset frontier keyed by runtime (legacy NDJSON boot field retained for
/// `RuntimeSnapshot` shape; production durability is EventStore-only after Phase 5).
type Frontier = Map<RuntimeId, int64>

type BlobWriteReceipt =
    { BlobRef: BlobRef
      BlobDigest: BlobDigest }

type IBlobWriter =
    abstract Write: string -> Task<Result<BlobWriteReceipt, string>>
    abstract Read: BlobRef -> Task<Result<string, string>>

/// Shared writer surface for EventStore-backed journals.
/// EventStore implementation lives in EventStoreJournalWriter.fs so this file
/// stays free of IEventStore tokens (unified-store dual-write gate).
type IJournalWriter =
    abstract RuntimeId: RuntimeId
    abstract BlobWriter: IBlobWriter
    abstract FilePath: string
    abstract LocalSeq: int64
    abstract LastCommittedLocalSeq: int64
    abstract IsPoisoned: bool
    /// Read one canonical Integrator Current slot. The writer exposes the store's
    /// read-only projection surface without giving AgentJournal a history reader.
    abstract TryCurrent: key: string -> obj option
    abstract Append: StreamId -> ProviderRunIdentity option -> Fact -> Task<CommitResult<Envelope>>
    /// Release durable resources (fd / latches). Prefer over IDisposable on the
    /// interface so Fable does not collide with System.IDisposable.Dispose.
    abstract Release: unit -> unit
    abstract ReleaseAsync: unit -> ValueTask
