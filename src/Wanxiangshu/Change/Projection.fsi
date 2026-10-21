namespace Wanxiangshu.Change

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
             ExpectedHead: CommitHash
             AuthorityRevision: AuthorityRevision |} option
      Terminal: TerminalOutcome option }

[<RequireQualifiedAccess>]
type WorktreeEffectStatus =
    | Requested of
        {| ManagerJobId: ManagerJobId
           WorktreePath: WorktreePath |}
    | Created of
        {| ManagerJobId: ManagerJobId
           WorktreePath: WorktreePath |}

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

/// wanxiangli/integration binding (CUT-23 ②). The library is the only owner of
/// commission worktree binding, the publish gate's verdicts, conflict ownership
/// and crash-recovery resume; this module converts F# values in and hands back
/// the library's step/code vocabulary. Callers keep only the I/O and its order.
module ChangeIntegrationLaw =
    /// CleanUp | ReenterClaim | RunLoop
    val resume: record: ManagerJobProjection option -> string

    /// TargetMismatch | MissingRecord | MissingRebased | VerifyHead | RunLoop
    val reenterClaim:
        jobTargetRef: TargetRef ->
        claimTargetRef: TargetRef ->
        claimRebased: CommitHash ->
        claimExpected: CommitHash ->
        record: ManagerJobProjection option ->
            string

    /// PublishCertified | TargetAdvanced | InitialRebaseRequired
    val admitCandidate:
        candidate: CommitHash ->
        target: CommitHash ->
        rebased:
            {| RebasedCommit: CommitHash
               TargetHeadSnapshot: CommitHash |} option ->
            string

    /// Ok | MissingRebased | CandidatePin | SnapshotPin
    val certify:
        candidate: CommitHash ->
        expectedHead: CommitHash ->
        rebased:
            {| RebasedCommit: CommitHash
               TargetHeadSnapshot: CommitHash |} option ->
            string

    /// RecordConflict | Fail
    val rebaseFailed: conflictFiles: string list -> string

    /// Ok | JobMismatch | TargetMismatch | CasMismatch
    val checkPublication:
        jobId: ManagerJobId ->
        jobTargetRef: TargetRef ->
        evidenceJobId: ManagerJobId ->
        evidenceTargetRef: TargetRef ->
        evidenceExpectedHead: CommitHash ->
        current: CommitHash ->
            string

    /// Landed | LandedMismatch | TargetMoved | Failed
    val ffMerge: merge: Result<CommitHash, string> -> candidate: CommitHash -> targetMovedError: string -> string

module OrchestratorProjection =
    val empty: OrchestratorProjection
    val tryFind: ManagerJobId -> OrchestratorProjection -> ManagerJobProjection option
    val tryFindByByname: string -> OrchestratorProjection -> ManagerJobProjection option
    val tryWorktreeEffect: WorktreeIdentity -> OrchestratorProjection -> WorktreeEffectStatus option

    val decideWorktreeReconciliation:
        ManagerJobId ->
        WorktreeIdentity ->
        WorktreePath ->
        WorktreeReconciliationObservation ->
            WorktreeReconciliationDecision

    val requestWorktree:
        WorktreeIdentity -> WorktreePath -> ManagerJobId -> OrchestratorProjection -> OrchestratorProjection

    val acceptWorktree:
        WorktreeIdentity -> WorktreePath -> ManagerJobId -> OrchestratorProjection -> OrchestratorProjection

    val tryFindByManagerSession: SessionId -> OrchestratorProjection -> ManagerJobProjection option
    val activeJobs: OrchestratorProjection -> ManagerJobProjection list

    val createJob:
        {| ManagerJobId: ManagerJobId
           ManagerSessionId: SessionId
           ManagerAgent: string
           Byname: string
           WorktreeIdentity: WorktreeIdentity
           WorktreePath: WorktreePath
           TargetRef: TargetRef
           TargetBranchFrozen: string |} ->
        OrchestratorProjection ->
            OrchestratorProjection

    val recordCandidateReady:
        ManagerJobId -> {| CandidateCommit: CommitHash |} -> OrchestratorProjection -> OrchestratorProjection

    val recordConflictDetected:
        ManagerJobId ->
        {| CandidateCommit: CommitHash
           TargetHeadSnapshot: CommitHash
           ConflictFiles: string list
           DiagnosticsDigest: string |} ->
        OrchestratorProjection ->
            OrchestratorProjection

    val recordRebasedCandidateReady:
        ManagerJobId ->
        {| RebasedCommit: CommitHash
           TargetHeadSnapshot: CommitHash |} ->
        OrchestratorProjection ->
            OrchestratorProjection

    val recordPublishClaimed:
        ManagerJobId ->
        {| TargetRef: TargetRef
           RebasedCommit: CommitHash
           ExpectedHead: CommitHash
           AuthorityRevision: AuthorityRevision |} ->
        OrchestratorProjection ->
            OrchestratorProjection

    val recordTerminal: ManagerJobId -> TerminalOutcome -> OrchestratorProjection -> OrchestratorProjection
    val classifyRebasedCandidate: CommitHash option -> CommitHash -> CommitHash -> RebasedCandidateReality
    val classifyPublishClaim: CommitHash option -> CommitHash -> CommitHash -> PublishClaimReality
