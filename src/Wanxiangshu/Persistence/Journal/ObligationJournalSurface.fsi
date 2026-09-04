namespace Wanxiangshu.Persistence.Journal

open System.Threading.Tasks

/// Journal owner operations specific to the obligation ledger. The generic
/// JournalSurface owns boot/release; this module owns MagicTodo facts and the
/// compact projections that prove their durability.
[<RequireQualifiedAccess>]
module ObligationJournalSurface =
    val appendMagicTodo: handle: JournalHandle -> sessionId: string -> providerRun: obj -> factJson: string -> Task<obj>
    val snapshotMagicTodo: handle: JournalHandle -> incumbencyId: string -> obj
