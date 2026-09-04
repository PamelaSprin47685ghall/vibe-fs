namespace Wanxiangshu.Change

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Relay

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
           ExpectedHead: CommitHash |}
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
