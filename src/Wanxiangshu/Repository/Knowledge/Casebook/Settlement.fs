namespace Wanxiangshu.Repository.Knowledge.Casebook

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

[<RequireQualifiedAccess>]
type CasebookAppendOperation =
    | Capture
    | Refresh
    | Access
    | Evict

type CasebookAppendFailure =
    { Operation: CasebookAppendOperation
      CaseIdentity: string
      EventId: EventId
      Error: AppendError }

[<RequireQualifiedAccess>]
type CasebookMutationError =
    | AlreadyFinalized of identity: string
    | CaseMissing of identity: string
    | PreparationRejected of reason: string
    | AppendFailure of CasebookAppendFailure

[<RequireQualifiedAccess>]
module CasebookMutationError =
    let describe error =
        match error with
        | CasebookMutationError.AlreadyFinalized identity -> sprintf "case already finalized for scope %s" identity
        | CasebookMutationError.CaseMissing identity -> sprintf "case %s not found" identity
        | CasebookMutationError.PreparationRejected reason -> reason
        | CasebookMutationError.AppendFailure failure ->
            sprintf
                "case %s append %s failed: %s"
                failure.CaseIdentity
                (EventId.value failure.EventId)
                (AppendError.describe failure.Error)

[<RequireQualifiedAccess>]
module CasebookAppendFailure =
    let code failure =
        match failure.Error with
        | AppendError.AppendNotAttempted _ -> "CASEBOOK_APPEND_NOT_ATTEMPTED"
        | AppendError.CommitUnknown _ -> "CASEBOOK_APPEND_COMMIT_UNKNOWN"
        | AppendError.NoNewWriteReleaseFailed _ -> "CASEBOOK_APPEND_NO_NEW_WRITE_RELEASE_FAILED"
        | _ -> "CASEBOOK_APPEND_FAILED"

    let finalizeKind failure =
        match failure.Error with
        | AppendError.CommitUnknown _ -> "unknown"
        | AppendError.NoNewWriteReleaseFailed _ -> "noNewWriteReleaseFailed"
        | _ -> "notCommitted"

[<Sealed>]
type CasebookSemanticCutIncident private (failure: CasebookAppendFailure) =
    inherit
        System.Exception(sprintf "Casebook semantic cut for %s/%s" failure.CaseIdentity (EventId.value failure.EventId))

    member _.Failure = failure
    member _.Cuts = AppendError.semanticCuts failure.Error

    static member internal TryFromSettlement(failure: CasebookAppendFailure) =
        let relevant =
            AppendError.semanticCuts failure.Error
            |> List.exists (fun cut -> cut.FailedEventId = failure.EventId && cut.Rule = "Casebook")

        if relevant then
            Some(CasebookSemanticCutIncident failure)
        else
            None

[<RequireQualifiedAccess>]
module CasebookSemanticCutIncident =
    let tryFromSettlement failure =
        CasebookSemanticCutIncident.TryFromSettlement failure

/// CASE-003 / delegation-031 (F35): the case finalize outcome is a closed
/// settlement, never a bare Result&lt;unit, string&gt;. `Finalized` and
/// `NothingToFinalize` both release the identity; `NotCommitted` and
/// `Unknown` RETAIN it so a later recovery can resume the exact finalize;
/// `PhaseConflict` is the exactly-one invariant cut. The identity is always
/// carried so the deletion owner can decide retention explicitly.
type CaseFinalizeIdentity = { DelegateSessionId: string }

[<RequireQualifiedAccess>]
type CaseFinalizeCommitment =
    | Finalized
    | NothingToFinalize
    | NotCommitted of reason: string
    | Unknown of reason: string
    | PhaseConflict of reason: string
    | PersistenceFailed of CasebookAppendFailure

type CaseFinalizeSettlement =
    { Identity: CaseFinalizeIdentity
      Commitment: CaseFinalizeCommitment }

[<RequireQualifiedAccess>]
module CaseFinalizeSettlement =

    let finalized (delegateSessionId: string) : CaseFinalizeSettlement =
        { Identity = { DelegateSessionId = delegateSessionId }
          Commitment = CaseFinalizeCommitment.Finalized }

    let nothingToFinalize (delegateSessionId: string) : CaseFinalizeSettlement =
        { Identity = { DelegateSessionId = delegateSessionId }
          Commitment = CaseFinalizeCommitment.NothingToFinalize }

    let notCommitted (delegateSessionId: string) (reason: string) : CaseFinalizeSettlement =
        { Identity = { DelegateSessionId = delegateSessionId }
          Commitment = CaseFinalizeCommitment.NotCommitted reason }

    let unknown (delegateSessionId: string) (reason: string) : CaseFinalizeSettlement =
        { Identity = { DelegateSessionId = delegateSessionId }
          Commitment = CaseFinalizeCommitment.Unknown reason }

    let phaseConflict (delegateSessionId: string) (reason: string) : CaseFinalizeSettlement =
        { Identity = { DelegateSessionId = delegateSessionId }
          Commitment = CaseFinalizeCommitment.PhaseConflict reason }

    let persistenceFailed (delegateSessionId: string) (failure: CasebookAppendFailure) : CaseFinalizeSettlement =
        { Identity = { DelegateSessionId = delegateSessionId }
          Commitment = CaseFinalizeCommitment.PersistenceFailed failure }

    /// Owner retention decision: only a durably-settled finalize releases the
    /// identity. NotCommitted/Unknown retain it so a later recovery can resume
    /// the exact finalize; PhaseConflict retains it for the invariant incident
    /// evidence instead of dropping it in a finally.
    let releasesIdentity (settlement: CaseFinalizeSettlement) : bool =
        match settlement.Commitment with
        | CaseFinalizeCommitment.Finalized
        | CaseFinalizeCommitment.NothingToFinalize -> true
        | _ -> false
