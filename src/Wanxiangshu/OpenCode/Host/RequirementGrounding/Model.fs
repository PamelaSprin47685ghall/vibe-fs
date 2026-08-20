namespace Wanxiangshu.OpenCode.Host.RequirementGrounding

open Wanxiangshu.Foundation.Identity

type RequirementGroundingAnchoredRead =
    { CallId: ToolCallId
      Path: string
      ArgsJson: string
      ResultBytes: string
      CursorResultBytes: string }

type RequirementGroundingMaterialObserved =
    { Workspace: string
      PackageName: string
      Path: string
      Digest: string }

type RequirementGroundingOccurrence =
    { Workspace: string
      PackageName: string
      Digest: string
      Ordinal: int64
      Reads: RequirementGroundingAnchoredRead list
      CallGap: TranscriptGap
      ResultGap: TranscriptGap }
