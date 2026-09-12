namespace Wanxiangshu.Change

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Foundation

module OrchestratorFactFold =

    let private foldRebasedCandidateReady
        (payload:
            {| ManagerJobId: ManagerJobId
               RebasedCommit: CommitHash
               TargetHeadSnapshot: CommitHash
               WorkspaceSnapshotId: WorkspaceSnapshotId |})
        (projection: OrchestratorProjection)
        : Result<OrchestratorProjection, OrchestratorFoldRejection> =
        // Journal is append-only: the projection keeps the latest appended
        // RebasedCandidateReady as the current view. Exact replay is
        // idempotent; a later differing event supersedes because the prior
        // CAS/target observation proved that attempt did not land.
        Ok(
            OrchestratorProjection.recordRebasedCandidateReady
                payload.ManagerJobId
                {| RebasedCommit = payload.RebasedCommit
                   TargetHeadSnapshot = payload.TargetHeadSnapshot
                   WorkspaceSnapshotId = payload.WorkspaceSnapshotId |}
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
        (projection: OrchestratorProjection)
        : Result<OrchestratorProjection, OrchestratorFoldRejection> =
        match
            OrchestratorProjection.tryFind payload.ManagerJobId projection
            |> Option.bind (fun job -> job.RebasedCandidateReady)
        with
        | None -> Error OrchestratorFoldRejection.PublishClaimedWithoutRebasedCandidate
        | Some rebasedReady ->
            if payload.RebasedCommit <> rebasedReady.RebasedCommit then
                Error OrchestratorFoldRejection.PublishClaimedCommitMismatch
            else
                // Latest appended claim is the current view: a retried publish
                // under a fresh certificate carries a new ExpectedHead and
                // evidence, which supersedes the CAS-missed attempt.
                Ok(
                    OrchestratorProjection.recordPublishClaimed
                        payload.ManagerJobId
                        {| TargetRef = payload.TargetRef
                           RebasedCommit = payload.RebasedCommit
                           ExpectedHead = payload.ExpectedHead
                           WorkspaceSnapshotId = payload.WorkspaceSnapshotId
                           QualityCertificateId = payload.QualityCertificateId
                           AuthorityRevision = payload.AuthorityRevision |}
                        projection
                )

    let fold
        (orchestrator: OrchestratorProjection)
        (fact: OrchestratorFactCases)
        : Result<OrchestratorProjection, OrchestratorFoldRejection> =
        match fact with
        | OrchestratorFactCases.ManagerJobCreated payload -> Ok(OrchestratorProjection.createJob payload orchestrator)
        | OrchestratorFactCases.CandidateReady payload ->
            Ok(
                OrchestratorProjection.recordCandidateReady
                    payload.ManagerJobId
                    {| CandidateCommit = payload.CandidateCommit
                       WorkspaceSnapshotId = payload.WorkspaceSnapshotId
                       QualityCertificateId = payload.QualityCertificateId |}
                    orchestrator
            )
        | OrchestratorFactCases.ConflictDetected payload ->
            Ok(
                OrchestratorProjection.recordConflictDetected
                    payload.ManagerJobId
                    {| CandidateCommit = payload.CandidateCommit
                       TargetHeadSnapshot = payload.TargetHeadSnapshot
                       WorkspaceSnapshotId = payload.WorkspaceSnapshotId
                       ConflictFiles = payload.ConflictFiles
                       DiagnosticsDigest = payload.DiagnosticsDigest |}
                    orchestrator
            )
        | OrchestratorFactCases.RebasedCandidateReady payload -> foldRebasedCandidateReady payload orchestrator
        | OrchestratorFactCases.PublishClaimed payload -> foldPublishClaimed payload orchestrator
        | OrchestratorFactCases.Published payload ->
            Ok(
                OrchestratorProjection.recordTerminal
                    payload.ManagerJobId
                    (TerminalOutcome.Published
                        {| CandidateCommit = payload.CandidateCommit
                           ResultingTargetHead = payload.ResultingTargetHead |})
                    orchestrator
            )
        | OrchestratorFactCases.JobFailed payload ->
            Ok(
                OrchestratorProjection.recordTerminal
                    payload.ManagerJobId
                    (TerminalOutcome.Failed payload.Reason)
                    orchestrator
            )
        | OrchestratorFactCases.JobAbandoned payload ->
            Ok(OrchestratorProjection.recordTerminal payload.ManagerJobId TerminalOutcome.Abandoned orchestrator)
        | OrchestratorFactCases.WorktreeCreateRequested payload ->
            Ok(
                OrchestratorProjection.requestWorktree
                    payload.WorktreeIdentity
                    payload.WorktreePath
                    payload.ManagerJobId
                    orchestrator
            )
        | OrchestratorFactCases.WorktreeCreated payload ->
            Ok(
                OrchestratorProjection.acceptWorktree
                    payload.WorktreeIdentity
                    payload.WorktreePath
                    payload.ManagerJobId
                    orchestrator
            )
