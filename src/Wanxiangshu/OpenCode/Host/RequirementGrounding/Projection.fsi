namespace Wanxiangshu.OpenCode.Host.RequirementGrounding

open Wanxiangshu.Requirement.Grounding

type RequirementGroundingProjectionState =
    { Pending: Map<string, GroundingSnapshot>
      OccurrencesRev: RequirementGroundingOccurrence list
      VisibleMaterials: Set<string>
      VisibleFromOrdinal: int64 }

[<RequireQualifiedAccess>]
type RequirementGroundingFoldRejection =
    | NonSequentialOrdinal of expected: int64 * actual: int64
    | DuplicateIdentity of identity: string
    | MissingRequest of identity: string

module RequirementGroundingProjection =
    val empty: RequirementGroundingProjectionState
    val isSnapshotGrounded: GroundingSnapshot -> RequirementGroundingProjectionState -> bool
    val snapshotRequested: GroundingSnapshot -> RequirementGroundingProjectionState -> bool
    val pending: RequirementGroundingProjectionState -> GroundingSnapshot list
    val occurrences: RequirementGroundingProjectionState -> RequirementGroundingOccurrence list
    val visibleOccurrences: RequirementGroundingProjectionState -> RequirementGroundingOccurrence list
    val groundedKeys: RequirementGroundingProjectionState -> string list
    val nextOrdinal: RequirementGroundingProjectionState -> int64
    val applyReanchor: RequirementGroundingProjectionState -> RequirementGroundingProjectionState
    val applyRequested: GroundingSnapshot -> RequirementGroundingProjectionState -> RequirementGroundingProjectionState
    val applyMaterialObserved:
        RequirementGroundingMaterialObserved -> RequirementGroundingProjectionState -> RequirementGroundingProjectionState
    val applyAnchored:
        RequirementGroundingOccurrence ->
        RequirementGroundingProjectionState ->
        Result<RequirementGroundingProjectionState, RequirementGroundingFoldRejection>
