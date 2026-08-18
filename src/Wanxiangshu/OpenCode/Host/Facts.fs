namespace Wanxiangshu.OpenCode.Host

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Requirement.Grounding
open Wanxiangshu.OpenCode.Host.RequirementGrounding

/// Rulebook Main tip presentation for auto-injected guidance.
[<RequireQualifiedAccess>]
type TipPresentation =
    | Full
    | IdentityOnly

/// Durable Host transcript facts owned by the Host boundary.
type HostFactCases =
    | PairProgrammingGuidelineAnchored of
        {| SessionId: SessionId
           Ordinal: int64
           CallId: ToolCallId
           MarkerText: string
           CallGap: TranscriptGap
           ResultGap: TranscriptGap |}
    | RequirementGroundingRequested of
        {| SessionId: SessionId
           Snapshot: GroundingSnapshot |}
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
