namespace Wanxiangshu.Change.Orchestration

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal.ProjectionUpdate

module OrchestratorFactFold =

    let private reject = FoldRejection.reject

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

        | OrchestratorFactCases.PublishClaimed payload ->
            // ORCH-007 needs the rebased commit to recognise "already published".
            // It comes from the job's current progress rather than the claim
            // fact, because the claim is written inside the CAS window where the
            // rebased candidate is already established.
            let rebasedCommit =
                OrchestratorProjection.tryFind payload.ManagerJobId projection.Orchestrator
                |> Option.bind (fun job ->
                    match job.Progress with
                    | JobProgress.RebasedCandidateReady rebased -> Some rebased.RebasedCommit
                    | _ -> None)

            match rebasedCommit with
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
