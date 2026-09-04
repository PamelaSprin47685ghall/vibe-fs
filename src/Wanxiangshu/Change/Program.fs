namespace Wanxiangshu.Change

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Persistence.Journal

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

    let private mapTaskError mapper operation =
        mapTask (Result.mapError mapper) operation

    let private continueResult binder operation =
        task {
            let! outcome = operation

            match outcome with
            | Ok value -> return! binder value
            | Error verdict -> return verdict
        }

    let private continueUnit binder operation =
        continueResult (fun () -> binder ()) operation

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

    let private completeClaimAndFf (deps: OrchestratorProgramDeps) (job: ManagerJob) (current: CommitHash) =
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

    let private claimAndFf (deps: OrchestratorProgramDeps) (job: ManagerJob) (expectedHead: CommitHash) =
        taskResult {
            let! current = targetHead deps job

            if current <> expectedHead then
                return TargetMoved
            else
                return! completeClaimAndFf deps job current
        }

    let private publishUnderGate (deps: OrchestratorProgramDeps) (job: ManagerJob) (expectedHead: CommitHash) =
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

    let private settleLanded (deps: OrchestratorProgramDeps) (job: ManagerJob) (commit: CommitHash) =
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

    let private requestAfterBindingChange (deps: OrchestratorProgramDeps) (job: ManagerJob) reason =
        taskResult {
            do! invalidate deps job reason
            let! _ = successor deps job reason
            return ()
        }

    let private captureSnapshotResult (deps: OrchestratorProgramDeps) (job: ManagerJob) details =
        deps.Relay.CaptureSnapshot job.JobId
        |> mapTaskError (fun error -> failed job (sprintf "%s: %s" details error))

    let private conflictedFilesResult (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        deps.Git.ConflictedFiles job.Worktree.Path
        |> mapTaskError (fun error -> failed job (sprintf "Conflict-file lookup failed: %s" error))

    let private prepareCandidateResult (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        deps.Relay.PrepareCandidate job.JobId
        |> mapTaskError (fun error -> failed job (sprintf "Candidate admission failed: %s" error))

    let rec private runRoad (deps: OrchestratorProgramDeps) (job: ManagerJob) : Task<OrchestratorVerdict> =
        task {
            match! deps.Relay.AwaitRoadSignal job.JobId with
            | Error error -> return failed job (sprintf "Relay signal failed: %s" error)
            | Ok(RoadSignal.ExceptionalTerminal reason) -> return failed job reason
            | Ok(RoadSignal.IncumbencyRetired _) ->
                return!
                    successor deps job "IndependentAssessmentRequired"
                    |> continueResult (fun _ -> runRoad deps job)
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
        let afterRebaseSuccessor _ = runRoad deps job

        let afterRebasedRecord _ =
            successor deps job "PostRebaseIndependentAssessment"
            |> continueResult afterRebaseSuccessor

        let afterRebaseSnapshot snapshot =
            recordRebased deps job target snapshot |> continueResult afterRebasedRecord

        let onRebaseSuccess () =
            captureSnapshotResult deps job "Post-rebase snapshot failed"
            |> continueResult afterRebaseSnapshot

        let afterConflictSuccessor _ = runRoad deps job

        let afterConflictRecord _ =
            successor deps job "RebaseConflict" |> continueResult afterConflictSuccessor

        let afterConflictSnapshot files snapshot =
            recordConflict deps job candidate target snapshot files
            |> continueResult afterConflictRecord

        let handleConflictFiles rebaseError files =
            if List.isEmpty files then
                Task.FromResult(failed job (sprintf "Rebase failed without conflicts: %s" rebaseError))
            else
                captureSnapshotResult deps job "Conflict snapshot failed"
                |> continueResult (afterConflictSnapshot files)

        let onRebaseFailure rebaseError =
            conflictedFilesResult deps job
            |> continueResult (handleConflictFiles rebaseError)

        let afterInvalidation () =
            task {
                let! rebase = deps.Git.Rebase job.Worktree.Path job.TargetRef

                match rebase with
                | Ok() -> return! onRebaseSuccess ()
                | Error rebaseError -> return! onRebaseFailure rebaseError
            }

        invalidate deps job reason |> continueUnit afterInvalidation

    and private publishCertified
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (certificate: QualityCertificate)
        (candidate: CommitHash)
        (expectedHead: CommitHash)
        : Task<OrchestratorVerdict> =
        let afterTargetRefresh refreshed =
            handleRebase deps job certificate candidate refreshed "PublishCasMissed"

        let handlePublishAttempt =
            function
            | Landed commit -> settleLanded deps job commit
            | TargetMoved -> targetHead deps job |> continueResult afterTargetRefresh

        publishUnderGate deps job expectedHead |> continueResult handlePublishAttempt

    and private handleQualityCandidate
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (certificate: QualityCertificate)
        : Task<OrchestratorVerdict> =
        let resumeRoad () = runRoad deps job

        let bindingChanged () =
            requestAfterBindingChange deps job "WorkspaceChangedAfterAssessment"
            |> continueUnit resumeRoad

        let afterConflictBindingChange () = runRoad deps job

        let afterConflictRecord () =
            requestAfterBindingChange deps job "ArtifactAdmissionUnmerged"
            |> continueUnit afterConflictBindingChange

        let recordObservedConflict currentSnapshot files candidate target =
            recordConflict deps job candidate target currentSnapshot files
            |> continueUnit afterConflictRecord

        let afterConflictHead currentSnapshot files candidate =
            targetHead deps job
            |> continueResult (recordObservedConflict currentSnapshot files candidate)

        let handleObservedConflict currentSnapshot files =
            readHead deps job |> continueResult (afterConflictHead currentSnapshot files)

        let rebaseReason =
            function
            | Some _ -> "TargetAdvanced"
            | None -> "InitialRebaseRequired"

        let recordInitialCandidate candidate target rebased =
            let afterCandidateRecord () =
                handleRebase deps job certificate candidate target (rebaseReason rebased)

            recordCandidate deps job candidate certificate
            |> continueUnit afterCandidateRecord

        let admitPreparedCandidate candidate target =
            let rebased =
                currentRecord deps job
                |> Option.bind (fun record -> record.RebasedCandidateReady)

            match rebased with
            | Some admitted when admitted.RebasedCommit = candidate && admitted.TargetHeadSnapshot = target ->
                publishCertified deps job certificate candidate target
            | _ -> recordInitialCandidate candidate target rebased

        let onPreparedCandidate candidate =
            targetHead deps job |> continueResult (admitPreparedCandidate candidate)

        let handleConflictFiles currentSnapshot files =
            match files with
            | [] -> prepareCandidateResult deps job |> continueResult onPreparedCandidate
            | _ -> handleObservedConflict currentSnapshot files

        let onPrepareFailure currentSnapshot _ =
            conflictedFilesResult deps job
            |> continueResult (handleConflictFiles currentSnapshot)

        let inspectCurrentArtifact currentSnapshot =
            task {
                let! outcome = prepareCandidateResult deps job

                match outcome with
                | Ok candidate -> return! onPreparedCandidate candidate
                | Error error -> return! onPrepareFailure currentSnapshot error
            }

        let handleSnapshotAdmission (matches, currentSnapshot) =
            if matches then
                inspectCurrentArtifact currentSnapshot
            else
                bindingChanged ()

        artifactSnapshotMatches deps job certificate
        |> continueResult handleSnapshotAdmission

    let private resumePublishReady (deps: OrchestratorProgramDeps) (job: ManagerJob) (expectedHead: CommitHash) =
        task {
            match! publishUnderGate deps job expectedHead with
            | Error verdict -> return verdict
            | Ok(Landed commit) -> return! settleLanded deps job commit
            | Ok TargetMoved -> return! runRoad deps job
        }

    let private resumePublishReality
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (rebasedCommit: CommitHash)
        (expectedHead: CommitHash)
        (current: CommitHash)
        (reality: PublishClaimReality)
        =
        match reality with
        | PublishClaimReality.HeadUnreadable ->
            Task.FromResult(failed job "GetTargetHead failed during publish recovery")
        | PublishClaimReality.AlreadyFastForwarded -> backfillPublished deps job rebasedCommit current
        | PublishClaimReality.PublishReady -> resumePublishReady deps job expectedHead
        | PublishClaimReality.ClaimExpired -> runRoad deps job

    let private reenterPublishClaim
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (claim:
            {| RebasedCommit: CommitHash
               ExpectedHead: CommitHash |})
        =
        task {
            let! headResult = deps.Git.GetTargetHead job.TargetRef

            match headResult with
            | Error _ -> return failed job "GetTargetHead failed; refusing publish recovery"
            | Ok current ->
                let reality =
                    OrchestratorProjection.classifyPublishClaim (Some current) claim.RebasedCommit claim.ExpectedHead

                return! resumePublishReality deps job claim.RebasedCommit claim.ExpectedHead current reality
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
