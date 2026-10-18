namespace Wanxiangshu.Persistence.Journal

open System.Threading.Tasks

/// Retired obligation-ledger read boundary, plus relay-shaped entry points that
/// no longer append lifecycle facts.
///
/// obligation-ledger-007: the magic-todo append is gone. This surface has an
/// AgentJournal handle, not an event store, an observed head, or a work id, so
/// it refuses lifecycle appends and writes nothing.
[<RequireQualifiedAccess>]
module ObligationJournalSurface =

    let private refused reason : Task<obj> =
        Task.FromResult(box {| ok = false; error = reason |})

    /// Retired. A refusal, so a migrated caller learns why instead of inferring it.
    let appendMagicTodo (handle: JournalHandle) : obj =
        ignore handle

        box
            {| ok = false
               error =
                "appendMagicTodo is retired (obligation-ledger-007); programming-surface view declarations own context compression and successful calls append ViewCheckpointCommitted only for context compression" |}

    /// Retired alongside the projection it would have returned.
    let snapshotMagicTodo (handle: JournalHandle) : obj =
        ignore handle
        null

    let openIncumbency (handle: JournalHandle) (sessionId: string) (incumbencyId: string) : Task<obj> =
        ignore handle
        ignore sessionId
        ignore incumbencyId

        refused
            "openIncumbency refused: this surface has no event store, observed head, or work id, so it writes nothing"

    let grantWorkOwned (handle: JournalHandle) (sessionId: string) (incumbencyId: string) : Task<obj> =
        ignore handle
        ignore sessionId
        ignore incumbencyId

        refused
            "grantWorkOwned refused: this surface has no event store, observed head, or work id, so it writes nothing"

    let appendManagerLifecycle (handle: JournalHandle) (sessionId: string) (action: string) (payload: obj) : Task<obj> =
        match action with
        | "LifeOpened"
        | "LifeCompleted" ->
            ignore handle
            ignore sessionId
            ignore payload

            refused
                "appendManagerLifecycle refused: this surface has no event store, observed head, or work id, so it writes nothing"
        | _ -> Task.FromResult(box {| ok = true |})
