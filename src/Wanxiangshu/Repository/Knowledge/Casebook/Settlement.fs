namespace Wanxiangshu.Repository.Knowledge.Casebook

/// CASE-003 / DELEG-031 (F35): the case finalize outcome is a closed
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

    /// Owner retention decision: only a durably-settled finalize releases the
    /// identity. NotCommitted/Unknown retain it so a later recovery can resume
    /// the exact finalize; PhaseConflict retains it for the invariant incident
    /// evidence instead of dropping it in a finally.
    let releasesIdentity (settlement: CaseFinalizeSettlement) : bool =
        match settlement.Commitment with
        | CaseFinalizeCommitment.Finalized
        | CaseFinalizeCommitment.NothingToFinalize -> true
        | _ -> false
