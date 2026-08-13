namespace Wanxiangshu.Journal

open System
open System.Threading.Tasks
open Wanxiangshu.Kernel.Identity

/// Host-facing EventStore journal boot port.
///
/// Kept free of `IEventStore` / `AppendCandidate` / `EventStore.create*` tokens so
/// composition roots that already name `AgentJournal` can depend on this seam
/// without tripping the unified-store dual-write gate.
type IJournalEventStoreBoot =
    abstract ResumeOrCreate:
        RuntimeId * int * DateTimeOffset -> Task<Result<IJournalWriter * Envelope * ProjectionSet, FoldRejection>>
