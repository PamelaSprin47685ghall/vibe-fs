namespace Wanxiangshu.Next.Orchestrator

open Wanxiangshu.Next.Journal

/// Queries durable Git/review facts for crash recovery. No workflow stage is
/// reconstructed; the ordinary program rechecks each external fact in order.
module OrchestratorRecovery =

    let currentJob snapshot managerId worktreePath prompt =
        Map.tryFind
            (ManagerId.create managerId)
            snapshot.AgentProjections.Orchestrator.ManagerJobs
        |> Option.defaultValue
            { WorktreePath = worktreePath
              Branch = sprintf "manager/%s" managerId
              CandidateId = None
              CandidateCommit = None
              PublishedCommit = None
              Prompt = prompt
              PreRebaseReviewCommit = None
              RebasedCommit = None
              ConflictFiles = None
              PostRebaseReviewCommit = None
              PublishClaimHead = None }

    let candidateId managerId job =
        job.CandidateId
        |> Option.map CandidateId.value
        |> Option.defaultValue (sprintf "candidate-%s" managerId)
