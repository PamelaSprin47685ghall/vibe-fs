namespace Wanxiangshu.Mission.Review.Judgement

open Wanxiangshu.Persistence.Journal

/// Reviewer-side reads of the durable review guard.
///
/// Keyed by reviewer session, which is where REVIEW-003's facts land.
module ReviewerEvidence =

    /// Whether the barrier still authorizes reviewer continuation. Finality may
    /// stop waiting after a sibling REVISE; that closed request must also revoke
    /// the reviewer's challenge capability. Non-Finality review owners have no
    /// ManagerLife projection and remain eligible.
    val continuationOpen: journal: AgentJournal option -> reviewerKey: string -> bool
