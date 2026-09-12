namespace Wanxiangshu.OpenCode.Host.RequirementGrounding

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Requirement.Grounding

type RequirementGroundingDecision =
    { NeedsGrounding: bool
      Requested: int
      Packages: string list }

module RequirementGroundingRuntime =

    let private stateFor (port: RequirementGroundingPort) sessionId = port.ReadState sessionId

    let pending port sessionId =
        stateFor port sessionId |> RequirementGroundingProjection.pending

    let occurrences port sessionId =
        stateFor port sessionId |> RequirementGroundingProjection.visibleOccurrences

    let historyOccurrences port sessionId =
        stateFor port sessionId |> RequirementGroundingProjection.occurrences

    let groundedKeys port sessionId =
        stateFor port sessionId |> RequirementGroundingProjection.groundedKeys

    let nextOrdinal port sessionId =
        stateFor port sessionId |> RequirementGroundingProjection.nextOrdinal

    let private appendRequest (port: RequirementGroundingPort) sessionId snapshot =
        port.AppendRequested sessionId snapshot

    let private appendMaterialObserved (port: RequirementGroundingPort) sessionId observation =
        port.AppendMaterialObserved sessionId observation

    let private requestOne port sessionId snapshot =
        let current = stateFor port sessionId

        if
            RequirementGroundingProjection.isSnapshotGrounded snapshot current
            || RequirementGroundingProjection.snapshotRequested snapshot current
        then
            Task.FromResult(Ok 0)
        else
            taskResult {
                let! _ = appendRequest port sessionId snapshot
                return 1
            }

    let private requestMissing port sessionId snapshots =
        let rec loop remaining requested =
            match remaining with
            | [] -> Task.FromResult(Ok requested)
            | snapshot :: tail ->
                taskResult {
                    let! added = requestOne port sessionId snapshot
                    return! loop tail (requested + added)
                }

        loop snapshots 0

    let requestPaths
        (port: RequirementGroundingPort)
        workspace
        sessionId
        paths
        : Task<Result<RequirementGroundingDecision, string>> =
        task {
            let snapshots = GroundingCatalog.snapshotsForPaths workspace paths
            let before = stateFor port sessionId

            let needsGrounding =
                snapshots
                |> List.exists (fun snapshot -> not (RequirementGroundingProjection.isSnapshotGrounded snapshot before))

            match! requestMissing port sessionId snapshots with
            | Error error -> return Error error
            | Ok requested ->
                return
                    Ok
                        { NeedsGrounding = needsGrounding
                          Requested = requested
                          Packages = snapshots |> List.map _.PackageName }
        }

    let private observeOneMaterial port sessionId snapshot material =
        let current = stateFor port sessionId
        let key = GroundingIdentity.snapshotMaterialKey snapshot material

        if Set.contains key current.VisibleMaterials then
            Task.FromResult(Ok())
        else
            let observation =
                { Workspace = snapshot.Workspace
                  PackageName = snapshot.PackageName
                  Path = material.Path
                  Digest = GroundingIdentity.materialDigest material.Path material.ResultBytes }

            taskResult {
                do! appendMaterialObserved port sessionId observation
                return ()
            }

    let private observeMaterials port sessionId materials =
        let rec loop remaining =
            match remaining with
            | [] -> Task.FromResult(Ok())
            | (snapshot, material) :: tail ->
                taskResult {
                    do! observeOneMaterial port sessionId snapshot material
                    return! loop tail
                }

        loop materials

    let observeReadPaths
        (port: RequirementGroundingPort)
        workspace
        sessionId
        paths
        : Task<Result<RequirementGroundingDecision, string>> =
        taskResult {
            let materials = GroundingCatalog.materialsForExactPaths workspace paths
            do! observeMaterials port sessionId materials
            return! requestPaths port workspace sessionId paths
        }

    let appendAnchored (port: RequirementGroundingPort) sessionId occurrence =
        port.AppendAnchored sessionId occurrence
