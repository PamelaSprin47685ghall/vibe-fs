namespace Wanxiangshu.Change

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Work
open Wanxiangshu.Foundation

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
           CandidateCommit: CommitHash |}
    | ConflictDetected of
        {| ManagerJobId: ManagerJobId
           CandidateCommit: CommitHash
           TargetHeadSnapshot: CommitHash
           ConflictFiles: string list
           DiagnosticsDigest: string |}
    | RebasedCandidateReady of
        {| ManagerJobId: ManagerJobId
           RebasedCommit: CommitHash
           TargetHeadSnapshot: CommitHash |}
    | PublishClaimed of
        {| ManagerJobId: ManagerJobId
           TargetRef: TargetRef
           RebasedCommit: CommitHash
           ExpectedHead: CommitHash
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

[<RequireQualifiedAccess>]
type OrchestratorFoldRejection =
    | PublishClaimedWithoutRebasedCandidate
    | PublishClaimedCommitMismatch

[<RequireQualifiedAccess>]
module OrchestratorFoldRejection =
    val fact: OrchestratorFoldRejection -> string
    val message: OrchestratorFoldRejection -> string
