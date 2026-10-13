namespace Wanxiangshu.Persistence.Journal

open System.Threading.Tasks

/// Retired obligation-ledger read boundary, plus relay-shaped entry points that
/// no longer append lifecycle facts.
///
/// obligation-ledger-007: the magic-todo append is gone. This surface refuses
/// lifecycle appends and writes nothing.
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
