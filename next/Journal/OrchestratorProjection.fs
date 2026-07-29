namespace Wanxiangshu.Next.Journal

type ManagerId = private ManagerId of string

module ManagerId =
    let create value = ManagerId value
    let value (ManagerId value) = value

type CandidateId = private CandidateId of string

module CandidateId =
    let create value = CandidateId value
    let value (CandidateId value) = value

type CandidateStatus =
    | Registered of candidateId: CandidateId * branch: string * commitHash: string
    | Published of candidateId: CandidateId * commitHash: string
    | Rejected of candidateId: CandidateId * reason: string

type ManagerState = { Status: CandidateStatus option }

type ManagerJobProjection =
    { WorktreePath: string
      Branch: string
      CandidateId: CandidateId option
      CandidateCommit: string option
      PublishedCommit: string option
      Prompt: string
      PreRebaseReviewCommit: string option
      RebasedCommit: string option
      ConflictFiles: string list option
      PostRebaseReviewCommit: string option
      PublishClaimHead: string option }

type OrchestratorProjection =
    { ManagerJobs: Map<ManagerId, ManagerJobProjection>
      Managers: Map<ManagerId, ManagerState>
      PublishedCommit: string option }

/// Durable Git/publish facts only; no workflow stage is stored.
module OrchestratorProjection =

    let empty =
        { ManagerJobs = Map.empty
          Managers = Map.empty
          PublishedCommit = None }

    let createManager managerId worktree branch prompt projection =
        let id = ManagerId.create managerId

        let job =
            { WorktreePath = worktree
              Branch = branch
              CandidateId = None
              CandidateCommit = None
              PublishedCommit = None
              Prompt = prompt
              PreRebaseReviewCommit = None
              RebasedCommit = None
              ConflictFiles = None
              PostRebaseReviewCommit = None
              PublishClaimHead = None }

        { projection with ManagerJobs = Map.add id job projection.ManagerJobs }

    let private updateJob managerId update projection =
        let id = ManagerId.create managerId

        match Map.tryFind id projection.ManagerJobs with
        | None -> projection
        | Some job ->
            { projection with
                ManagerJobs = Map.add id (update job) projection.ManagerJobs }

    let registerCandidate managerId candidateId branch commit projection =
        let manager = ManagerId.create managerId
        let candidate = CandidateId.create candidateId
        let status = Registered(candidate, branch, commit)

        let withJob =
            updateJob
                managerId
                (fun job ->
                    { job with
                        CandidateId = Some candidate
                        CandidateCommit = Some commit })
                projection

        { withJob with Managers = Map.add manager { Status = Some status } withJob.Managers }

    let published managerId commit projection =
        let id = ManagerId.create managerId

        { projection with
            ManagerJobs = Map.remove id projection.ManagerJobs
            Managers = Map.remove id projection.Managers
            PublishedCommit = Some commit }

    let rejected managerId projection =
        let id = ManagerId.create managerId

        { projection with
            ManagerJobs = Map.remove id projection.ManagerJobs
            Managers = Map.remove id projection.Managers }

    let preRebaseReviewed managerId commit projection =
        updateJob managerId (fun job -> { job with PreRebaseReviewCommit = Some commit }) projection

    let rebased managerId commit projection =
        updateJob
            managerId
            (fun job ->
                { job with
                    RebasedCommit = Some commit
                    ConflictFiles = None })
            projection

    let conflict managerId files projection =
        updateJob managerId (fun job -> { job with ConflictFiles = Some files }) projection

    let postRebaseReviewed managerId commit projection =
        updateJob managerId (fun job -> { job with PostRebaseReviewCommit = Some commit }) projection

    let claimed managerId expectedHead projection =
        updateJob managerId (fun job -> { job with PublishClaimHead = Some expectedHead }) projection
