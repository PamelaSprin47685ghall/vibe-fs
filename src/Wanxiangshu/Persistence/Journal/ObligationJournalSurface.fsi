namespace Wanxiangshu.Persistence.Journal

open System.Threading.Tasks

/// Journal operations for the relay lifecycle, plus the retired obligation-ledger
/// read boundary.
///
/// obligation-ledger-007: the append is gone, so the only honest answer refuses and
/// names the retirement. The relay lifecycle functions stay because real callers use
/// them; they are relay code that historically sat beside the old ledger route.
[<RequireQualifiedAccess>]
module ObligationJournalSurface =
    /// Retired. A refusal, so a migrated caller learns why instead of inferring it.
    val appendMagicTodo: handle: JournalHandle -> obj

    /// Retired alongside the projection it would have returned.
    val snapshotMagicTodo: handle: JournalHandle -> obj

    val openIncumbency: handle: JournalHandle -> sessionId: string -> incumbencyId: string -> Task<obj>
    val grantWorkOwned: handle: JournalHandle -> sessionId: string -> incumbencyId: string -> Task<obj>

    val appendManagerLifecycle:
        handle: JournalHandle -> sessionId: string -> action: string -> payload: obj -> Task<obj>
