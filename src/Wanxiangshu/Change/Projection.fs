namespace Wanxiangshu.Change

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Relay

[<RequireQualifiedAccess>]
type TerminalOutcome =
    | Published of
        {| CandidateCommit: CommitHash
           ResultingTargetHead: CommitHash |}
    | Failed of reason: string
    | Abandoned

type ManagerJobProjection =
    { ManagerJobId: ManagerJobId
      ManagerSessionId: SessionId
      ManagerAgent: string
      Byname: string
      WorktreeIdentity: WorktreeIdentity
      WorktreePath: WorktreePath
      TargetRef: TargetRef
      TargetBranchFrozen: string
      CandidateReady:
          {| CandidateCommit: CommitHash
             WorkspaceSnapshotId: WorkspaceSnapshotId
             QualityCertificateId: QualityCertificateId |} option
      ConflictDetected:
          {| CandidateCommit: CommitHash
             TargetHeadSnapshot: CommitHash
             WorkspaceSnapshotId: WorkspaceSnapshotId
             ConflictFiles: string list
             DiagnosticsDigest: string |} option
      RebasedCandidateReady:
          {| RebasedCommit: CommitHash
             TargetHeadSnapshot: CommitHash
             WorkspaceSnapshotId: WorkspaceSnapshotId |} option
      PublishClaimed:
          {| RebasedCommit: CommitHash
             ExpectedHead: CommitHash |} option
      Terminal: TerminalOutcome option }

[<RequireQualifiedAccess>]
type WorktreeEffectStatus =
    | Requested of
        {| ManagerJobId: ManagerJobId
           WorktreePath: WorktreePath |}
    | Created of
        {| ManagerJobId: ManagerJobId
           WorktreePath: WorktreePath |}

/// Complete evidence presented to the pure worktree reconciliation boundary.
/// Physical evidence is representable only for the ambiguous Requested state.
[<RequireQualifiedAccess>]
type WorktreeReconciliationObservation =
    | NoDurableEffect
    | RequestedConflict of recordedJobId: ManagerJobId * recordedPath: WorktreePath
    | RequestedAmbiguity of
        recordedJobId: ManagerJobId *
        recordedPath: WorktreePath *
        physical: Result<(WorktreePath * WorktreeIdentity option) list, string>
    | CreatedReceipt of recordedJobId: ManagerJobId * recordedPath: WorktreePath

[<RequireQualifiedAccess>]
type WorktreeReconciliationFailure =
    | DurableOwnershipConflict
    | WorktreeQueryFailed of string
    | PhysicalIdentityPathConflict

