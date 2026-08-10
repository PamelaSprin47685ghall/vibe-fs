namespace Wanxiangshu.Domain

open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Kernel.Identity

/// Pure before-hook admission pipeline (protocol §11.3).
/// Speculative / unwired: Host callID→ToolPart localization and Journal append
/// remain at the membrane boundary; this module owns only fail-closed Magic checks.
module MagicTodoAdmission =

    type LocalizedToolCall =
        { ToolCallId: ToolCallId
          ToolPartOrdinal: int
          /// Other todowrite ToolCallIds in the same assistant message (incl. self).
          TodowriteCallIdsInMessage: ToolCallId list
          ReviewFrontier: XTraceCursor
          ProviderInputDigest: string }

    type PrepareSuccess =
        { TodoWriteId: TodoWriteId
          NormalizedProposed: MagicTodoList
          BaseTodo: MagicTodoList
          BaseTodoDigest: string
          ProposedTodoDigest: string
          RevisePreview: MagicTodoList
          ReviewFrontier: XTraceCursor
          ToolPartOrdinal: int
          ProviderInputDigest: string }

    /// Optional frozen Prepared for same-ToolCallId replay.
    type ExistingPrepared =
        { Identity: PreparedIdentity
          TodoWriteId: TodoWriteId }

    /// Result of Magic before admission prior to mutating Host args / appending Prepared.
    type AdmissionOutcome =
        | FreshPrepare of PrepareSuccess
        | IdempotentReplay of TodoWriteId
        | Rejected of MagicTodoReject

    /// Full Magic validation path for a todowrite before-hook.
    /// Does not append Journal facts or mutate Host args — caller does.
    let admit
        (sha256: string -> string)
        (lifeId: ManagerLifeId)
        (settledCurrent: MagicTodoList)
        (mayProceedPastLag1: Result<unit, MagicTodoReject>)
        (existingPrepared: ExistingPrepared option)
        (localized: LocalizedToolCall)
        (rawInputs: MagicTodoInputItem list)
        : AdmissionOutcome =
        match mayProceedPastLag1 with
        | Error e -> AdmissionOutcome.Rejected e
        | Ok() ->
            match MagicTodo.admitTodowriteBatch localized.TodowriteCallIdsInMessage with
            | Error e -> AdmissionOutcome.Rejected e
            | Ok() ->
                let writeId = MagicTodo.todoWriteId sha256 lifeId localized.ToolCallId

                match existingPrepared with
                | Some existing when TodoWriteId.value existing.TodoWriteId = TodoWriteId.value writeId ->
                    let observed =
                        { ManagerLifeId = lifeId
                          ProviderInputDigest = localized.ProviderInputDigest
                          BaseTodoDigest = MagicTodo.listDigest sha256 settledCurrent
                          ToolPartOrdinal = localized.ToolPartOrdinal }

                    match MagicTodo.checkPreparedReplay existing.Identity observed with
                    | Error e -> AdmissionOutcome.Rejected e
                    | Ok() -> AdmissionOutcome.IdempotentReplay writeId
                | Some _ -> AdmissionOutcome.Rejected(MagicTodoReject.IdentityCorruption "TodoWriteId")
                | None ->
                    match MagicTodo.normalizeProposed sha256 lifeId localized.ToolCallId settledCurrent rawInputs with
                    | Error e -> AdmissionOutcome.Rejected e
                    | Ok proposed ->
                        let baseDigest = MagicTodo.listDigest sha256 settledCurrent
                        let proposedDigest = MagicTodo.listDigest sha256 proposed

                        AdmissionOutcome.FreshPrepare
                            { TodoWriteId = writeId
                              NormalizedProposed = proposed
                              BaseTodo = settledCurrent
                              BaseTodoDigest = baseDigest
                              ProposedTodoDigest = proposedDigest
                              RevisePreview = MagicTodo.semanticMerge settledCurrent proposed
                              ReviewFrontier = localized.ReviewFrontier
                              ToolPartOrdinal = localized.ToolPartOrdinal
                              ProviderInputDigest = localized.ProviderInputDigest }

    /// Ephemeral before→after bridge payload (protocol §12). Process-local only;
    /// never durable truth. JS membrane stores this under a non-enumerable Symbol.
    type EphemeralBridge =
        { SettledOld: MagicTodoList
          NormalizedProposal: MagicTodoList
          PreviousReviewReport: string option
          PreviousVerdict: ProcessReviewVerdict option
          RevisePreview: MagicTodoList
          CompatibilityProjection: MagicTodoSurface.CompatibilityTodoRow list
          TodoWriteId: TodoWriteId
          ProviderInputDigest: string }
