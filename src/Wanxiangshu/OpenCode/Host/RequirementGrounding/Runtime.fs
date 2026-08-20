namespace Wanxiangshu.OpenCode.Host.RequirementGrounding

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode.Host
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Requirement.Grounding

type RequirementGroundingDecision =
    { NeedsGrounding: bool
      Requested: int
      Packages: string list }

module RequirementGroundingRuntime =

    let private stateFor (journal: AgentJournal) sessionId =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind _.RequirementGrounding
        |> Option.defaultValue RequirementGroundingProjection.empty

    let pending (journal: AgentJournal) sessionId =
        stateFor journal sessionId |> RequirementGroundingProjection.pending

    let occurrences (journal: AgentJournal) sessionId =
        stateFor journal sessionId |> RequirementGroundingProjection.visibleOccurrences

    let historyOccurrences (journal: AgentJournal) sessionId =
        stateFor journal sessionId |> RequirementGroundingProjection.occurrences

    let groundedKeys (journal: AgentJournal) sessionId =
        stateFor journal sessionId |> RequirementGroundingProjection.groundedKeys

    let nextOrdinal (journal: AgentJournal) sessionId =
        stateFor journal sessionId |> RequirementGroundingProjection.nextOrdinal

    let private appendRequest (journal: AgentJournal) sessionId snapshot =
        AgentJournal.appendAgent
            (StreamId.Session sessionId)
            None
            (HostFact.RequirementGroundingRequested
                {| SessionId = sessionId
                   Snapshot = snapshot |})
            journal

    let private appendMaterialObserved (journal: AgentJournal) sessionId observation =
        AgentJournal.appendAgent
            (StreamId.Session sessionId)
            None
            (HostFact.RequirementGroundingMaterialObserved
                {| SessionId = sessionId
                   Observation = observation |})
            journal

    let private requestOne journal sessionId snapshot =
        let current = stateFor journal sessionId

        if
            RequirementGroundingProjection.isSnapshotGrounded snapshot current
            || RequirementGroundingProjection.snapshotRequested snapshot current
        then
            Task.FromResult(Ok 0)
        else
            taskResult {
                let! _ =
                    appendRequest journal sessionId snapshot
                    |> TaskResult.mapError JournalAppendFailure.describe

                return 1
            }

    let private requestMissing journal sessionId snapshots =
        let rec loop remaining requested =
            match remaining with
            | [] -> Task.FromResult(Ok requested)
            | snapshot :: tail ->
                taskResult {
                    let! added = requestOne journal sessionId snapshot
                    return! loop tail (requested + added)
                }

        loop snapshots 0

    let requestPaths
        (journal: AgentJournal)
        workspace
        sessionId
        paths
        : Task<Result<RequirementGroundingDecision, string>> =
        task {
            let snapshots = GroundingCatalog.snapshotsForPaths workspace paths
            let before = stateFor journal sessionId

            let needsGrounding =
                snapshots
                |> List.exists (fun snapshot -> not (RequirementGroundingProjection.isSnapshotGrounded snapshot before))

            match! requestMissing journal sessionId snapshots with
            | Error error -> return Error error
            | Ok requested ->
                return
                    Ok
                        { NeedsGrounding = needsGrounding
                          Requested = requested
                          Packages = snapshots |> List.map _.PackageName }
        }

    let private observeOneMaterial journal sessionId snapshot material =
        let current = stateFor journal sessionId
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
                let! _ =
                    appendMaterialObserved journal sessionId observation
                    |> TaskResult.mapError JournalAppendFailure.describe

                return ()
            }

    let private observeMaterials journal sessionId materials =
        let rec loop remaining =
            match remaining with
            | [] -> Task.FromResult(Ok())
            | (snapshot, material) :: tail ->
                taskResult {
                    do! observeOneMaterial journal sessionId snapshot material
                    return! loop tail
                }

        loop materials

    let observeReadPaths
        (journal: AgentJournal)
        workspace
        sessionId
        paths
        : Task<Result<RequirementGroundingDecision, string>> =
        taskResult {
            let materials = GroundingCatalog.materialsForExactPaths workspace paths
            do! observeMaterials journal sessionId materials
            return! requestPaths journal workspace sessionId paths
        }

    let appendAnchored (journal: AgentJournal) sessionId occurrence =
        AgentJournal.appendAgent
            (StreamId.Session sessionId)
            None
            (HostFact.RequirementGroundingAnchored
                {| SessionId = sessionId
                   Occurrence = occurrence |})
            journal
