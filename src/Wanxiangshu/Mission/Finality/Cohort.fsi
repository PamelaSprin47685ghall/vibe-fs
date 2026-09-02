namespace Wanxiangshu.Mission.Finality

open System.Threading.Tasks
open Wanxiangshu.Change
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type CohortJudgement =
    | RevisionRequired of
        rejectingReviewer: SessionId *
        barrierId: ReviewBarrierId *
        siblings: (SessionId * ReviewBarrierId) list
    | AllConfirmed

/// Finality cohort enlistment and temporal short-circuit vocabulary.
module CohortWorkflow =

    val enlistRequiredReviewers:
        reviewerPort: FinalityReviewerPort ->
        journal: AgentJournal ->
        managerSessionId: SessionId ->
        life: LifeProjection ->
        request: FinalityRequestProjection ->
            Task<Result<EnlistedMember list, string>>

    val reviewUntilFirstRevisionOrAllConfirmed:
        reviewerPort: FinalityReviewerPort ->
        journal: AgentJournal ->
        managerSessionId: SessionId ->
        members: EnlistedMember list ->
        requestTree: GitTreeHash ->
            Task<Result<CohortJudgement, ReviewBarrierFailure>>
