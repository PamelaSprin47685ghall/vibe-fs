namespace Wanxiangshu.Domain

open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Domain.MagicTodoFacts
open Wanxiangshu.Kernel.Identity

/// Legacy open-Life seed adoption (protocol §45 / goal #28).
/// Speculative: only for the upgrade-instant open Life, before first Magic
/// provider request. Subsequent Lives MUST start empty — never re-adopt Host table.
module MagicTodoLegacySeed =

    /// Host TodoTable row as observed at upgrade (no stable id).
    type HostTodoRow =
        { Content: string
          Status: string
          Priority: string }

    [<RequireQualifiedAccess>]
    type LegacySeedReject =
        | LifeAlreadyHasCheckpoint
        | SeedAlreadyAdopted
        | UnknownHostStatus of raw: string
        | EmptySeedNotAllowedWhenHostNonEmpty

    /// Assign fresh Magic ids from LifeId + synthetic seed tool-call identity.
    /// Uses ordinal as newItemOrdinal; seed ToolCallId is stable per Life so
    /// replay of adoption yields the same ids.
    let adopt
        (sha256: string -> string)
        (sessionId: SessionId)
        (lifeId: ManagerLifeId)
        (seedToolCallId: ToolCallId)
        (hostRows: HostTodoRow list)
        : Result<MagicTodoList * LegacyTodoSeedAdopted, LegacySeedReject> =
        let rec loop (rows: HostTodoRow list) (ordinal: int) (acc: MagicTodoItem list) =
            match rows with
            | [] -> Ok(List.rev acc)
            | row :: rest ->
                match TodoStatus.parse row.Status with
                | None -> Error(LegacySeedReject.UnknownHostStatus row.Status)
                | Some status ->
                    // Host may not have reviewing yet; keep parsed status as-is.
                    // Completed without reviewing is allowed ONLY for legacy seed
                    // (pre-protocol history) — Host gate applies to new transitions.
                    let id = MagicTodo.todoItemId sha256 lifeId seedToolCallId ordinal

                    let item =
                        { Id = id
                          Content = row.Content
                          Status = status
                          Priority = row.Priority }

                    loop rest (ordinal + 1) (item :: acc)

        match loop hostRows 0 [] with
        | Error e -> Error e
        | Ok items ->
            // Blob ref/digest filled by caller after persisting list wire body.
            let placeholderRef = BlobRef.create "legacy-todo-seed-pending"
            let placeholderDigest = BlobDigest.create "legacy-todo-seed-pending"

            let fact =
                { ManagerSessionId = sessionId
                  ManagerLifeId = lifeId
                  SeedTodoRef = placeholderRef
                  SeedTodoDigest = placeholderDigest
                  SeedItemIds = items |> List.map (fun i -> i.Id) }

            Ok(items, fact)

    /// Guard: adoption only when Life has no Accepted checkpoints and no prior seed.
    let mayAdopt (legacySeedAdopted: bool) (acceptedCount: int) : Result<unit, LegacySeedReject> =
        if legacySeedAdopted then
            Error LegacySeedReject.SeedAlreadyAdopted
        elif acceptedCount > 0 then
            Error LegacySeedReject.LifeAlreadyHasCheckpoint
        else
            Ok()
