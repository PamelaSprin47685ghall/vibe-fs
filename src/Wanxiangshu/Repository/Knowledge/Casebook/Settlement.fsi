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
    val finalized: inspectorSessionId: string -> InspectorFinalizeSettlement
    val nothingToFinalize: inspectorSessionId: string -> InspectorFinalizeSettlement
    val notCommitted: inspectorSessionId: string -> reason: string -> InspectorFinalizeSettlement
    val unknown: inspectorSessionId: string -> reason: string -> InspectorFinalizeSettlement
    val phaseConflict: inspectorSessionId: string -> reason: string -> InspectorFinalizeSettlement
    val releasesIdentity: settlement: InspectorFinalizeSettlement -> bool
