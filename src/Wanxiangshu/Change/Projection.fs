namespace Wanxiangshu.Change

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Work

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
      CandidateReady: {| CandidateCommit: CommitHash |} option
      ConflictDetected:
          {| CandidateCommit: CommitHash
             TargetHeadSnapshot: CommitHash
             ConflictFiles: string list
             DiagnosticsDigest: string |} option
      RebasedCandidateReady:
          {| RebasedCommit: CommitHash
             TargetHeadSnapshot: CommitHash |} option
      PublishClaimed:
          {| TargetRef: TargetRef
             RebasedCommit: CommitHash
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

/// Whether a prepared candidate may publish as it stands.
[<RequireQualifiedAccess>]
type CandidateAdmission =
    | PublishCertified
    | Rebase of reason: string

/// Whether the rebased pin still certifies the candidate about to publish.
[<RequireQualifiedAccess>]
type PublicationCertification =
    | Certified
    | MissingRebased
    | CandidatePinMismatch
    | SnapshotPinMismatch

/// Whether the publication request still matches the job and target it claims.
[<RequireQualifiedAccess>]
type PublicationCheck =
    | Matched
    | JobMismatch
    | TargetMismatch
    | CasMismatch

/// What the fast-forward actually did.
[<RequireQualifiedAccess>]
type FastForwardOutcome =
    | Landed of CommitHash
    | LandedMismatch of CommitHash
    | TargetMoved
    | Failed of string

/// Whether a failed rebase is a conflict this job owns.
[<RequireQualifiedAccess>]
type ConflictOwnership =
    | Conflict of files: string list
    | NotAConflict

/// Where a recovered job continues.
[<RequireQualifiedAccess>]
type RecoveryStep =
    | CleanUp
    | ReenterClaim
    | RunLoop

/// Why a durable publish claim cannot be re-entered.
[<RequireQualifiedAccess>]
type ClaimReentryRefusal =
    | TargetMismatch
    | MissingRecord
    | MissingRebased

/// What a durable publish claim may do after a restart.
[<RequireQualifiedAccess>]
type ClaimReentry =
    | VerifyHead
    | RunLoop
    | Refused of ClaimReentryRefusal

/// The publication decisions Change owns.
///
/// Publishing is Change's responsibility: which candidate may publish, which
/// binding is still valid, and what a fast-forward outcome means. These are
/// pure decisions over durable facts and the observed Git reality, so they live
/// in the owner as typed answers — the host never re-reads a string protocol to
/// learn what a library meant.
[<RequireQualifiedAccess>]
module PublicationLaw =
    /// A terminal job is only cleaned up, a claimed one re-enters its claim,
    /// anything else resumes the manager loop.
    let resume (record: ManagerJobProjection option) : RecoveryStep =
        match record with
        | Some job when job.Terminal.IsSome -> RecoveryStep.CleanUp
        | Some job when job.PublishClaimed.IsSome -> RecoveryStep.ReenterClaim
        | _ -> RecoveryStep.RunLoop

    /// Re-entering a durable publish claim after restart: the claim must target
    /// the job's own ref and still pin the latest rebased candidate exactly.
    let reenterClaim
        (jobTargetRef: TargetRef)
        (claimTargetRef: TargetRef)
        (claimRebased: CommitHash)
        (claimExpected: CommitHash)
        (record: ManagerJobProjection option)
        : ClaimReentry =
        let rebasedCandidateReentry (job: ManagerJobProjection) =
            match job.RebasedCandidateReady with
            | None -> ClaimReentry.Refused ClaimReentryRefusal.MissingRebased
            | Some rebased when
                rebased.RebasedCommit = claimRebased
                && rebased.TargetHeadSnapshot = claimExpected
                ->
                ClaimReentry.VerifyHead
            | Some _ -> ClaimReentry.RunLoop

        match record with
        | _ when claimTargetRef <> jobTargetRef -> ClaimReentry.Refused ClaimReentryRefusal.TargetMismatch
        | None -> ClaimReentry.Refused ClaimReentryRefusal.MissingRecord
        | Some job -> rebasedCandidateReentry job

    /// A prepared candidate publishes only when the latest rebased record pins
    /// exactly this candidate on exactly this target head; otherwise it rebases.
    let admitCandidate
        (candidate: CommitHash)
        (target: CommitHash)
        (rebased:
            {| RebasedCommit: CommitHash
               TargetHeadSnapshot: CommitHash |} option)
        : CandidateAdmission =
        match rebased with
        | Some ready when ready.RebasedCommit = candidate && ready.TargetHeadSnapshot = target ->
            CandidateAdmission.PublishCertified
        | Some _ -> CandidateAdmission.Rebase "TargetAdvanced"
        | None -> CandidateAdmission.Rebase "InitialRebaseRequired"

    /// Certified publish evidence must match the rebased pin: candidate first,
    /// then snapshot.
    let certify
        (candidate: CommitHash)
        (expectedHead: CommitHash)
        (rebased:
            {| RebasedCommit: CommitHash
               TargetHeadSnapshot: CommitHash |} option)
        : PublicationCertification =
        match rebased with
        | None -> PublicationCertification.MissingRebased
        | Some ready when candidate <> ready.RebasedCommit -> PublicationCertification.CandidatePinMismatch
        | Some ready when expectedHead <> ready.TargetHeadSnapshot -> PublicationCertification.SnapshotPinMismatch
        | Some _ -> PublicationCertification.Certified

    /// A failed rebase with unmerged files is this job's conflict; without
    /// conflicted files the failure is not a conflict and the job fails.
    let rebaseFailed (conflictFiles: string list) : ConflictOwnership =
        match conflictFiles with
        | [] -> ConflictOwnership.NotAConflict
        | files -> ConflictOwnership.Conflict files

    /// Inside the publish window: the evidence must belong to this job and
    /// target, and the target head must still be the CAS expectation.
    let checkPublication
        (jobId: ManagerJobId)
        (jobTargetRef: TargetRef)
        (evidenceJobId: ManagerJobId)
        (evidenceTargetRef: TargetRef)
        (evidenceExpectedHead: CommitHash)
        (current: CommitHash)
        : PublicationCheck =
        if evidenceJobId <> jobId then
            PublicationCheck.JobMismatch
        elif evidenceTargetRef <> jobTargetRef then
            PublicationCheck.TargetMismatch
        elif current <> evidenceExpectedHead then
            PublicationCheck.CasMismatch
        else
            PublicationCheck.Matched

    /// Only the pinned candidate counts as landed; the declared target-moved
    /// error is a retryable miss, never a failure.
    let fastForward
        (merge: Result<CommitHash, string>)
        (candidate: CommitHash)
        (targetMovedError: string)
        : FastForwardOutcome =
        match merge with
        | Ok landed when landed = candidate -> FastForwardOutcome.Landed landed
        | Ok landed -> FastForwardOutcome.LandedMismatch landed
        | Error error when error = targetMovedError -> FastForwardOutcome.TargetMoved
        | Error error -> FastForwardOutcome.Failed error

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
        let sameOwner (recordedJobId: ManagerJobId) (recordedPath: WorktreePath) =
            recordedJobId = jobId && recordedPath = path

        let reject failure =
            WorktreeReconciliationDecision.Reject failure

        let reconcileAmbiguousEntries entries =
            let relevant =
                entries
                |> List.filter (fun (observedPath, observedIdentity) ->
                    observedPath = path || observedIdentity = Some identity)

            let matchesCanonical (observedPath, observedIdentity) =
                observedPath = path && observedIdentity = Some identity

            if List.isEmpty relevant then
                WorktreeReconciliationDecision.CreateAfterProvenMissing
            elif List.forall matchesCanonical relevant then
                WorktreeReconciliationDecision.AdoptThenRecordCreated
            else
                reject WorktreeReconciliationFailure.PhysicalIdentityPathConflict

        match observation with
        | WorktreeReconciliationObservation.NoDurableEffect -> WorktreeReconciliationDecision.RequestThenCreate
        | WorktreeReconciliationObservation.CreatedReceipt(recordedJobId, recordedPath) when
            sameOwner recordedJobId recordedPath
            ->
            WorktreeReconciliationDecision.AdoptCreated
        | WorktreeReconciliationObservation.CreatedReceipt _
        | WorktreeReconciliationObservation.RequestedConflict _ ->
            reject WorktreeReconciliationFailure.DurableOwnershipConflict
        | WorktreeReconciliationObservation.RequestedAmbiguity(recordedJobId, recordedPath, Error error) when
            not (sameOwner recordedJobId recordedPath)
            ->
            reject WorktreeReconciliationFailure.DurableOwnershipConflict
        | WorktreeReconciliationObservation.RequestedAmbiguity(_, _, Error error) ->
            reject (WorktreeReconciliationFailure.WorktreeQueryFailed error)
        | WorktreeReconciliationObservation.RequestedAmbiguity(recordedJobId, recordedPath, Ok entries) when
            not (sameOwner recordedJobId recordedPath)
            ->
            reject WorktreeReconciliationFailure.DurableOwnershipConflict
        | WorktreeReconciliationObservation.RequestedAmbiguity(_, _, Ok entries) -> reconcileAmbiguousEntries entries

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
        (payload: {| CandidateCommit: CommitHash |})
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
               TargetHeadSnapshot: CommitHash |})
        (projection: OrchestratorProjection)
        =
        // Latest-event view mirroring Fold: exact replay is idempotent and a
        // later differing record supersedes. Terminal guard stays in
        // updateActiveJob so Published still settles the effect.
        updateActiveJob
            jobId
            (fun job ->
                { job with
                    RebasedCandidateReady = Some payload })
            projection

    let recordPublishClaimed
        (jobId: ManagerJobId)
        (payload:
            {| TargetRef: TargetRef
               RebasedCommit: CommitHash
               ExpectedHead: CommitHash |})
        (projection: OrchestratorProjection)
        =
        // Latest-event view mirroring Fold: a retried publish with fresh
        // evidence supersedes the CAS-missed claim. Terminal guard stays in
        // updateActiveJob so Published still blocks replay.
        updateActiveJob
            jobId
            (fun job ->
                { job with
                    PublishClaimed = Some payload })
            projection

    let recordTerminal (jobId: ManagerJobId) (terminal: TerminalOutcome) (projection: OrchestratorProjection) =
        updateActiveJob jobId (fun job -> { job with Terminal = Some terminal }) projection

    /// ORCH-007: what a recorded rebased candidate means against the target
    /// head now.
    let classifyRebasedCandidate
        (currentHead: CommitHash option)
        (rebasedCommit: CommitHash)
        (targetHeadSnapshot: CommitHash)
        : RebasedCandidateReality =
        match currentHead with
        | None -> RebasedCandidateReality.HeadUnreadable
        | Some head when head = targetHeadSnapshot -> RebasedCandidateReality.PublishReady
        | Some _ -> RebasedCandidateReality.NeedsRebase

    /// ORCH-007: what a durable publish claim means against the target head now.
    /// Already-published is tested first, then unchanged target, then expired.
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
