namespace Wanxiangshu.Next.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity

module OrchestratorHostReview =

    /// Host-owned post-rebase reviewer identity. Explicit deep tier; never inferred.
    let DeepReviewerAgent = ManagedAgent.nameOf AgentTier.Deep Role.Reviewer

    let reverify
        (journal: AgentJournal option)
        (orchestratorId: SessionId)
        (runReviewerOnce: string -> string -> string -> Task<Result<unit, string>>)
        (managerId: string)
        (worktree: string)
        (barrierKey: string)
        : Task<Result<unit, string>> =
        task {
            // OrchestratorHost forks the reviewer under the Orchestrator runtime,
            // so sessionParents[reviewer] = orchestrator and ReviewVerdictRecorded
            // lands on the Orchestrator session. Barriers and reads must use that
            // same durable session — managerId is only a job alias, not a Host
            // session id.
            //
            // Reviewer identity is always DeepReviewerAgent (deep-reviewer).
            let reviewOwnerSessionId = orchestratorId
            let! barrierResult = OrchestratorManagerJob.emitReviewBarrier journal reviewOwnerSessionId barrierKey

            match barrierResult with
            | Error err -> return Error err
            | Ok() ->
                let! priorState = OrchestratorReviewRead.read journal reviewOwnerSessionId worktree

                match priorState with
                | Error err -> return Error err
                | Ok OrchestratorReviewRead.Confirmed -> return Ok()
                | Ok OrchestratorReviewRead.RevisionRequired -> return Error "Reviewer requested revision"
                | Ok OrchestratorReviewRead.PendingConfirmation
                | Ok OrchestratorReviewRead.NeedsReview ->
                    // One reviewer assignment covers first PERFECT + HostReviewGuard
                    // confirmation + second PERFECT. The first PERFECT does not
                    // complete the run until confirmation finishes, so this await
                    // spans the full double-PERFECT barrier.
                    let prompt =
                        match priorState with
                        | Ok OrchestratorReviewRead.PendingConfirmation -> ReviewChallenge.Text
                        | _ -> "Review the current worktree for correctness. Submit your verdict with the verdict tool."

                    let! ran = runReviewerOnce managerId worktree prompt

                    match ran with
                    | Error err -> return Error err
                    | Ok() ->
                        let! state = OrchestratorReviewRead.read journal reviewOwnerSessionId worktree

                        match state with
                        | Error err -> return Error err
                        | Ok OrchestratorReviewRead.Confirmed -> return Ok()
                        | Ok OrchestratorReviewRead.RevisionRequired -> return Error "Reviewer requested revision"
                        | Ok OrchestratorReviewRead.PendingConfirmation ->
                            let! nudged = runReviewerOnce managerId worktree ReviewChallenge.Text

                            match nudged with
                            | Error err -> return Error err
                            | Ok() ->
                                let! retry = OrchestratorReviewRead.read journal reviewOwnerSessionId worktree

                                match retry with
                                | Error err -> return Error err
                                | Ok OrchestratorReviewRead.Confirmed -> return Ok()
                                | Ok OrchestratorReviewRead.RevisionRequired ->
                                    return Error "Reviewer requested revision"
                                | Ok _ -> return Error "Reviewer produced no confirmed verdict"
                        | Ok OrchestratorReviewRead.NeedsReview ->
                            let! nudged =
                                runReviewerOnce
                                    managerId
                                    worktree
                                    "You produced no verdict. Submit your verdict with the verdict tool."

                            match nudged with
                            | Error err -> return Error err
                            | Ok() ->
                                let! retry = OrchestratorReviewRead.read journal reviewOwnerSessionId worktree

                                match retry with
                                | Error err -> return Error err
                                | Ok OrchestratorReviewRead.Confirmed -> return Ok()
                                | Ok OrchestratorReviewRead.RevisionRequired ->
                                    return Error "Reviewer requested revision"
                                | Ok _ -> return Error "Reviewer produced no confirmed verdict"
        }
