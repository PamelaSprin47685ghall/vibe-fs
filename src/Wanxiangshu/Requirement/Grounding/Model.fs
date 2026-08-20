namespace Wanxiangshu.Requirement.Grounding

type GroundingMaterial =
    { Path: string
      Digest: string
      ResultBytes: string }

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

    let materialKey workspace packageName path digest =
        workspace + "\u0000" + packageName + "\u0000" + path + "\u0000" + digest

    let snapshotMaterialKey (snapshot: GroundingSnapshot) (material: GroundingMaterial) =
        materialKey snapshot.Workspace snapshot.PackageName material.Path material.Digest
