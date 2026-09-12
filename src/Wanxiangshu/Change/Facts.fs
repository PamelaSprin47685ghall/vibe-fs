namespace Wanxiangshu.Change

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Foundation

/// Durable orchestration facts owned by the Change/orchestrator boundary.
type OrchestratorFactCases =
    | ManagerJobCreated of
        {| ManagerJobId: ManagerJobId
           ManagerSessionId: SessionId
           ManagerAgent: string
           Byname: string
           WorktreeIdentity: WorktreeIdentity
           WorktreePath: WorktreePath
           TargetRef: TargetRef
           TargetBranchFrozen: string |}
    | CandidateReady of
        {| ManagerJobId: ManagerJobId
           CandidateCommit: CommitHash
           WorkspaceSnapshotId: WorkspaceSnapshotId
           QualityCertificateId: QualityCertificateId |}
    | ConflictDetected of
        {| ManagerJobId: ManagerJobId
           CandidateCommit: CommitHash
           TargetHeadSnapshot: CommitHash
           WorkspaceSnapshotId: WorkspaceSnapshotId
           ConflictFiles: string list
           DiagnosticsDigest: string |}
    | RebasedCandidateReady of
        {| ManagerJobId: ManagerJobId
           RebasedCommit: CommitHash
           TargetHeadSnapshot: CommitHash
           WorkspaceSnapshotId: WorkspaceSnapshotId |}
    | PublishClaimed of
        {| ManagerJobId: ManagerJobId
           TargetRef: TargetRef
           RebasedCommit: CommitHash
           ExpectedHead: CommitHash
           WorkspaceSnapshotId: WorkspaceSnapshotId
           QualityCertificateId: QualityCertificateId
           AuthorityRevision: AuthorityRevision |}
    | Published of
        {| ManagerJobId: ManagerJobId
           CandidateCommit: CommitHash
           ResultingTargetHead: CommitHash |}
    | JobFailed of
        {| ManagerJobId: ManagerJobId
           Reason: string |}
    | JobAbandoned of {| ManagerJobId: ManagerJobId |}
    | WorktreeCreateRequested of
        {| ManagerJobId: ManagerJobId
           WorktreeIdentity: WorktreeIdentity
           WorktreePath: WorktreePath |}
    | WorktreeCreated of
        {| ManagerJobId: ManagerJobId
           WorktreeIdentity: WorktreeIdentity
           WorktreePath: WorktreePath |}

/// Fold refusals owned by the Change family: composition renders them into the
/// durable fail-closed report, the family keeps the decision (DURABLE-EVENTS-023).
[<RequireQualifiedAccess>]
type OrchestratorFoldRejection =
    | PublishClaimedWithoutRebasedCandidate
    | PublishClaimedCommitMismatch

[<RequireQualifiedAccess>]
module OrchestratorFoldRejection =
    let fact (_: OrchestratorFoldRejection) : string = "PublishClaimed"

    let message =
        function
        | OrchestratorFoldRejection.PublishClaimedWithoutRebasedCandidate ->
            "publish claimed for a job with no rebased candidate (ORCH-004)"
        | OrchestratorFoldRejection.PublishClaimedCommitMismatch ->
            "publish claimed commit does not match admitted rebased commit"
