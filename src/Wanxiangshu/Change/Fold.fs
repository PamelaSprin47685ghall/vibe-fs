namespace Wanxiangshu.Change

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Composition.Durable.ProjectionUpdate
open Wanxiangshu.Foundation.Identity

module OrchestratorFactFold =

    let private reject = FoldRejection.reject

    let private foldPublishClaimed
        (payload:
            {| ManagerJobId: ManagerJobId
               TargetRef: TargetRef
               ExpectedHead: CommitHash |})
        (projection: AgentProjectionSet)
        : Result<AgentProjectionSet, FoldRejection> =
        match
            OrchestratorProjection.tryFind payload.ManagerJobId projection.Orchestrator
            |> Option.bind (fun job -> job.RebasedCandidateReady |> Option.map (fun r -> r.RebasedCommit))
        with
        | None -> reject "PublishClaimed" "publish claimed for a job with no rebased candidate (ORCH-004)"
        | Some commit ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordPublishClaimed
                        payload.ManagerJobId
                        {| RebasedCommit = commit
                           ExpectedHead = payload.ExpectedHead |})
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
                           PreRebaseReviewBarrierId = payload.PreRebaseReviewBarrierId |})
                    projection
            )
        | OrchestratorFactCases.ConflictDetected payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordConflictDetected
                        payload.ManagerJobId
                        {| CandidateCommit = payload.CandidateCommit
                           TargetHeadSnapshot = payload.TargetHeadSnapshot
                           ConflictFiles = payload.ConflictFiles
                           DiagnosticsDigest = payload.DiagnosticsDigest |})
                    projection
            )
        | OrchestratorFactCases.RebasedCandidateReady payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordRebasedCandidateReady
                        payload.ManagerJobId
                        {| RebasedCommit = payload.RebasedCommit
                           TargetHeadSnapshot = payload.TargetHeadSnapshot
                           PostRebaseReviewBarrierId = payload.PostRebaseReviewBarrierId |})
                    projection
            )
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
