namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection

open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Foundation.Identity

/// Pure before-hook admission pipeline (protocol §11.3).
/// Host callID→ToolPart localization and Journal append remain at the membrane
/// boundary; this module owns only fail-closed Magic checks.
module MagicTodoAdmission =

    type AdmissionLocalizedToolCall =
        {
            ToolCallId: ToolCallId
            ToolPartOrdinal: int
            /// Other todowrite ToolCallIds in the same assistant message (incl. self).
            TodowriteCallIdsInMessage: ToolCallId list
            ReviewFrontier: XTraceCursor
            ProviderInputDigest: string
        }

    /// GrandRewrite clean-break prepare plan. New provider calls carry only the
    /// obligation account; historical tagged TodoItem plans remain decode-only.
    type ObligationPrepareSuccess =
        { TodoWriteId: TodoWriteId
          Proposed: ObligationList
          Base: ObligationList
          BaseDigest: string
          ProposedDigest: string
          ReviewFrontier: XTraceCursor
          ToolPartOrdinal: int
          ProviderInputDigest: string }

    /// Optional frozen Prepared for same-ToolCallId replay.
    type ExistingPrepared =
        { Identity: PreparedIdentity
          TodoWriteId: TodoWriteId
          Accepted: bool }

    /// Result of Magic before admission prior to mutating Host args / appending Prepared.
    /// The control algebra is identical for historical and clean-break plans; only
    /// the successful prepare payload differs.
    ///
    /// `AwaitingConsumableReview` is a legal lag-1 wait (TODO-006 / HOST-019 deferred
    /// prepare), not a fail-closed reject.
    [<RequireQualifiedAccess>]
    type AdmissionOutcome<'Prepare> =
        | FreshPrepare of 'Prepare
        | IdempotentReplay of TodoWriteId
        | AwaitingConsumableReview of pendingTodoWriteId: string
        | Rejected of MagicTodoReject

    let private decideReplay
        (sha256: string -> string)
        (lifeId: ManagerLifeId)
        (settledCurrent: ObligationList)
        (localized: AdmissionLocalizedToolCall)
        (existing: ExistingPrepared)
        (writeId: TodoWriteId)
        : AdmissionOutcome<ObligationPrepareSuccess> =
        let observed =
            { ManagerLifeId = lifeId
              ProviderInputDigest = localized.ProviderInputDigest
              BaseTodoDigest = MagicTodo.obligationListDigest sha256 settledCurrent
              ToolPartOrdinal = localized.ToolPartOrdinal }

        let acceptedReplayCheck () =
            // Accepted checkpoints may have advanced Current, so their frozen
            // BaseTodoDigest is historical evidence rather than current input.
            // Keep the durable call identity checks; do not compare that stale
            // base against the post-accept projection.
            match
                existing.Identity.ManagerLifeId = observed.ManagerLifeId,
                existing.Identity.ProviderInputDigest = observed.ProviderInputDigest,
                existing.Identity.ToolPartOrdinal = observed.ToolPartOrdinal
            with
            | false, _, _ -> Error(MagicTodoReject.IdentityCorruption "ManagerLifeId")
            | true, false, _ -> Error(MagicTodoReject.IdentityCorruption "ProviderInputDigest")
            | true, true, false -> Error(MagicTodoReject.IdentityCorruption "ToolPartOrdinal")
            | true, true, true -> Ok()

        let replayCheck =
            match existing.Accepted with
            | true -> acceptedReplayCheck ()
            | false -> MagicTodo.checkPreparedReplay existing.Identity observed

        match replayCheck with
        | Error e -> AdmissionOutcome.Rejected e
        | Ok() -> AdmissionOutcome.IdempotentReplay writeId

    let private decideFresh
        (sha256: string -> string)
        (settledCurrent: ObligationList)
        (mayProceedPastLag1: Result<unit, MagicTodoReject>)
        (localized: AdmissionLocalizedToolCall)
        (submitted: ObligationList)
        (writeId: TodoWriteId)
        : AdmissionOutcome<ObligationPrepareSuccess> =
        match
            mayProceedPastLag1
            |> Result.bind (fun () -> MagicTodo.validateObligations submitted)
        with
        | Error(MagicTodoReject.AwaitingConsumableReview pending) -> AdmissionOutcome.AwaitingConsumableReview pending
        | Error e -> AdmissionOutcome.Rejected e
        | Ok proposed ->
            AdmissionOutcome.FreshPrepare
                { TodoWriteId = writeId
                  Proposed = proposed
                  Base = settledCurrent
                  BaseDigest = MagicTodo.obligationListDigest sha256 settledCurrent
                  ProposedDigest = MagicTodo.obligationListDigest sha256 proposed
                  ReviewFrontier = localized.ReviewFrontier
                  ToolPartOrdinal = localized.ToolPartOrdinal
                  ProviderInputDigest = localized.ProviderInputDigest }

    let private admitAfterBatch
        (sha256: string -> string)
        (lifeId: ManagerLifeId)
        (settledCurrent: ObligationList)
        (mayProceedPastLag1: Result<unit, MagicTodoReject>)
        (existingPrepared: ExistingPrepared option)
        (localized: AdmissionLocalizedToolCall)
        (submitted: ObligationList)
        : AdmissionOutcome<ObligationPrepareSuccess> =
        let writeId = MagicTodo.todoWriteId sha256 lifeId localized.ToolCallId

        match existingPrepared with
        | Some existing when TodoWriteId.value existing.TodoWriteId = TodoWriteId.value writeId ->
            decideReplay sha256 lifeId settledCurrent localized existing writeId
        | Some _ -> AdmissionOutcome.Rejected(MagicTodoReject.IdentityCorruption "TodoWriteId")
        | None -> decideFresh sha256 settledCurrent mayProceedPastLag1 localized submitted writeId

    /// GrandRewrite admission: same durable identity / lag-1 law, but no
    /// provider-visible item ids or status machine. Duplicate names fail closed.
    let admitObligations
        (sha256: string -> string)
        (lifeId: ManagerLifeId)
        (settledCurrent: ObligationList)
        (mayProceedPastLag1: Result<unit, MagicTodoReject>)
        (existingPrepared: ExistingPrepared option)
        (localized: AdmissionLocalizedToolCall)
        (submitted: ObligationList)
        : AdmissionOutcome<ObligationPrepareSuccess> =
        match MagicTodo.admitTodowriteBatch localized.TodowriteCallIdsInMessage with
        | Error e -> AdmissionOutcome.Rejected e
        | Ok() -> admitAfterBatch sha256 lifeId settledCurrent mayProceedPastLag1 existingPrepared localized submitted
