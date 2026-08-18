namespace Wanxiangshu.Requirement.Grounding

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
