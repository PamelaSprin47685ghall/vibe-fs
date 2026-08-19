namespace Wanxiangshu.Change

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type TerminalOutcome =
    | Published of {| CandidateCommit: CommitHash; ResultingTargetHead: CommitHash |}
    | Failed of reason: string
    | Abandoned

type ManagerJobProjection =
    {
        ManagerJobId: ManagerJobId
        ManagerSessionId: SessionId
        ManagerAgent: string
        Byname: string
        WorktreeIdentity: WorktreeIdentity
        WorktreePath: WorktreePath
        TargetRef: TargetRef
        TargetBranchFrozen: string
        CandidateReady: {| CandidateCommit: CommitHash; PreRebaseReviewBarrierId: ReviewBarrierId |} option
        ConflictDetected: {| CandidateCommit: CommitHash; TargetHeadSnapshot: CommitHash; ConflictFiles: string list; DiagnosticsDigest: string |} option
        RebasedCandidateReady: {| RebasedCommit: CommitHash; TargetHeadSnapshot: CommitHash; PostRebaseReviewBarrierId: ReviewBarrierId |} option
        PublishClaimed: {| RebasedCommit: CommitHash; ExpectedHead: CommitHash |} option
        Terminal: TerminalOutcome option
    }

[<RequireQualifiedAccess>]
type WorktreeEffectStatus =
    | Requested of {| ManagerJobId: ManagerJobId; WorktreePath: WorktreePath |}
    | Created of {| ManagerJobId: ManagerJobId; WorktreePath: WorktreePath |}

type OrchestratorProjection =
    { Jobs: Map<ManagerJobId, ManagerJobProjection>
      WorktreeEffects: Map<WorktreeIdentity, WorktreeEffectStatus> }

[<RequireQualifiedAccess>]
type RebasedCandidateReality =
    | HeadUnreadable
    | PublishReady
    | NeedsRebase

[<RequireQualifiedAccess>]
type PublishClaimReality =
    | HeadUnreadable
    | AlreadyFastForwarded
    | PublishReady
    | ClaimExpired

