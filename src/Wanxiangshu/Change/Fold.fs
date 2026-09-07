namespace Wanxiangshu.Change

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Composition.Durable.ProjectionUpdate
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Relay

module OrchestratorFactFold =

    let private reject = FoldRejection.reject

    let private foldRebasedCandidateReady
        (payload:
            {| ManagerJobId: ManagerJobId
               RebasedCommit: CommitHash
               TargetHeadSnapshot: CommitHash
               WorkspaceSnapshotId: WorkspaceSnapshotId |})
        (projection: AgentProjectionSet)
        : Result<AgentProjectionSet, FoldRejection> =
        // Journal is append-only: the projection keeps the latest appended
        // RebasedCandidateReady as the current view. Exact replay is
        // idempotent; a later differing event supersedes because the prior
        // CAS/target observation proved that attempt did not land.
        Ok(
            updateOrchestrator
                (OrchestratorProjection.recordRebasedCandidateReady
                    payload.ManagerJobId
                    {| RebasedCommit = payload.RebasedCommit
                       TargetHeadSnapshot = payload.TargetHeadSnapshot
                       WorkspaceSnapshotId = payload.WorkspaceSnapshotId |})
                projection
        )

    let private foldPublishClaimed
        (payload:
            {| ManagerJobId: ManagerJobId
               TargetRef: TargetRef
               RebasedCommit: CommitHash
               ExpectedHead: CommitHash
               WorkspaceSnapshotId: WorkspaceSnapshotId
               QualityCertificateId: QualityCertificateId
               AuthorityRevision: AuthorityRevision |})
        (projection: AgentProjectionSet)
        : Result<AgentProjectionSet, FoldRejection> =
        match
            OrchestratorProjection.tryFind payload.ManagerJobId projection.Orchestrator
            |> Option.bind (fun job -> job.RebasedCandidateReady)
        with
        | None -> reject "PublishClaimed" "publish claimed for a job with no rebased candidate (ORCH-004)"
        | Some rebasedReady ->
            if payload.RebasedCommit <> rebasedReady.RebasedCommit then
                reject "PublishClaimed" "publish claimed commit does not match admitted rebased commit"
            else
                // Latest appended claim is the current view: a retried publish
                // under a fresh certificate carries a new ExpectedHead and
                // evidence, which supersedes the CAS-missed attempt.
                Ok(
                    updateOrchestrator
                        (OrchestratorProjection.recordPublishClaimed
                            payload.ManagerJobId
                            {| TargetRef = payload.TargetRef
                               RebasedCommit = payload.RebasedCommit
                               ExpectedHead = payload.ExpectedHead
                               WorkspaceSnapshotId = payload.WorkspaceSnapshotId
                               QualityCertificateId = payload.QualityCertificateId
                               AuthorityRevision = payload.AuthorityRevision |})
                        projection
                )

    let fold
        (projection: AgentProjectionSet)
        (fact: OrchestratorFactCases)
        : Result<AgentProjectionSet, FoldRejection> =
        match fact with
        | OrchestratorFactCases.ManagerJobCreated payload ->
            Ok(updateOrchestrator (OrchestratorProjection.createJob payload) projection)
        | OrchestratorFactCases.CandidateReady payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordCandidateReady
                        payload.ManagerJobId
                        {| CandidateCommit = payload.CandidateCommit
                           WorkspaceSnapshotId = payload.WorkspaceSnapshotId
                           QualityCertificateId = payload.QualityCertificateId |})
                    projection
            )
        | OrchestratorFactCases.ConflictDetected payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordConflictDetected
                        payload.ManagerJobId
                        {| CandidateCommit = payload.CandidateCommit
                           TargetHeadSnapshot = payload.TargetHeadSnapshot
                           WorkspaceSnapshotId = payload.WorkspaceSnapshotId
                           ConflictFiles = payload.ConflictFiles
                           DiagnosticsDigest = payload.DiagnosticsDigest |})
                    projection
            )
        | OrchestratorFactCases.RebasedCandidateReady payload -> foldRebasedCandidateReady payload projection
        | OrchestratorFactCases.PublishClaimed payload -> foldPublishClaimed payload projection
        | OrchestratorFactCases.Published payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordTerminal
                        payload.ManagerJobId
                        (TerminalOutcome.Published
                            {| CandidateCommit = payload.CandidateCommit
                               ResultingTargetHead = payload.ResultingTargetHead |}))
                    projection
            )
        | OrchestratorFactCases.JobFailed payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordTerminal payload.ManagerJobId (TerminalOutcome.Failed payload.Reason))
                    projection
            )
        | OrchestratorFactCases.JobAbandoned payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordTerminal payload.ManagerJobId TerminalOutcome.Abandoned)
                    projection
            )
        | OrchestratorFactCases.WorktreeCreateRequested payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.requestWorktree
                        payload.WorktreeIdentity
                        payload.WorktreePath
                        payload.ManagerJobId)
                    projection
            )
        | OrchestratorFactCases.WorktreeCreated payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.acceptWorktree
                        payload.WorktreeIdentity
                        payload.WorktreePath
                        payload.ManagerJobId)
                    projection
            )
