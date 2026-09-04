namespace Wanxiangshu.Change

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Mission.Relay

/// One Road owns one stable worktree. Human quality decisions come only from
/// Relay incumbencies; Change owns deterministic Git admission, rebase and CAS.
module OrchestratorProgram =

    type private PublishAttempt =
        | TargetMoved
        | Landed of CommitHash

    let private failed (job: ManagerJob) details =
        OrchestratorVerdict.IntegrationFailed(job.JobId, details)

    let private mapTask (mapper: 'a -> 'b) (operation: Task<'a>) : Task<'b> =
        task {
            let! value = operation
            return mapper value
        }

    let private mapTaskError mapper operation = mapTask (Result.mapError mapper) operation

    let private append (deps: OrchestratorProgramDeps) (job: ManagerJob) fact =
        taskResult { do! deps.AppendFact StreamId.Workspace fact |> mapTaskError (failed job) }

    let private readHead (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        deps.Git.ReadHead job.Worktree.Path
        |> mapTaskError (fun error -> failed job (sprintf "Git head lookup failed: %s" error))

    let private targetHead (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        deps.Git.GetTargetHead job.TargetRef
        |> mapTaskError (fun error -> failed job (sprintf "Git target head lookup failed: %s" error))

    let private invalidate (deps: OrchestratorProgramDeps) (job: ManagerJob) reason =
        deps.Relay.InvalidateCertificate job.JobId reason
        |> mapTaskError (fun error -> failed job (sprintf "Certificate invalidation failed: %s" error))

    let private successor (deps: OrchestratorProgramDeps) (job: ManagerJob) reason =
        deps.Relay.RequestSuccessor job.JobId job.Worktree.Path reason
        |> mapTaskError (fun error -> failed job (sprintf "Successor activation failed: %s" error))

    let private recordCandidate
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (candidate: CommitHash)
        (certificate: QualityCertificate)
        =
        append
            deps
            job
            (OrchestratorFact.CandidateReady
                {| ManagerJobId = job.JobId
                   CandidateCommit = candidate
                   WorkspaceSnapshotId = certificate.SnapshotId
                   QualityCertificateId = certificate.Id |})

    let private recordRebased
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (target: CommitHash)
        (snapshot: WorkspaceSnapshotId)
        =
        taskResult {
            let! rebased = readHead deps job

            do!
                append
                    deps
                    job
                    (OrchestratorFact.RebasedCandidateReady
                        {| ManagerJobId = job.JobId
                           RebasedCommit = rebased
                           TargetHeadSnapshot = target
                           WorkspaceSnapshotId = snapshot |})

            return rebased
        }

    let private recordConflict
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (candidate: CommitHash)
        (target: CommitHash)
        (snapshot: WorkspaceSnapshotId)
        (files: string list)
        =
        append
            deps
            job
            (OrchestratorFact.ConflictDetected
                {| ManagerJobId = job.JobId
                   CandidateCommit = candidate
                   TargetHeadSnapshot = target
                   WorkspaceSnapshotId = snapshot
                   ConflictFiles = files
                   DiagnosticsDigest = HostDigest.sha256Hex (String.Join("\n", files)) |})

    let private completeClaimAndFf
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (current: CommitHash)
        =
        taskResult {
            do!
                append
                    deps
                    job
                    (OrchestratorFact.PublishClaimed
                        {| ManagerJobId = job.JobId
                           TargetRef = job.TargetRef
                           ExpectedHead = current |})

            let! merge = deps.Git.FfMerge job.Worktree.Path job.TargetRef current |> TaskResultCE.ofTask

            match merge with
            | Error error when error = OrchestratorConstants.targetRefMovedError -> return TargetMoved
            | Error error -> return! Error(failed job (sprintf "FF merge failed: %s" error))
            | Ok landed ->
                do!
                    append
                        deps
                        job
                        (OrchestratorFact.Published
                            {| ManagerJobId = job.JobId
                               CandidateCommit = landed
                               ResultingTargetHead = landed |})

                do! deps.Relay.TerminateRoadResources job.JobId |> TaskResultCE.ofTask
                return Landed landed
        }

    let private claimAndFf
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (expectedHead: CommitHash)
        =
        taskResult {
            let! current = targetHead deps job

            if current <> expectedHead then
                return TargetMoved
            else
                return! completeClaimAndFf deps job current
        }

    let private publishUnderGate
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (expectedHead: CommitHash)
        =
        task {
            let! gate = deps.AcquirePublishGate()

            let! outcome =
                task {
                    try
                        return! claimAndFf deps job expectedHead
                    with error ->
                        return Error(failed job (sprintf "Publish window failed: %s" error.Message))
                }

            do! gate.Release()
            return outcome
        }

    let private releaseTerminalWorktree (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            do! deps.Relay.TerminateRoadResources job.JobId
            return! job.Worktree.Release()
        }

    let private settleLanded
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (commit: CommitHash)
        =
        task {
            match! releaseTerminalWorktree deps job with
            | Ok() -> return OrchestratorVerdict.Published(job.JobId, commit)
            | Error error ->
                return failed job (sprintf "Published %s but cleanup failed: %s" (CommitHash.value commit) error)
        }

    let private backfillPublished
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (candidate: CommitHash)
        (resultingHead: CommitHash)
        =
        taskResult {
            do!
                append
                    deps
                    job
                    (OrchestratorFact.Published
                        {| ManagerJobId = job.JobId
                           CandidateCommit = candidate
                           ResultingTargetHead = resultingHead |})

            do!
                releaseTerminalWorktree deps job
                |> mapTaskError (fun error -> failed job (sprintf "Published cleanup failed: %s" error))

            return OrchestratorVerdict.Published(job.JobId, resultingHead)
        }
        |> mapTask (function
            | Ok verdict -> verdict
            | Error verdict -> verdict)

    let private currentRecord (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        OrchestratorProjection.tryFind job.JobId (deps.Snapshot()).AgentProjections.Orchestrator

    let private artifactSnapshotMatches
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (certificate: QualityCertificate)
        =
        taskResult {
            let! snapshot =
                deps.Relay.CaptureSnapshot job.JobId
                |> mapTaskError (fun error -> failed job (sprintf "Workspace snapshot failed: %s" error))

            return snapshot = certificate.SnapshotId, snapshot
        }

    let private requestAfterBindingChange
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        reason
        =
        taskResult {
            do! invalidate deps job reason
            let! _ = successor deps job reason
            return ()
        }

    let rec private runRoad (deps: OrchestratorProgramDeps) (job: ManagerJob) : Task<OrchestratorVerdict> =
        task {
            match! deps.Relay.AwaitRoadSignal job.JobId with
            | Error error -> return failed job (sprintf "Relay signal failed: %s" error)
            | Ok(RoadSignal.ExceptionalTerminal reason) -> return failed job reason
            | Ok(RoadSignal.IncumbencyRetired _) ->
                match! successor deps job "IndependentAssessmentRequired" with
                | Error verdict -> return verdict
                | Ok _ -> return! runRoad deps job
            | Ok(RoadSignal.QualityCandidateAccepted(_, certificate)) ->
                return! handleQualityCandidate deps job certificate
        }

    and private handleRebase
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (certificate: QualityCertificate)
        (candidate: CommitHash)
        (target: CommitHash)
        reason
        : Task<OrchestratorVerdict> =
        task {
            match! invalidate deps job reason with
            | Error verdict -> return verdict
            | Ok() ->
                match! deps.Git.Rebase job.Worktree.Path job.TargetRef with
                | Ok() ->
                    match! deps.Relay.CaptureSnapshot job.JobId with
                    | Error error -> return failed job (sprintf "Post-rebase snapshot failed: %s" error)
                    | Ok snapshot ->
                        match! recordRebased deps job target snapshot with
                        | Error verdict -> return verdict
                        | Ok _ ->
                            match! successor deps job "PostRebaseIndependentAssessment" with
                            | Error verdict -> return verdict
                            | Ok _ -> return! runRoad deps job
                | Error rebaseError ->
                    match! deps.Git.ConflictedFiles job.Worktree.Path with
                    | Error error -> return failed job (sprintf "Conflict-file lookup failed: %s" error)
                    | Ok [] -> return failed job (sprintf "Rebase failed without conflicts: %s" rebaseError)
                    | Ok files ->
                        match! deps.Relay.CaptureSnapshot job.JobId with
                        | Error error -> return failed job (sprintf "Conflict snapshot failed: %s" error)
                        | Ok snapshot ->
                            match! recordConflict deps job candidate target snapshot files with
                            | Error verdict -> return verdict
                            | Ok() ->
                                match! successor deps job "RebaseConflict" with
                                | Error verdict -> return verdict
                                | Ok _ -> return! runRoad deps job
        }

    and private publishCertified
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (certificate: QualityCertificate)
        (candidate: CommitHash)
        (expectedHead: CommitHash)
        : Task<OrchestratorVerdict> =
        task {
            match! publishUnderGate deps job expectedHead with
            | Error verdict -> return verdict
            | Ok(Landed commit) -> return! settleLanded deps job commit
            | Ok TargetMoved ->
                match! targetHead deps job with
                | Error verdict -> return verdict
                | Ok refreshed ->
                    return! handleRebase deps job certificate candidate refreshed "PublishCasMissed"
        }

    and private handleQualityCandidate
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (certificate: QualityCertificate)
        : Task<OrchestratorVerdict> =
        task {
            match! artifactSnapshotMatches deps job certificate with
            | Error verdict -> return verdict
            | Ok(false, _) ->
                match! requestAfterBindingChange deps job "WorkspaceChangedAfterAssessment" with
                | Error verdict -> return verdict
                | Ok() -> return! runRoad deps job
            | Ok(true, currentSnapshot) ->
                match! deps.Git.ConflictedFiles job.Worktree.Path with
                | Error error -> return failed job (sprintf "Conflict-file lookup failed: %s" error)
                | Ok(_ :: _ as files) ->
                    match! readHead deps job, targetHead deps job with
                    | Error verdict, _
                    | _, Error verdict -> return verdict
                    | Ok candidate, Ok target ->
                        match! recordConflict deps job candidate target currentSnapshot files with
                        | Error verdict -> return verdict
                        | Ok() ->
                            match! requestAfterBindingChange deps job "ArtifactAdmissionUnmerged" with
                            | Error verdict -> return verdict
                            | Ok() -> return! runRoad deps job
                | Ok [] ->
                    match! deps.Relay.PrepareCandidate job.JobId with
                    | Error error -> return failed job (sprintf "Candidate admission failed: %s" error)
                    | Ok candidate ->
                        match! targetHead deps job with
                        | Error verdict -> return verdict
                        | Ok target ->
                            let rebased = currentRecord deps job |> Option.bind (fun record -> record.RebasedCandidateReady)

                            match rebased with
                            | Some admitted
                                when admitted.RebasedCommit = candidate
                                     && admitted.TargetHeadSnapshot = target ->
                                return! publishCertified deps job certificate candidate target
                            | _ ->
                                match! recordCandidate deps job candidate certificate with
                                | Error verdict -> return verdict
                                | Ok() ->
                                    let reason =
                                        match rebased with
                                        | Some _ -> "TargetAdvanced"
                                        | None -> "InitialRebaseRequired"

                                    return! handleRebase deps job certificate candidate target reason
        }

    let private reenterPublishClaim
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (claim:
            {| RebasedCommit: CommitHash
               ExpectedHead: CommitHash |})
        =
        task {
            match! deps.Git.GetTargetHead job.TargetRef with
            | Error _ -> return failed job "GetTargetHead failed; refusing publish recovery"
            | Ok current ->
                match OrchestratorProjection.classifyPublishClaim (Some current) claim.RebasedCommit claim.ExpectedHead with
                | PublishClaimReality.HeadUnreadable -> return failed job "GetTargetHead failed during publish recovery"
                | PublishClaimReality.AlreadyFastForwarded ->
                    return! backfillPublished deps job claim.RebasedCommit current
                | PublishClaimReality.PublishReady ->
                    match! publishUnderGate deps job claim.ExpectedHead with
                    | Error verdict -> return verdict
                    | Ok(Landed commit) -> return! settleLanded deps job commit
                    | Ok TargetMoved -> return! runRoad deps job
                | PublishClaimReality.ClaimExpired -> return! runRoad deps job
        }

    let private cleanUp (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            match! releaseTerminalWorktree deps job with
            | Ok() -> return OrchestratorVerdict.Empty
            | Error error -> return failed job (sprintf "Terminal job cleanup failed: %s" error)
        }

    let private program (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        match currentRecord deps job with
        | Some { Terminal = Some _ } -> cleanUp deps job
        | Some { PublishClaimed = Some claim } -> reenterPublishClaim deps job claim
        | _ -> runRoad deps job

    let run (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            try
                return! program deps job
            with
            | :? OperationCanceledException -> return failed job "cancelled"
            | error -> return failed job (sprintf "%A" error)
        }
