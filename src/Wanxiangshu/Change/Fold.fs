namespace Wanxiangshu.Change

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable.ProjectionUpdate
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.ProjectionUpdate

module OrchestratorFactFold =

    let private reject = FoldRejection.reject

    /// Progress evidence → optional rebased commit for ORCH-007 publish claim.
    let private tryRebasedCommit (job: ManagerJobProjection) : CommitHash option =
        match job.Progress with
        | JobProgress.RebasedCandidateReady rebased -> Some rebased.RebasedCommit
        | _ -> None

    /// PublishClaimed evidence → fold decision (ORCH-004 requires a rebased candidate).
    let private foldPublishClaimed
        (payload: {| ManagerJobId: ManagerJobId; TargetRef: TargetRef; ExpectedHead: CommitHash |})
        (projection: AgentProjectionSet)
        : Result<AgentProjectionSet, FoldRejection> =
        match
            OrchestratorProjection.tryFind payload.ManagerJobId projection.Orchestrator
            |> Option.bind tryRebasedCommit
        with
        | None -> reject "PublishClaimed" "publish claimed for a job with no rebased candidate (ORCH-004)"
        | Some commit ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordProgress
                        payload.ManagerJobId
                        (JobProgress.PublishClaimed
                            {| RebasedCommit = commit
                               ExpectedHead = payload.ExpectedHead |}))
                    projection
            )

    let fold
        (projection: AgentProjectionSet)
        (fact: OrchestratorFactCases)
        : Result<AgentProjectionSet, FoldRejection> =
        // ── orchestrator ────────────────────────────────────────────────────
        match fact with
        | OrchestratorFactCases.ManagerJobCreated payload ->
            Ok(updateOrchestrator (OrchestratorProjection.createJob payload) projection)

        | OrchestratorFactCases.CandidateReady payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordProgress
                        payload.ManagerJobId
                        (JobProgress.CandidateReady
                            {| CandidateCommit = payload.CandidateCommit
                               PreRebaseReviewBarrierId = payload.PreRebaseReviewBarrierId |}))
                    projection
            )

        | OrchestratorFactCases.ConflictDetected payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordProgress
                        payload.ManagerJobId
                        (JobProgress.ConflictPending
                            {| CandidateCommit = payload.CandidateCommit
                               TargetHeadSnapshot = payload.TargetHeadSnapshot
                               ConflictFiles = payload.ConflictFiles
                               DiagnosticsDigest = payload.DiagnosticsDigest |}))
                    projection
            )

        | OrchestratorFactCases.RebasedCandidateReady payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordProgress
                        payload.ManagerJobId
                        (JobProgress.RebasedCandidateReady
                            {| RebasedCommit = payload.RebasedCommit
                               TargetHeadSnapshot = payload.TargetHeadSnapshot
                               PostRebaseReviewBarrierId = payload.PostRebaseReviewBarrierId |}))
                    projection
            )

        | OrchestratorFactCases.PublishClaimed payload -> foldPublishClaimed payload projection

        | OrchestratorFactCases.Published payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordProgress
                        payload.ManagerJobId
                        (JobProgress.Published
                            {| CandidateCommit = payload.CandidateCommit
                               ResultingTargetHead = payload.ResultingTargetHead |}))
                    projection
            )

        | OrchestratorFactCases.JobFailed payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordProgress payload.ManagerJobId (JobProgress.Failed payload.Reason))
                    projection
            )

        | OrchestratorFactCases.JobAbandoned payload ->
            Ok(
                updateOrchestrator
                    (OrchestratorProjection.recordProgress payload.ManagerJobId JobProgress.Abandoned)
                    projection
            )

        // ── durable effects (PERSIST-009 typed worktree) ────────────────────

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
