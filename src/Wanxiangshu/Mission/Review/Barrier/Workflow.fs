namespace Wanxiangshu.Mission.Review.Barrier

open Wanxiangshu.Composition.Durable

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Mission.Review
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// REVIEW-003: the single writer of `ReviewBarrierStarted`.
///
/// Emitted from the reviewer fork path, once the child session exists — the fact
/// carries `ReviewerSessionId` and the fold keys `ReviewGuardProjection` by it, so
/// a barrier opened before the fork has nothing to key. One reviewer session per
/// barrier also makes REVIEW-008's "a fresh dual PERFECT" automatic: that
/// session's guard starts empty, so no earlier witness can satisfy it.
///
/// Both fork paths call this: the Orchestrator's review barrier (ORCH-006) and a
/// Manager's own guard-path review fork (REVIEW-007). Before this existed the
/// barrier was written only by the Orchestrator, so a Manager-forked Reviewer's
/// verdict was always refused with "no review barrier is open" (REVIEW-008 fail
/// closed) and the guard path could never reach a confirmed double PERFECT.
module ReviewBarrier =

    let private appendReviewBarrierFact (durable: AgentJournal) (reviewerSessionId: SessionId) fact =
        task {
            match! AgentJournal.appendAgent (StreamId.Session reviewerSessionId) None fact durable with
            | Ok value -> return Ok value
            | Error failure -> return Error(JournalAppendFailure.describe failure)
        }

    let openBarrier
        (journal: AgentJournal option)
        (managerSessionId: SessionId)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (tree: GitTreeHash)
        : Task<Result<unit, string>> =
        taskResult {
            match journal with
            | None -> return! Error "Review barrier requires an AgentJournal"
            | Some durable ->
                let fact =
                    ReviewFact.ReviewBarrierStarted
                        {| ReviewerSessionId = reviewerSessionId
                           ManagerSessionId = managerSessionId
                           BarrierId = barrierId
                           GitTreeHash = tree |}

                let! _ = appendReviewBarrierFact durable reviewerSessionId fact
                return ()
        }
