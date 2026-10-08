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

    let private appendUnseenRead (port: RequirementGroundingPort) sessionId observation =
        let current = stateFor port sessionId

        if Set.contains observation current.ObservedReads then
            Task.FromResult(Ok())
        else
            port.AppendReadObserved sessionId observation

    let private observeOneRead (port: RequirementGroundingPort) workspace sessionId (read: GroundingFileRead) =
        match GroundingCatalog.workspaceRelativePath workspace read.Path with
        | None -> Task.FromResult(Ok())
        | Some path ->
            let observation =
                { Workspace = GroundingCatalog.canonicalWorkspace workspace
                  Path = path
                  Digest = GroundingIdentity.materialDigest path read.ResultBytes
                  Coverage = read.Coverage }

            appendUnseenRead port sessionId observation

    let private observeReads port workspace sessionId reads =
        let rec loop remaining =
            match remaining with
            | [] -> Task.FromResult(Ok())
            | read :: tail ->
                taskResult {
                    do! observeOneRead port workspace sessionId read
                    return! loop tail
                }

        loop reads

    let observeFileReads
        (port: RequirementGroundingPort)
        workspace
        sessionId
        (reads: GroundingFileRead list)
        : Task<Result<RequirementGroundingDecision, string>> =
        taskResult {
            do! observeReads port workspace sessionId reads
            return! requestPaths port workspace sessionId (reads |> List.map _.Path)
        }

    let appendAnchored (port: RequirementGroundingPort) sessionId occurrence =
        port.AppendAnchored sessionId occurrence
