namespace Wanxiangshu.Next.Orchestrator

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

module PublishChain =
    type Deps =
        { Git: GitPort
          Manager: ManagerPort
          AppendFact: StreamId -> AgentFact -> Result<unit, string>
          ReverifyTwice: string -> string -> string -> Task<Result<unit, string>>
          ReadHead: string -> string -> Task<Result<string, string>>
          ReconcileTarget: unit -> Task<Result<unit, string>>
          GetTargetHead: unit -> Task<Result<string, string>>
          TargetBranch: string
          Prompts: Dictionary<string, string>
          Snapshot: unit -> ProjectionSet }

    type private PassResult =
        | Redrive
        | PublishedCommit of string

    let private emptyJob managerId worktreePath : ManagerJob =
        { WorktreePath = worktreePath
          Branch = sprintf "manager/%s" managerId
          CandidateId = None
          CandidateCommit = None
          PublishedCommit = None
          Prompt = ""
          PreRebaseReviewCommit = None
          RebasedCommit = None
          ConflictFiles = None
          PostRebaseReviewCommit = None
          PublishClaimHead = None }

    let private currentJob (deps: Deps) managerId worktreePath : ManagerJob =
        let projection = deps.Snapshot()

        Map.tryFind (ManagerId.create managerId) projection.AgentProjections.Orchestrator.ManagerJobs
        |> Option.defaultValue (emptyJob managerId worktreePath)

    let private candidateIdString job managerId =
        job.CandidateId
        |> Option.map CandidateId.value
        |> Option.defaultValue (sprintf "candidate-%s" managerId)

    let private append deps managerId message : Result<unit, OrchestratorVerdict> =
        match deps.AppendFact StreamId.Workspace message with
        | Ok() -> Ok()
        | Error err -> Error(IntegrationFailed(managerId, err))

    let private readHead (deps: Deps) managerId worktreePath : Task<Result<string, OrchestratorVerdict>> =
        task {
            match! deps.ReadHead worktreePath "" with
            | Ok head -> return Ok head
            | Error err -> return Error(IntegrationFailed(managerId, sprintf "Git head lookup failed: %s" err))
        }

    let private reviewStage (deps: Deps) managerId worktreePath : Task<Result<unit, OrchestratorVerdict>> =
        task {
            let job = currentJob deps managerId worktreePath
            let! headResult = readHead deps managerId worktreePath

            match headResult with
            | Error verdict -> return Error verdict
            | Ok head ->
                // Idempotent skip: the pre-rebase review is durably confirmed
                // once OrchestratorPreRebaseReviewConfirmed is recorded. A manager
                // re-run (crash recovery) regenerates the candidate commit hash,
                // so comparing against the current head would wrongly re-run the
                // review; the confirmation is content-stable, so skip on presence.
                match job.PreRebaseReviewCommit with
                | Some _ -> return Ok()
                | None ->
                    match! deps.ReverifyTwice managerId worktreePath "pre-rebase" with
                    | Error err -> return Error(NeedsReview(managerId, err))
                    | Ok() ->
                        let fact =
                            AgentFact.OrchestratorPreRebaseReviewConfirmed
                                {| ManagerId = managerId
                                   CandidateId = candidateIdString job managerId
                                   CommitHash = head |}

                        return append deps managerId fact
        }

    let private candidateStage (deps: Deps) managerId worktreePath : Task<Result<unit, OrchestratorVerdict>> =
        task {
            let job = currentJob deps managerId worktreePath

            match job.CandidateCommit with
            | Some _ -> return Ok()
            | None ->
                let! headResult = readHead deps managerId worktreePath

                match headResult with
                | Error verdict -> return Error verdict
                | Ok head ->
                    let fact =
                        AgentFact.OrchestratorCandidateRegistered
                            {| ManagerId = managerId
                               CandidateId = candidateIdString job managerId
                               Branch = sprintf "manager/%s" managerId
                               CommitHash = head |}

                    return append deps managerId fact
        }

    let private recordRebased deps managerId worktreePath candidateId : Task<Result<unit, OrchestratorVerdict>> =
        task {
            let! headResult = readHead deps managerId worktreePath

            match headResult with
            | Error verdict -> return Error verdict
            | Ok head ->
                let fact =
                    AgentFact.OrchestratorRebased
                        {| ManagerId = managerId
                           CandidateId = candidateId
                           RebasedCommit = head |}

                return append deps managerId fact
        }

    let private conflictFiles (deps: Deps) worktreePath : Task<string list> =
        task {
            match! deps.Git.ConflictedFiles worktreePath with
            | Ok files -> return files
            | Error _ -> return []
        }

    let private rebaseStage (deps: Deps) managerId worktreePath isRedrive : Task<Result<unit, OrchestratorVerdict>> =
        task {
            let job = currentJob deps managerId worktreePath
            let candidateId = candidateIdString job managerId
            let! headResult = readHead deps managerId worktreePath

            match headResult with
            | Error verdict -> return Error verdict
            | Ok head ->
                let! hasRebase = deps.Git.HasRebaseHead worktreePath
                let skip = not isRedrive && job.RebasedCommit = Some head && not hasRebase

                match skip with
                | true -> return Ok()
                | false ->
                    match! deps.Git.Rebase worktreePath deps.TargetBranch with
                    | Ok() -> return! recordRebased deps managerId worktreePath candidateId
                    | Error rebaseError ->
                        let! files = conflictFiles deps worktreePath

                        let conflictFact =
                            AgentFact.OrchestratorConflictDetected
                                {| ManagerId = managerId
                                   CandidateId = candidateId
                                   Files = files |}

                        match append deps managerId conflictFact with
                        | Error verdict -> return Error verdict
                        | Ok() ->
                            let prompt =
                                match deps.Prompts.TryGetValue managerId with
                                | true, saved -> OrchestratorPrompts.buildConflictResumePrompt saved files
                                | false, _ -> OrchestratorPrompts.buildConflictResumePrompt "" files

                            match! deps.Manager.RunManager managerId worktreePath prompt with
                            | Error err ->
                                return
                                    Error(
                                        IntegrationFailed(
                                            managerId,
                                            sprintf
                                                "Rebase conflict (%s); manager continuation failed: %s"
                                                rebaseError
                                                err
                                        )
                                    )
                            | Ok() ->
                                match! deps.Git.Rebase worktreePath deps.TargetBranch with
                                | Error err ->
                                    return
                                        Error(
                                            IntegrationFailed(managerId, sprintf "Rebase continuation failed: %s" err)
                                        )
                                | Ok() -> return! recordRebased deps managerId worktreePath candidateId
        }

    let private postReviewStage (deps: Deps) managerId worktreePath : Task<Result<unit, OrchestratorVerdict>> =
        task {
            let job = currentJob deps managerId worktreePath
            let! headResult = readHead deps managerId worktreePath

            match headResult with
            | Error verdict -> return Error verdict
            | Ok head ->
                match job.PostRebaseReviewCommit = Some head with
                | true -> return Ok()
                | false ->
                    match! deps.ReverifyTwice managerId worktreePath "post-rebase" with
                    | Error err -> return Error(NeedsReview(managerId, err))
                    | Ok() ->
                        let fact =
                            AgentFact.OrchestratorPostRebaseReviewConfirmed
                                {| ManagerId = managerId
                                   CandidateId = candidateIdString job managerId
                                   RebasedCommit = head |}

                        return append deps managerId fact
        }

    let private publishStage (deps: Deps) managerId worktreePath : Task<Result<PassResult, OrchestratorVerdict>> =
        task {
            let job = currentJob deps managerId worktreePath
            let! targetResult = deps.GetTargetHead()

            match targetResult with
            | Error err -> return Error(IntegrationFailed(managerId, sprintf "Git target head lookup failed: %s" err))
            | Ok targetHead ->
                let expectedTargetHead =
                    if String.IsNullOrWhiteSpace targetHead then
                        None
                    else
                        Some targetHead

                let claimResult =
                    match expectedTargetHead with
                    | None -> Ok()
                    | Some expected ->
                        match job.PublishClaimHead = Some expected with
                        | true -> Ok()
                        | false ->
                            append
                                deps
                                managerId
                                (AgentFact.OrchestratorPublishClaimed
                                    {| ManagerId = managerId
                                       CandidateId = candidateIdString job managerId
                                       ExpectedTargetHead = expected |})

                match claimResult with
                | Error verdict -> return Error verdict
                | Ok() ->
                    match! deps.Git.FfMerge worktreePath deps.TargetBranch expectedTargetHead with
                    | Error err when err = OrchestratorConstants.targetRefMovedError -> return Ok Redrive
                    | Error err -> return Error(IntegrationFailed(managerId, sprintf "FF merge failed: %s" err))
                    | Ok commitHash ->
                        let publishedFact =
                            AgentFact.OrchestratorPublished
                                {| ManagerId = managerId
                                   CandidateId = candidateIdString (currentJob deps managerId worktreePath) managerId
                                   CommitHash = commitHash |}

                        match append deps managerId publishedFact with
                        | Error verdict -> return Error verdict
                        | Ok() ->
                            let! _ = deps.Git.RemoveWorktree worktreePath
                            let! _ = deps.Git.DeleteBranch(sprintf "manager/%s" managerId)
                            return Ok(PublishedCommit commitHash)
        }

    let private pass deps managerId worktreePath isRedrive : Task<Result<PassResult, OrchestratorVerdict>> =
        task {
            let! rebaseResult = rebaseStage deps managerId worktreePath isRedrive

            match rebaseResult with
            | Error verdict -> return Error verdict
            | Ok() ->
                let! reviewResult = postReviewStage deps managerId worktreePath

                match reviewResult with
                | Error verdict -> return Error verdict
                | Ok() -> return! publishStage deps managerId worktreePath
        }

    let run (deps: Deps) (completion: ManagerCompletion) : Task<OrchestratorVerdict> =
        let managerId = completion.Handle.ManagerId
        let worktreePath = completion.Handle.WorktreePath

        let rec loop attempt =
            task {
                match attempt >= 64 with
                | true ->
                    return IntegrationFailed(managerId, "publish chain did not converge after 64 target movements")
                | false ->
                    let! passResult = pass deps managerId worktreePath (attempt > 0)

                    match passResult with
                    | Error verdict -> return verdict
                    | Ok Redrive -> return! loop (attempt + 1)
                    | Ok(PublishedCommit commitHash) -> return OrchestratorVerdict.Published(managerId, commitHash)
            }

        task {
            match completion.Result with
            | Error err -> return IntegrationFailed(managerId, err)
            | Ok() ->
                match! deps.ReconcileTarget() with
                | Error err -> return IntegrationFailed(managerId, sprintf "Git reconcile failed: %s" err)
                | Ok() ->
                    let! reviewResult = reviewStage deps managerId worktreePath

                    match reviewResult with
                    | Error verdict -> return verdict
                    | Ok() ->
                        let! candidateResult = candidateStage deps managerId worktreePath

                        match candidateResult with
                        | Error verdict -> return verdict
                        | Ok() -> return! loop 0
        }
