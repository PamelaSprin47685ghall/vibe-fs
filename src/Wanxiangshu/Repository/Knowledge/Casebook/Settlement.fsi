namespace Wanxiangshu.Repository.Knowledge.Casebook

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
    val releasesIdentity: settlement: CaseFinalizeSettlement -> bool
