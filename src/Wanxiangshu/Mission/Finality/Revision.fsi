namespace Wanxiangshu.Mission.Finality

open System.Threading.Tasks
open Wanxiangshu.Change
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Review
open Wanxiangshu.Persistence.Journal

/// Finality rejection, sibling accounting/steering, and durable resume.
module RevisionWorkflow =

    val tryActiveFinality:
        snapshot: ProjectionSet ->
        managerSessionId: SessionId ->
        requestId: FinalityRequestId ->
            FinalityRequestProjection option

    val rejectAndSteer:
        reviewerPort: FinalityReviewerPort ->
        journal: AgentJournal ->
        managerSessionId: SessionId ->
        lifeId: ManagerLifeId ->
        requestId: FinalityRequestId ->
        rejectingReviewer: SessionId ->
        barrierId: ReviewBarrierId ->
        requestTree: GitTreeHash ->
        siblings: (SessionId * ReviewBarrierId) list ->
            Task<FinalityOutcome>

    val steerRevisionSiblings:
        reviewerPort: FinalityReviewerPort ->
        journal: AgentJournal ->
        managerSessionId: SessionId ->
        requestId: FinalityRequestId ->
        siblings: (SessionId * ReviewBarrierId) list ->
            Task

    val pendingRevision:
        snapshot: ProjectionSet -> request: FinalityRequestProjection -> (SessionId * ReviewBarrierId) option

    val durableRevisionSiblings:
        snapshot: ProjectionSet ->
        request: FinalityRequestProjection ->
        rejectingReviewer: SessionId ->
            (SessionId * ReviewBarrierId) list

    val resumeRejectedRequest:
        reviewerPort: FinalityReviewerPort ->
        journal: AgentJournal ->
        managerSessionId: SessionId ->
        lifeId: ManagerLifeId ->
        requestId: FinalityRequestId ->
            Task<FinalityOutcome option>
