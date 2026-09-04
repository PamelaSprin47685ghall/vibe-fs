namespace Wanxiangshu.Change

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
        ManagerJobId ->
        {| CandidateCommit: CommitHash
           WorkspaceSnapshotId: WorkspaceSnapshotId
           QualityCertificateId: QualityCertificateId |} ->
        OrchestratorProjection ->
            OrchestratorProjection

    val recordConflictDetected:
        ManagerJobId ->
        {| CandidateCommit: CommitHash
           TargetHeadSnapshot: CommitHash
           WorkspaceSnapshotId: WorkspaceSnapshotId
           ConflictFiles: string list
           DiagnosticsDigest: string |} ->
        OrchestratorProjection ->
            OrchestratorProjection

    val recordRebasedCandidateReady:
        ManagerJobId ->
        {| RebasedCommit: CommitHash
           TargetHeadSnapshot: CommitHash
           WorkspaceSnapshotId: WorkspaceSnapshotId |} ->
        OrchestratorProjection ->
            OrchestratorProjection

    val recordPublishClaimed:
        ManagerJobId ->
        {| RebasedCommit: CommitHash
           ExpectedHead: CommitHash |} ->
        OrchestratorProjection ->
            OrchestratorProjection

    val recordTerminal: ManagerJobId -> TerminalOutcome -> OrchestratorProjection -> OrchestratorProjection
    val classifyRebasedCandidate: CommitHash option -> CommitHash -> CommitHash -> RebasedCandidateReality
    val classifyPublishClaim: CommitHash option -> CommitHash -> CommitHash -> PublishClaimReality
