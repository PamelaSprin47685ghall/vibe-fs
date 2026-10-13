namespace Wanxiangshu.Change

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Work

module OrchestratorFact =
    val inline ManagerJobCreated:
        payload:
            {| ManagerJobId: ManagerJobId
               ManagerSessionId: SessionId
               ManagerAgent: string
               Byname: string
               WorktreeIdentity: WorktreeIdentity
               WorktreePath: WorktreePath
               TargetRef: TargetRef
               TargetBranchFrozen: string |} ->
            AgentFact

    val inline CandidateReady:
        payload:
            {| ManagerJobId: ManagerJobId
               CandidateCommit: CommitHash |} ->
            AgentFact

    val inline ConflictDetected:
        payload:
            {| ManagerJobId: ManagerJobId
               CandidateCommit: CommitHash
               TargetHeadSnapshot: CommitHash
               ConflictFiles: string list
               DiagnosticsDigest: string |} ->
            AgentFact

    val inline RebasedCandidateReady:
        payload:
            {| ManagerJobId: ManagerJobId
               RebasedCommit: CommitHash
               TargetHeadSnapshot: CommitHash |} ->
            AgentFact

    val inline PublishClaimed:
        payload:
            {| ManagerJobId: ManagerJobId
               TargetRef: TargetRef
               RebasedCommit: CommitHash
               ExpectedHead: CommitHash
               AuthorityRevision: AuthorityRevision |} ->
            AgentFact

    val inline Published:
        payload:
            {| ManagerJobId: ManagerJobId
               CandidateCommit: CommitHash
               ResultingTargetHead: CommitHash |} ->
            AgentFact

    val inline JobFailed:
        payload:
            {| ManagerJobId: ManagerJobId
               Reason: string |} ->
            AgentFact

    val inline JobAbandoned: payload: {| ManagerJobId: ManagerJobId |} -> AgentFact

    val inline WorktreeCreateRequested:
        payload:
            {| ManagerJobId: ManagerJobId
               WorktreeIdentity: WorktreeIdentity
               WorktreePath: WorktreePath |} ->
            AgentFact

    val inline WorktreeCreated:
        payload:
            {| ManagerJobId: ManagerJobId
               WorktreeIdentity: WorktreeIdentity
               WorktreePath: WorktreePath |} ->
            AgentFact
