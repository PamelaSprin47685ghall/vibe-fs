namespace Wanxiangshu.Next.Orchestrator

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

module PublishStages =
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

    let emptyJob managerId worktreePath : ManagerJob =
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

    let currentJob (deps: Deps) managerId worktreePath : ManagerJob =
        let projection = deps.Snapshot()

        Map.tryFind (ManagerId.create managerId) projection.AgentProjections.Orchestrator.ManagerJobs
        |> Option.defaultValue (emptyJob managerId worktreePath)

    let candidateIdString job managerId =
        job.CandidateId
        |> Option.map CandidateId.value
        |> Option.defaultValue (sprintf "candidate-%s" managerId)

    let append deps managerId message : Result<unit, OrchestratorVerdict> =
        match deps.AppendFact StreamId.Workspace message with
        | Ok() -> Ok()
        | Error err -> Error(IntegrationFailed(managerId, err))

    let readHead (deps: Deps) managerId worktreePath : Task<Result<string, OrchestratorVerdict>> =
        task {
            match! deps.ReadHead worktreePath "" with
            | Ok head -> return Ok head
            | Error err -> return Error(IntegrationFailed(managerId, sprintf "Git head lookup failed: %s" err))
        }

    let reviewStage (deps: Deps) managerId worktreePath : Task<Result<unit, OrchestratorVerdict>> =
        task {
            let job = currentJob deps managerId worktreePath
            let! headResult = readHead deps managerId worktreePath

            match headResult with
            | Error verdict -> return Error verdict
            | Ok head ->
                // A pre-rebase review is valid only for the current candidate
                // commit. Any later manager/coder change invalidates it.
                match job.PreRebaseReviewCommit = Some head with
                | true -> return Ok()
                | false ->
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

    let postReviewStage (deps: Deps) managerId worktreePath : Task<Result<unit, OrchestratorVerdict>> =
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