[<RequireQualifiedAccess>]
type WorktreeReconciliationDecision =
    | RequestThenCreate
    | CreateAfterProvenMissing
    | AdoptThenRecordCreated
    | AdoptCreated
    | Reject of WorktreeReconciliationFailure

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
        if System.String.IsNullOrWhiteSpace byname then
            None
        else
            let wanted = byname.Trim()

            let matchesWanted (job: ManagerJobProjection) =
                System.String.Equals(job.Byname, wanted, System.StringComparison.OrdinalIgnoreCase)

            projection.Jobs
            |> Map.tryPick (fun _ job -> if matchesWanted job then Some job else None)

    let tryWorktreeEffect (identity: WorktreeIdentity) (projection: OrchestratorProjection) =
        Map.tryFind identity projection.WorktreeEffects

    /// PERSIST-009: decide from durable intent plus, only when Requested is
    /// ambiguous, one complete physical worktree observation.
    let decideWorktreeReconciliation
        (jobId: ManagerJobId)
        (identity: WorktreeIdentity)
        (path: WorktreePath)
        (observation: WorktreeReconciliationObservation)
        : WorktreeReconciliationDecision =
        let sameDurableOwner recordedJobId recordedPath =
            recordedJobId = jobId && recordedPath = path

        let decidePhysicalWorktreeReconciliation physical =
            let relevantPhysicalEntries =
                physical
                |> Result.map (
                    List.filter (fun (observedPath, observedIdentity) ->
                        observedPath = path || observedIdentity = Some identity)
                )

            match relevantPhysicalEntries with
            | Error error ->
                WorktreeReconciliationDecision.Reject(WorktreeReconciliationFailure.WorktreeQueryFailed error)
            | Ok [] -> WorktreeReconciliationDecision.CreateAfterProvenMissing
            | Ok relevant when
                relevant
                |> List.forall (fun (observedPath, observedIdentity) ->
                    observedPath = path && observedIdentity = Some identity)
                ->
                WorktreeReconciliationDecision.AdoptThenRecordCreated
            | Ok _ -> WorktreeReconciliationDecision.Reject WorktreeReconciliationFailure.PhysicalIdentityPathConflict

        match observation with
        | WorktreeReconciliationObservation.NoDurableEffect -> WorktreeReconciliationDecision.RequestThenCreate
        | WorktreeReconciliationObservation.CreatedReceipt(recordedJobId, recordedPath) when
            sameDurableOwner recordedJobId recordedPath
            ->
            WorktreeReconciliationDecision.AdoptCreated
        | WorktreeReconciliationObservation.CreatedReceipt _
        | WorktreeReconciliationObservation.RequestedConflict _ ->
            WorktreeReconciliationDecision.Reject WorktreeReconciliationFailure.DurableOwnershipConflict
        | WorktreeReconciliationObservation.RequestedAmbiguity(recordedJobId, recordedPath, _) when
            not (sameDurableOwner recordedJobId recordedPath)
            ->
            WorktreeReconciliationDecision.Reject WorktreeReconciliationFailure.DurableOwnershipConflict
        | WorktreeReconciliationObservation.RequestedAmbiguity(_, _, physical) ->
            decidePhysicalWorktreeReconciliation physical

    let requestWorktree
        (identity: WorktreeIdentity)
        (path: WorktreePath)
        (jobId: ManagerJobId)
        (projection: OrchestratorProjection)
        =
        match Map.tryFind identity projection.WorktreeEffects with
        | Some _ -> projection
        | None ->
            { projection with
                WorktreeEffects =
                    Map.add
                        identity
                        (WorktreeEffectStatus.Requested
                            {| ManagerJobId = jobId
                               WorktreePath = path |})
                        projection.WorktreeEffects }

    let acceptWorktree
        (identity: WorktreeIdentity)
        (path: WorktreePath)
        (jobId: ManagerJobId)
        (projection: OrchestratorProjection)
        =
        let created =
            WorktreeEffectStatus.Created
                {| ManagerJobId = jobId
                   WorktreePath = path |}

        match Map.tryFind identity projection.WorktreeEffects with
        | Some(WorktreeEffectStatus.Created _) -> projection
        | _ ->
            { projection with
                WorktreeEffects = Map.add identity created projection.WorktreeEffects }

    let tryFindByManagerSession (managerSessionId: SessionId) (projection: OrchestratorProjection) =
        projection.Jobs
        |> Map.tryPick (fun _ job ->
            if job.ManagerSessionId = managerSessionId then
                Some job
            else
                None)

    let activeJobs (projection: OrchestratorProjection) =
        projection.Jobs
        |> Map.toList
        |> List.map snd
        |> List.filter (fun job -> job.Terminal.IsNone)

    let private effectiveByname (managerAgent: string) (byname: string) =
        if System.String.IsNullOrWhiteSpace byname then
            managerAgent
        else
            byname

    let createJob
        (job:
            {| ManagerJobId: ManagerJobId
               ManagerSessionId: SessionId
               ManagerAgent: string
               Byname: string
               WorktreeIdentity: WorktreeIdentity
               WorktreePath: WorktreePath
               TargetRef: TargetRef
               TargetBranchFrozen: string |})
        (projection: OrchestratorProjection)
        =
        if Map.containsKey job.ManagerJobId projection.Jobs then
            projection
        else
            { projection with
                Jobs =
                    Map.add
                        job.ManagerJobId
                        { ManagerJobId = job.ManagerJobId
                          ManagerSessionId = job.ManagerSessionId
                          ManagerAgent = job.ManagerAgent
                          Byname = effectiveByname job.ManagerAgent job.Byname
                          WorktreeIdentity = job.WorktreeIdentity
                          WorktreePath = job.WorktreePath
                          TargetRef = job.TargetRef
                          TargetBranchFrozen = job.TargetBranchFrozen
                          CandidateReady = None
                          ConflictDetected = None
                          RebasedCandidateReady = None
                          PublishClaimed = None
                          Terminal = None }
                        projection.Jobs }

    let private updateActiveJob
        (jobId: ManagerJobId)
        (update: ManagerJobProjection -> ManagerJobProjection)
        (projection: OrchestratorProjection)
        =
        match Map.tryFind jobId projection.Jobs with
        | Some job when job.Terminal.IsNone ->
            { projection with
                Jobs = Map.add jobId (update job) projection.Jobs }
        | _ -> projection

    let recordCandidateReady
        (jobId: ManagerJobId)
        (payload:
            {| CandidateCommit: CommitHash
               WorkspaceSnapshotId: WorkspaceSnapshotId
               QualityCertificateId: QualityCertificateId |})
        (projection: OrchestratorProjection)
        =
        updateActiveJob
            jobId
            (fun job ->
                { job with
                    CandidateReady = Some payload })
            projection

    let recordConflictDetected
        (jobId: ManagerJobId)
        (payload:
            {| CandidateCommit: CommitHash
               TargetHeadSnapshot: CommitHash
               WorkspaceSnapshotId: WorkspaceSnapshotId
               ConflictFiles: string list
               DiagnosticsDigest: string |})
        (projection: OrchestratorProjection)
        =
        updateActiveJob
            jobId
            (fun job ->
                { job with
                    ConflictDetected = Some payload })
            projection

    let recordRebasedCandidateReady
        (jobId: ManagerJobId)
        (payload:
            {| RebasedCommit: CommitHash
               TargetHeadSnapshot: CommitHash
               WorkspaceSnapshotId: WorkspaceSnapshotId |})
        (projection: OrchestratorProjection)
        =
        updateActiveJob
            jobId
            (fun job ->
                { job with
                    RebasedCandidateReady = Some payload })
            projection

    let recordPublishClaimed
        (jobId: ManagerJobId)
        (payload:
            {| RebasedCommit: CommitHash
               ExpectedHead: CommitHash |})
        (projection: OrchestratorProjection)
        =
        updateActiveJob
            jobId
            (fun job ->
                { job with
                    PublishClaimed = Some payload })
            projection

    let recordTerminal (jobId: ManagerJobId) (terminal: TerminalOutcome) (projection: OrchestratorProjection) =
        updateActiveJob jobId (fun job -> { job with Terminal = Some terminal }) projection

    let classifyRebasedCandidate
        (currentHead: CommitHash option)
        (rebasedCommit: CommitHash)
        (targetHeadSnapshot: CommitHash)
        : RebasedCandidateReality =
        match currentHead with
        | None -> RebasedCandidateReality.HeadUnreadable
        | Some head when head = targetHeadSnapshot -> RebasedCandidateReality.PublishReady
        | Some _ -> RebasedCandidateReality.NeedsRebase

    let classifyPublishClaim
        (currentHead: CommitHash option)
        (rebasedCommit: CommitHash)
        (expectedHead: CommitHash)
        : PublishClaimReality =
        match currentHead with
        | None -> PublishClaimReality.HeadUnreadable
        | Some head when head = rebasedCommit -> PublishClaimReality.AlreadyFastForwarded
        | Some head when head = expectedHead -> PublishClaimReality.PublishReady
        | Some _ -> PublishClaimReality.ClaimExpired
