// primary_owner: review-judgement — Review.Judgement.Workflow — KEEP — Review Fact judgement workflow surface
namespace Wanxiangshu.Mission.Review

open Wanxiangshu.Composition.Durable.Fact

/// Review fact constructors — bridge from Review-owned ReviewFactCases
/// into the Composition-owned AgentFact outer routing union.
module ReviewFact =
    let inline ReviewBarrierStarted payload =
        AgentFact.Review(ReviewFactCases.ReviewBarrierStarted payload)

    let inline ReviewVerdictRecorded payload =
        AgentFact.Review(ReviewFactCases.ReviewVerdictRecorded payload)

    let inline ReviewAttemptClosed payload =
        AgentFact.Review(ReviewFactCases.ReviewAttemptClosed payload)

    let inline ConfirmedReviewWitness payload =
        AgentFact.Review(ReviewFactCases.ConfirmedReviewWitness payload)
