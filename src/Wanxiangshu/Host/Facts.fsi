namespace Wanxiangshu.Host

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Concern
open Wanxiangshu.Requirement.Grounding

[<RequireQualifiedAccess>]
type TipPresentation =
    | Full
    | IdentityOnly

type HostFactCases =
    | PairProgrammingGuidelineAnchored of
        {| SessionId: SessionId
           Ordinal: int64
           CallId: ToolCallId
           MarkerText: string
           CallGap: TranscriptGap
           ResultGap: TranscriptGap
           ConcernPlacement: ConcernPlacementBatch option |}
    | RequirementGroundingRequested of
        {| SessionId: SessionId
           Snapshot: GroundingSnapshot |}
    | RequirementGroundingMaterialObserved of
        {| SessionId: SessionId
           Observation: RequirementGroundingMaterialObserved |}
    | RequirementGroundingAnchored of
        {| SessionId: SessionId
           Occurrence: RequirementGroundingOccurrence |}
    | TipGuidanceDelivered of
        {| SessionId: SessionId
           TipName: string
           Presentation: TipPresentation |}
    | SessionStartedAtBound of
        {| SessionId: SessionId
           StartedAt: System.DateTimeOffset |}
