namespace Wanxiangshu.Mission.Review.Barrier

open System.Threading.Tasks
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

    val openBarrier:
        journal: AgentJournal option ->
        managerSessionId: SessionId ->
        reviewerSessionId: SessionId ->
        barrierId: ReviewBarrierId ->
        tree: GitTreeHash ->
            Task<Result<unit, string>>