module OrchestratorProjection =

    let empty =
        { Jobs = Map.empty
          WorktreeEffects = Map.empty }

    let tryFind (jobId: ManagerJobId) (projection: OrchestratorProjection) = Map.tryFind jobId projection.Jobs

    let tryFindByByname (byname: string) (projection: OrchestratorProjection) =
        if System.String.IsNullOrWhiteSpace byname then None
        else
            let wanted = byname.Trim()
            let matchesWanted (job: ManagerJobProjection) = System.String.Equals(job.Byname, wanted, System.StringComparison.OrdinalIgnoreCase)
            projection.Jobs |> Map.tryPick (fun _ job -> if matchesWanted job then Some job else None)

    let tryWorktreeEffect (identity: WorktreeIdentity) (projection: OrchestratorProjection) = Map.tryFind identity projection.WorktreeEffects

    let requestWorktree (identity: WorktreeIdentity) (path: WorktreePath) (jobId: ManagerJobId) (projection: OrchestratorProjection) =
        match Map.tryFind identity projection.WorktreeEffects with
        | Some _ -> projection
        | None -> { projection with WorktreeEffects = Map.add identity (WorktreeEffectStatus.Requested {| ManagerJobId = jobId; WorktreePath = path |}) projection.WorktreeEffects }

    let acceptWorktree (identity: WorktreeIdentity) (path: WorktreePath) (jobId: ManagerJobId) (projection: OrchestratorProjection) =
        let created = WorktreeEffectStatus.Created {| ManagerJobId = jobId; WorktreePath = path |}
        match Map.tryFind identity projection.WorktreeEffects with
        | Some(WorktreeEffectStatus.Created _) -> projection
        | _ -> { projection with WorktreeEffects = Map.add identity created projection.WorktreeEffects }

    let tryFindByManagerSession (managerSessionId: SessionId) (projection: OrchestratorProjection) =
        projection.Jobs |> Map.tryPick (fun _ job -> if job.ManagerSessionId = managerSessionId then Some job else None)

    let private isTerminal (job: ManagerJobProjection) = job.Terminal.IsSome

    let activeJobs (projection: OrchestratorProjection) =
        projection.Jobs |> Map.toList |> List.map snd |> List.filter (fun job -> job.Terminal.IsNone)

    let private effectiveByname (managerAgent: string) (byname: string) =
        if System.String.IsNullOrWhiteSpace byname then managerAgent else byname

    let createJob (job: {| ManagerJobId: ManagerJobId; ManagerSessionId: SessionId; ManagerAgent: string; Byname: string; WorktreeIdentity: WorktreeIdentity; WorktreePath: WorktreePath; TargetRef: TargetRef; TargetBranchFrozen: string |}) (projection: OrchestratorProjection) =
        if Map.containsKey job.ManagerJobId projection.Jobs then projection
        else { projection with Jobs = Map.add job.ManagerJobId { ManagerJobId = job.ManagerJobId; ManagerSessionId = job.ManagerSessionId; ManagerAgent = job.ManagerAgent; Byname = effectiveByname job.ManagerAgent job.Byname; WorktreeIdentity = job.WorktreeIdentity; WorktreePath = job.WorktreePath; TargetRef = job.TargetRef; TargetBranchFrozen = job.TargetBranchFrozen; CandidateReady = None; ConflictDetected = None; RebasedCandidateReady = None; PublishClaimed = None; Terminal = None } projection.Jobs }

    let private guardActive (job: ManagerJobProjection) = match job.Terminal with | Some _ -> None | None -> Some job

    let recordCandidateReady (jobId: ManagerJobId) (payload: {| CandidateCommit: CommitHash; PreRebaseReviewBarrierId: ReviewBarrierId |}) (projection: OrchestratorProjection) =
        match Map.tryFind jobId projection.Jobs with | None -> projection | Some job -> match guardActive job with | None -> projection | Some _ -> { projection with Jobs = Map.add jobId { job with CandidateReady = Some payload } projection.Jobs }

    let recordConflictDetected (jobId: ManagerJobId) (payload: {| CandidateCommit: CommitHash; TargetHeadSnapshot: CommitHash; ConflictFiles: string list; DiagnosticsDigest: string |}) (projection: OrchestratorProjection) =
        match Map.tryFind jobId projection.Jobs with | None -> projection | Some job -> match guardActive job with | None -> projection | Some _ -> { projection with Jobs = Map.add jobId { job with ConflictDetected = Some payload } projection.Jobs }

    let recordRebasedCandidateReady (jobId: ManagerJobId) (payload: {| RebasedCommit: CommitHash; TargetHeadSnapshot: CommitHash; PostRebaseReviewBarrierId: ReviewBarrierId |}) (projection: OrchestratorProjection) =
        match Map.tryFind jobId projection.Jobs with | None -> projection | Some job -> match guardActive job with | None -> projection | Some _ -> { projection with Jobs = Map.add jobId { job with RebasedCandidateReady = Some payload } projection.Jobs }

    let recordPublishClaimed (jobId: ManagerJobId) (payload: {| RebasedCommit: CommitHash; ExpectedHead: CommitHash |}) (projection: OrchestratorProjection) =
        match Map.tryFind jobId projection.Jobs with | None -> projection | Some job -> match guardActive job with | None -> projection | Some _ -> { projection with Jobs = Map.add jobId { job with PublishClaimed = Some payload } projection.Jobs }

    let recordTerminal (jobId: ManagerJobId) (terminal: TerminalOutcome) (projection: OrchestratorProjection) =
        match Map.tryFind jobId projection.Jobs with | None -> projection | Some job -> match guardActive job with | None -> projection | Some _ -> { projection with Jobs = Map.add jobId { job with Terminal = Some terminal } projection.Jobs }

    let classifyRebasedCandidate (currentHead: CommitHash option) (rebasedCommit: CommitHash) (targetHeadSnapshot: CommitHash) : RebasedCandidateReality =
        match currentHead with | None -> RebasedCandidateReality.HeadUnreadable | Some head when head = targetHeadSnapshot -> RebasedCandidateReality.PublishReady | Some _ -> RebasedCandidateReality.NeedsRebase

    let classifyPublishClaim (currentHead: CommitHash option) (rebasedCommit: CommitHash) (expectedHead: CommitHash) : PublishClaimReality =
        match currentHead with | None -> PublishClaimReality.HeadUnreadable | Some head when head = rebasedCommit -> PublishClaimReality.AlreadyFastForwarded | Some head when head = expectedHead -> PublishClaimReality.PublishReady | Some _ -> PublishClaimReality.ClaimExpired