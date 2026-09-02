namespace Wanxiangshu.Composition.Bridges.FinalityReview

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Manager.Life

/// GLORY-043/044/045: the pure cohort algebra of one FinalityRequest.
///
/// The roster is real history (durable enlistments + confirmed witnesses); the
/// member decision is a local result of one Reviewer's protocol. Neither is a
/// program counter (GLORY-009).
module FinalityReviewCohort =

    /// GLORY-043: one Reviewer's terminal business result. Infrastructure
    /// failures are the only Error-like branch; REVISE is a legal result.
    [<RequireQualifiedAccess>]
    type ReviewerOutcome =
        | Revision of workRecord: string
        | Confirmed of reviewerSessionId: SessionId * barrierId: ReviewBarrierId

    /// One roster slot: a still-ungraduated historical Reviewer (session must
    /// be reused, GLORY-045) or the request's one new Reviewer.
    type CohortSlot =
        { AgentId: string
          ReviewerSessionId: SessionId option
          ReviewerOrdinal: int
          IsNew: bool }

    /// GLORY-045: a Reviewer graduated iff it has a confirmed witness on one of
    /// the barriers this Life enlisted it on. Derived from durable facts only.
    val graduatedReviewer:
        snapshot: AgentProjectionSet -> reviewerSessionId: SessionId -> standing: ReviewerStanding -> bool

    /// GLORY-003/045: the roster of a new FinalityRequest =
    /// all still-ungraduated historical Reviewers of this Life
    /// + exactly one new Reviewer. The new Reviewer's ordinal is the next
    /// stable position (max enlisted ordinal + 1; 0 when the Life has none).
    val rosterOf:
        snapshot: AgentProjectionSet -> life: LifeProjection -> request: FinalityRequestProjection -> CohortSlot list
