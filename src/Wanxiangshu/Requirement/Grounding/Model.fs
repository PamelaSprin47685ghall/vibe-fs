namespace Wanxiangshu.Requirement.Grounding

open Wanxiangshu.Host

type GroundingMaterial = { Path: string; ResultBytes: string }

type GroundingSnapshot =
    { Workspace: string
      PackageName: string
      Digest: string
      Materials: GroundingMaterial list }

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
