namespace Wanxiangshu.Requirement.Grounding

open Wanxiangshu.Foundation.Identity

type GroundingMaterial = { Path: string; ResultBytes: string }

type GroundingSnapshot =
    { Workspace: string
      PackageName: string
      Digest: string
      Materials: GroundingMaterial list }

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

[<RequireQualifiedAccess>]
type GroundingReadCoverage =
    | CompleteFile
    | PartialFile

type GroundingFileRead =
    { Path: string
      ResultBytes: string
      Coverage: GroundingReadCoverage }

type RequirementGroundingReadObserved =
    { Workspace: string
      Path: string
      Digest: string
      Coverage: GroundingReadCoverage }

type RequirementGroundingOccurrence =
    { Workspace: string
      PackageName: string
      Digest: string
      Ordinal: int64
      Reads: RequirementGroundingAnchoredRead list
      CallGap: TranscriptGap
      ResultGap: TranscriptGap }

module GroundingIdentity =
    val key: workspace: string -> packageName: string -> digest: string -> string
    val snapshotKey: snapshot: GroundingSnapshot -> string
    val materialDigest: path: string -> resultBytes: string -> string
    val materialVersionKey: workspace: string -> packageName: string -> path: string -> digest: string -> string
    val snapshotMaterialKey: snapshot: GroundingSnapshot -> material: GroundingMaterial -> string
