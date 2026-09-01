namespace Wanxiangshu.Host

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Concern
open Wanxiangshu.Requirement.Grounding

module HostFact =
    val inline PairProgrammingGuidelineAnchored:
        payload:
            {| SessionId: SessionId
               Ordinal: int64
               CallId: ToolCallId
               MarkerText: string
               CallGap: TranscriptGap
               ResultGap: TranscriptGap
               ConcernPlacement: ConcernPlacementBatch option |} ->
            AgentFact

    val inline RequirementGroundingRequested:
        payload:
            {| SessionId: SessionId
               Snapshot: GroundingSnapshot |} ->
            AgentFact

    val inline RequirementGroundingMaterialObserved:
        payload:
            {| SessionId: SessionId
               Observation: RequirementGroundingMaterialObserved |} ->
            AgentFact

    val inline RequirementGroundingAnchored:
        payload:
            {| SessionId: SessionId
               Occurrence: RequirementGroundingOccurrence |} ->
            AgentFact

    val inline TipGuidanceDelivered:
        payload:
            {| SessionId: SessionId
               TipName: string
               Presentation: TipPresentation |} ->
            AgentFact

    val inline SessionStartedAtBound:
        payload:
            {| SessionId: SessionId
               StartedAt: System.DateTimeOffset |} ->
            AgentFact
