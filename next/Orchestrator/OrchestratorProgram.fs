namespace Wanxiangshu.Next.Orchestrator

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Flow

/// Canonical worktree → review → rebase → fresh review → ff-only program.
module OrchestratorProgram =

    type private PublishPass = Redrive | PublishedCommit of string

    let private fromTask action =
        Flow.create (fun context cancellation ->
            task {
                try
                    let! value = action context cancellation
                    return Ok value
                with
                | :? OperationCanceledException when cancellation.IsCancellationRequested ->
                    return Error(OrchestratorError.PublishFailed "cancelled")
                | error -> return Error(OrchestratorError.PublishFailed error.Message)
            })

    let private failed managerId details = OrchestratorVerdict.IntegrationFailed(managerId, details)

    let private append deps managerId fact =
        match deps.AppendFact StreamId.Workspace fact with
        | Ok() -> Ok()
        | Error error -> Error(failed managerId error)

    let private currentJob (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        OrchestratorRecovery.currentJob (deps.Snapshot()) job.ManagerId job.Worktree.Path job.Prompt

    let private readHead (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            match! deps.Git.ReadHead job.Worktree.Path with
            | Ok head -> return Ok head
            | Error error -> return Error(failed job.ManagerId (sprintf "Git head lookup failed: %s" error))
        }

    let private review deps job barrier factOfHead alreadyReviewed =
        task {
            match! readHead deps job with
            | Error verdict -> return Error verdict
            | Ok head when alreadyReviewed (currentJob deps job) head -> return Ok()
            | Ok head ->
                match! deps.Manager.Reverify job.ManagerId job.Worktree.Path barrier with
                | Error error -> return Error(OrchestratorVerdict.NeedsReview(job.ManagerId, error))
                | Ok() -> return append deps job.ManagerId (factOfHead (currentJob deps job) head)
        }

    let private preReview (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        review
            deps
            job
            "pre-rebase"
            (fun current head ->
                AgentFact.OrchestratorPreRebaseReviewConfirmed
                    {| ManagerId = job.ManagerId
                       CandidateId = OrchestratorRecovery.candidateId job.ManagerId current
                       CommitHash = head |})
            (fun current head -> current.PreRebaseReviewCommit = Some head)

    let private postReview (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        review
            deps
            job
            "post-rebase"
            (fun current head ->
                AgentFact.OrchestratorPostRebaseReviewConfirmed
                    {| ManagerId = job.ManagerId
                       CandidateId = OrchestratorRecovery.candidateId job.ManagerId current
                       RebasedCommit = head |})
            (fun current head -> current.PostRebaseReviewCommit = Some head)

    let private recordCandidate (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            let current = currentJob deps job

            match current.CandidateCommit with
            | Some _ -> return Ok()
            | None ->
                match! readHead deps job with
                | Error verdict -> return Error verdict
                | Ok head ->
                    return
                        append
                            deps
                            job.ManagerId
                            (AgentFact.OrchestratorCandidateRegistered
                                {| ManagerId = job.ManagerId
                                   CandidateId = OrchestratorRecovery.candidateId job.ManagerId current
                                   Branch = job.Worktree.Branch
                                   CommitHash = head |})
        }

    let private recordRebased (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            match! readHead deps job with
            | Error verdict -> return Error verdict
            | Ok head ->
                let current = currentJob deps job

                return
                    append
                        deps
                        job.ManagerId
                        (AgentFact.OrchestratorRebased
                            {| ManagerId = job.ManagerId
                               CandidateId = OrchestratorRecovery.candidateId job.ManagerId current
                               RebasedCommit = head |})
        }

    let private rebase (deps: OrchestratorProgramDeps) (job: ManagerJob) redrive =
        task {
            let current = currentJob deps job
            let! headResult = readHead deps job

            match headResult with
            | Error verdict -> return Error verdict
            | Ok head ->
                let! hasRebase = deps.Git.HasRebaseHead job.Worktree.Path

                if not redrive && current.RebasedCommit = Some head && not hasRebase then
                    return Ok()
                else
                    match! deps.Git.Rebase job.Worktree.Path deps.TargetBranch with
                    | Ok() -> return! recordRebased deps job
                    | Error rebaseError ->
                        match! deps.Git.ConflictedFiles job.Worktree.Path with
                        | Error error ->
                            return Error(failed job.ManagerId (sprintf "Conflict-file lookup failed: %s" error))
                        | Ok files ->
                            let conflict =
                                AgentFact.OrchestratorConflictDetected
                                    {| ManagerId = job.ManagerId
                                       CandidateId = OrchestratorRecovery.candidateId job.ManagerId current
                                       Files = files |}

                            match append deps job.ManagerId conflict with
                            | Error verdict -> return Error verdict
                            | Ok() ->
                                let prompt = OrchestratorPrompts.buildConflictResumePrompt job.Prompt files

                                match! deps.Manager.RunManager job.ManagerId job.Worktree.Path prompt with
                                | Error error ->
                                    return
                                        Error(
                                            failed
                                                job.ManagerId
                                                (sprintf
                                                    "Rebase conflict (%s); manager continuation failed: %s"
                                                    rebaseError
                                                    error)
                                        )
                                | Ok() ->
                                    match! deps.Git.Rebase job.Worktree.Path deps.TargetBranch with
                                    | Error error ->
                                        return Error(failed job.ManagerId (sprintf "Rebase continuation failed: %s" error))
                                    | Ok() -> return! recordRebased deps job
        }

    let private publish (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            match! deps.Git.GetTargetHead deps.TargetBranch with
            | Error error -> return Error(failed job.ManagerId (sprintf "Git target head lookup failed: %s" error))
            | Ok targetHead ->
                let expected = if String.IsNullOrWhiteSpace targetHead then None else Some targetHead
                let current = currentJob deps job

                let claimed =
                    match expected with
                    | Some head when current.PublishClaimHead <> Some head ->
                        append
                            deps
                            job.ManagerId
                            (AgentFact.OrchestratorPublishClaimed
                                {| ManagerId = job.ManagerId
                                   CandidateId = OrchestratorRecovery.candidateId job.ManagerId current
                                   ExpectedTargetHead = head |})
                    | _ -> Ok()

                match claimed with
                | Error verdict -> return Error verdict
                | Ok() ->
                    match! deps.Git.FfMerge job.Worktree.Path deps.TargetBranch expected with
                    | Error error when error = OrchestratorConstants.targetRefMovedError -> return Ok Redrive
                    | Error error -> return Error(failed job.ManagerId (sprintf "FF merge failed: %s" error))
                    | Ok commit ->
                        let latest = currentJob deps job

                        match
                            append
                                deps
                                job.ManagerId
                                (AgentFact.OrchestratorPublished
                                    {| ManagerId = job.ManagerId
                                       CandidateId = OrchestratorRecovery.candidateId job.ManagerId latest
                                       CommitHash = commit |})
                        with
                        | Error verdict -> return Error verdict
                        | Ok() ->
                            match! job.Worktree.Release() with
                            | Ok() -> return Ok(PublishedCommit commit)
                            | Error error ->
                                return Error(failed job.ManagerId (sprintf "Published %s but cleanup failed: %s" commit error))
        }

    let rec private publishLoop (deps: OrchestratorProgramDeps) (job: ManagerJob) redrive =
        task {
            match! rebase deps job redrive with
            | Error verdict -> return verdict
            | Ok() ->
                match! postReview deps job with
                | Error verdict -> return verdict
                | Ok() ->
                    match! publish deps job with
                    | Error verdict -> return verdict
                    | Ok Redrive -> return! publishLoop deps job true
                    | Ok(PublishedCommit commit) -> return OrchestratorVerdict.Published(job.ManagerId, commit)
        }

    let private program (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        orchestrator {
            use! _worktree = fromTask (fun _ _ -> Task.FromResult job.Worktree)
            let! managerResult = fromTask (fun _ _ -> job.Completion)

            match managerResult with
            | Error error -> return failed job.ManagerId (sprintf "Manager run failed: %s" error)
            | Ok() ->
                let! reviewed = fromTask (fun _ _ -> preReview deps job)

                match reviewed with
                | Error verdict -> return verdict
                | Ok() ->
                    let! candidate = fromTask (fun _ _ -> recordCandidate deps job)

                    match candidate with
                    | Error verdict -> return verdict
                    | Ok() ->
                        use! _gate = fromTask (fun _ _ -> IntegrationGate.acquire deps.GatePath)
                        return! fromTask (fun _ _ -> publishLoop deps job false)
        }

    let run (deps: OrchestratorProgramDeps) (job: ManagerJob) =
        task {
            let context =
                { TargetBranch = deps.TargetBranch
                  WorktreePath = job.Worktree.Path }

            match! Flow.run context CancellationToken.None (program deps job) with
            | Ok verdict -> return verdict
            | Error error -> return failed job.ManagerId (sprintf "%A" error)
        }
