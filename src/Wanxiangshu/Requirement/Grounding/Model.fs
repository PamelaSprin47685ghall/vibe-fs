namespace Wanxiangshu.Requirement.Grounding

open Wanxiangshu.Host
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

type RequirementGroundingOccurrence =
    { Workspace: string
      PackageName: string
      Digest: string
      Ordinal: int64
      Reads: RequirementGroundingAnchoredRead list
      CallGap: TranscriptGap
      ResultGap: TranscriptGap }

module GroundingIdentity =

    let key workspace packageName digest =
        workspace + "\u0000" + packageName + "\u0000" + digest

    let snapshotKey (snapshot: GroundingSnapshot) =
        key snapshot.Workspace snapshot.PackageName snapshot.Digest

    let materialDigest path resultBytes =
        HostDigest.sha256Hex (path + "\u0000" + resultBytes)

    let materialVersionKey workspace packageName path digest =
        workspace + "\u0000" + packageName + "\u0000" + path + "\u0000" + digest

    let snapshotMaterialKey (snapshot: GroundingSnapshot) (material: GroundingMaterial) =
        materialVersionKey
            snapshot.Workspace
            snapshot.PackageName
            material.Path
            (materialDigest material.Path material.ResultBytes)
