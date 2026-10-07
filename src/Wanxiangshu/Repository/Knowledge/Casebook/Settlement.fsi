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
    val describe: error: CasebookMutationError -> string

[<RequireQualifiedAccess>]
module CasebookAppendFailure =
    val code: failure: CasebookAppendFailure -> string
    val finalizeKind: failure: CasebookAppendFailure -> string

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
    val finalized: delegateSessionId: string -> CaseFinalizeSettlement
    val nothingToFinalize: delegateSessionId: string -> CaseFinalizeSettlement
    val notCommitted: delegateSessionId: string -> reason: string -> CaseFinalizeSettlement
    val unknown: delegateSessionId: string -> reason: string -> CaseFinalizeSettlement
    val phaseConflict: delegateSessionId: string -> reason: string -> CaseFinalizeSettlement
    val persistenceFailed: delegateSessionId: string -> failure: CasebookAppendFailure -> CaseFinalizeSettlement
    val releasesIdentity: settlement: CaseFinalizeSettlement -> bool
