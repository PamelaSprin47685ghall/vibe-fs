namespace Wanxiangshu.Repository.Knowledge.Casebook

/// CASE-003 / DELEG-031 (F35): the inspector finalize outcome is a closed
/// settlement, never a bare Result&lt;unit, string&gt;. `Finalized` and
/// `NothingToFinalize` both release the identity; `NotCommitted` and
/// `Unknown` RETAIN it so a later recovery can resume the exact finalize;
/// `PhaseConflict` is the exactly-one invariant cut. The identity is always
/// carried so the deletion owner can decide retention explicitly.
type InspectorFinalizeIdentity = { InspectorSessionId: string }

[<RequireQualifiedAccess>]
type InspectorFinalizeCommitment =
    | Finalized
    | NothingToFinalize
    | NotCommitted of reason: string
    | Unknown of reason: string
    | PhaseConflict of reason: string

type InspectorFinalizeSettlement =
    { Identity: InspectorFinalizeIdentity
      Commitment: InspectorFinalizeCommitment }

[<RequireQualifiedAccess>]
module InspectorFinalizeSettlement =

    let finalized (inspectorSessionId: string) : InspectorFinalizeSettlement =
        { Identity = { InspectorSessionId = inspectorSessionId }
          Commitment = InspectorFinalizeCommitment.Finalized }

    let nothingToFinalize (inspectorSessionId: string) : InspectorFinalizeSettlement =
        { Identity = { InspectorSessionId = inspectorSessionId }
          Commitment = InspectorFinalizeCommitment.NothingToFinalize }

    let notCommitted (inspectorSessionId: string) (reason: string) : InspectorFinalizeSettlement =
        { Identity = { InspectorSessionId = inspectorSessionId }
          Commitment = InspectorFinalizeCommitment.NotCommitted reason }

    let unknown (inspectorSessionId: string) (reason: string) : InspectorFinalizeSettlement =
        { Identity = { InspectorSessionId = inspectorSessionId }
          Commitment = InspectorFinalizeCommitment.Unknown reason }

    let phaseConflict (inspectorSessionId: string) (reason: string) : InspectorFinalizeSettlement =
        { Identity = { InspectorSessionId = inspectorSessionId }
          Commitment = InspectorFinalizeCommitment.PhaseConflict reason }

    /// Owner retention decision: only a durably-settled finalize releases the
    /// identity. NotCommitted/Unknown retain it so a later recovery can resume
    /// the exact finalize; PhaseConflict retains it for the invariant incident
    /// evidence instead of dropping it in a finally.
    let releasesIdentity (settlement: InspectorFinalizeSettlement) : bool =
        match settlement.Commitment with
        | InspectorFinalizeCommitment.Finalized
        | InspectorFinalizeCommitment.NothingToFinalize -> true
        | _ -> false
