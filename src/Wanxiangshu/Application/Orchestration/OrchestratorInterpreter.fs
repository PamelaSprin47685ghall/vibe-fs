namespace Wanxiangshu.Orchestrator

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

/// Production interpreter for Domain.OrchestratorProgram (FLOW-003 / M2).
/// Owns effects only; business branches live in the Program data.
module OrchestratorInterpreter =

    type private PublishAttempt =
        | TargetMoved
        | Landed of CommitHash

    let private failed (job: ManagerJob) details =
        OrchestratorVerdict.IntegrationFailed(job.JobId, details)

    let private append (deps: OrchestratorProgramDeps) (job: ManagerJob) fact =
        match deps.AppendFact StreamId.Workspace fact with
        | Ok() -> Ok()
        | Error error -> Error(failed job error)

    let private claimAndFf (deps: OrchestratorProgramDeps) (job: ManagerJob) (expectedHead: CommitHash) =
        task {
            match! deps.Git.GetTargetHead job.TargetRef with
            | Error error -> return Error(failed job (sprintf "Git target head lookup failed: %s" error))
            | Ok current when current <> expectedHead -> return Ok TargetMoved
            | Ok current ->
                let claim =
                    AgentFact.PublishClaimed
                        {| ManagerJobId = job.JobId
                           TargetRef = job.TargetRef
                           ExpectedHead = current |}

                match append deps job claim with
                | Error verdict -> return Error verdict
                | Ok() ->
                    match! deps.Git.FfMerge job.Worktree.Path job.TargetRef current with
                    | Error error when error = OrchestratorConstants.targetRefMovedError -> return Ok TargetMoved
                    | Error error -> return Error(failed job (sprintf "FF merge failed: %s" error))
                    | Ok landed ->
                        let published =
                            AgentFact.Published
                                {| ManagerJobId = job.JobId
                                   CandidateCommit = landed
                                   ResultingTargetHead = landed |}

                        match append deps job published with
                        | Error verdict -> return Error verdict
                        | Ok() ->
                            do! deps.Manager.TerminateChildren job.JobId
                            return Ok(Landed landed)
        }

    let private publishUnderGate (deps: OrchestratorProgramDeps) (job: ManagerJob) (expectedHead: CommitHash) =
        task {
            let! gate = IntegrationGate.acquire deps.GatePath

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

    let private executeCommand
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (command: OrchestratorCommand)
        : Task<Result<OrchestratorReply, OrchestratorVerdict>> =
        task {
            match command with
            | AwaitManager jobId ->
                match! deps.Manager.AwaitManager jobId with
                | Ok() -> return Ok UnitOk
                | Error error -> return Error(failed job (sprintf "Manager run failed: %s" error))

            | OrchestratorCommand.ResumeManager(jobId, path, prompt) ->
                match! deps.Manager.ResumeManager jobId path prompt with
                | Ok() -> return Ok UnitOk
                | Error error -> return Error(failed job error)

            | ReadTargetHead targetRef ->
                match! deps.Git.GetTargetHead targetRef with
                | Ok head -> return Ok(Head head)
                | Error error -> return Error(failed job (sprintf "Git target head lookup failed: %s" error))

            | ReadWorktreeHead path ->
                match! deps.Git.ReadHead path with
                | Ok head -> return Ok(Head head)
                | Error error -> return Error(failed job (sprintf "Git head lookup failed: %s" error))

            | RebaseOnto(path, targetRef) ->
                match! deps.Git.Rebase path targetRef with
                | Ok() -> return Ok RebaseOk
                | Error _rebaseError ->
                    match! deps.Git.ConflictedFiles path with
                    | Error error -> return Error(failed job (sprintf "Conflict-file lookup failed: %s" error))
                    | Ok files ->
                        match! deps.Git.ReadHead path with
                        | Error error -> return Error(failed job (sprintf "Git head lookup failed: %s" error))
                        | Ok worktreeHead -> return Ok(RebaseConflict(files, worktreeHead))

            | ReviewRound(jobId, sessionId, path, barrierId, _, _) ->
                match! deps.Manager.Reverify jobId sessionId path barrierId with
                | Ok() -> return Ok ReviewOk
                | Error error -> return Error(OrchestratorVerdict.NeedsReview(job.JobId, error))

            | RecordCandidateReady(jobId, candidate, barrierId) ->
                match
                    append
                        deps
                        job
                        (AgentFact.CandidateReady
                            {| ManagerJobId = jobId
                               CandidateCommit = candidate
                               PreRebaseReviewBarrierId = barrierId |})
                with
                | Ok() -> return Ok UnitOk
                | Error verdict -> return Error verdict

            | RecordRebasedReady(jobId, rebased, target, barrierId) ->
                match
                    append
                        deps
                        job
                        (AgentFact.RebasedCandidateReady
                            {| ManagerJobId = jobId
                               RebasedCommit = rebased
                               TargetHeadSnapshot = target
                               PostRebaseReviewBarrierId = barrierId |})
                with
                | Ok() -> return Ok UnitOk
                | Error verdict -> return Error verdict

            | RecordConflict(jobId, candidate, target, files) ->
                match
                    append
                        deps
                        job
                        (AgentFact.ConflictDetected
                            {| ManagerJobId = jobId
                               CandidateCommit = candidate
                               TargetHeadSnapshot = target
                               ConflictFiles = files
                               DiagnosticsDigest = HostDigest.sha256Hex (String.Join("\n", files)) |})
                with
                | Ok() -> return Ok UnitOk
                | Error verdict -> return Error verdict

            | PublishUnderGate(_, expectedHead) ->
                match! publishUnderGate deps job expectedHead with
                | Ok TargetMoved -> return Ok PublishTargetMoved
                | Ok(Landed commit) -> return Ok(PublishLanded commit)
                | Error verdict -> return Error verdict

            | TerminateChildren jobId ->
                do! deps.Manager.TerminateChildren jobId
                return Ok UnitOk

            | ReleaseWorktree _ ->
                match! job.Worktree.Release() with
                | Ok() -> return Ok UnitOk
                | Error error -> return Error(failed job error)

            | AppendFact fact ->
                match append deps job fact with
                | Ok() -> return Ok UnitOk
                | Error verdict -> return Error verdict
        }

    let private mapReturn (job: ManagerJob) (value: Result<CommitHash, string> option) : OrchestratorVerdict =
        match value with
        | None -> OrchestratorVerdict.Empty
        | Some(Ok head) -> OrchestratorVerdict.Published(job.JobId, head)
        | Some(Error reason) -> failed job reason

    let interpret
        (deps: OrchestratorProgramDeps)
        (job: ManagerJob)
        (program: OrchestratorProgram)
        : Task<OrchestratorVerdict> =
        let rec go (current: OrchestratorProgram) : Task<OrchestratorVerdict> =
            task {
                match current with
                | Return value -> return mapReturn job value
                | Step(command, next) ->
                    match! executeCommand deps job command with
                    | Error verdict -> return verdict
                    | Ok reply -> return! go (next reply)
            }

        task {
            try
                return! go program
            with
            | :? OperationCanceledException -> return failed job "cancelled"
            | error -> return failed job (sprintf "%A" error)
        }

    /// ORCH-007: build the Domain program from durable progress, then interpret.
    let programFor (deps: OrchestratorProgramDeps) (job: ManagerJob) : Task<OrchestratorProgram> =
        task {
            let record =
                OrchestratorProjection.tryFind job.JobId (deps.Snapshot()).AgentProjections.Orchestrator

            match record with
            | None ->
                return OrchestratorPrograms.freshStart job.JobId job.ManagerSessionId job.Worktree.Path job.TargetRef
            | Some value ->
                match value.Progress with
                | JobProgress.ManagerStarted ->
                    return
                        OrchestratorPrograms.freshStart job.JobId job.ManagerSessionId job.Worktree.Path job.TargetRef
                | _ ->
                    let! head = deps.Git.GetTargetHead job.TargetRef

                    let currentHead =
                        match head with
                        | Ok commit -> Some commit
                        | Error _ -> None

                    let action = OrchestratorProjection.recoveryAction currentHead value

                    match action with
                    | ResumeManager ->
                        return
                            OrchestratorPrograms.freshStart
                                job.JobId
                                job.ManagerSessionId
                                job.Worktree.Path
                                job.TargetRef
                    | RebaseReviewPublish _
                    | RebaseAndReviewAgain ->
                        return
                            OrchestratorPrograms.resumeRebaseReviewPublish
                                job.JobId
                                job.ManagerSessionId
                                job.Worktree.Path
                                job.TargetRef
                    | ResumeConflictResolution conflict ->
                        return
                            OrchestratorPrograms.resumeConflictResolution
                                job.JobId
                                job.ManagerSessionId
                                job.Worktree.Path
                                job.TargetRef
                                conflict.ConflictFiles
                    | AttemptPublish claim ->
                        return
                            OrchestratorPrograms.resumeAttemptPublish
                                job.JobId
                                job.ManagerSessionId
                                job.Worktree.Path
                                job.TargetRef
                                claim.ExpectedHead
                    | BackfillPublished landed ->
                        return
                            OrchestratorPrograms.resumeBackfillPublished
                                job.JobId
                                job.Worktree.Path
                                landed.RebasedCommit
                                landed.ResultingTargetHead
                    | CleanUp -> return OrchestratorPrograms.resumeCleanUp job.Worktree.Path
                    | FailClosed reason -> return OrchestratorPrograms.resumeFailClosed reason
        }

    let run (deps: OrchestratorProgramDeps) (job: ManagerJob) : Task<OrchestratorVerdict> =
        task {
            let! program = programFor deps job
            return! interpret deps job program
        }
