namespace Wanxiangshu.Finality

open Wanxiangshu.Kernel.Identity

/// Outcome of one FinalityRequest drive (GLORY-044/055/060/061).
///
/// Extracted from Infrastructure `FinalityController` (rabbit §12.2) so
/// Application owns the vocabulary before the workflow body moves.
type FinalityOutcome =
    | Rejected of prompt: string
    | Blessed of prompt: string
    | Undecided of prompt: string

/// One enlisted cohort member with the durable identities the driver needs.
type EnlistedMember =
    { ReviewerSessionId: SessionId
      BarrierId: ReviewBarrierId
      ReviewerOrdinal: int
      AgentId: string }

/// One Reviewer's terminal business result for a Finality member round.
/// REVISE is a legal result (`RevisionRequired`), never an infrastructure error.
/// Canonical work-record text is the rendered LWR string (GLORY-060).
type MemberJudgement =
    | Confirmed of workRecord: string
    | RevisionRequired of workRecord: string
    | Unavailable of reason: string

/// Pure enlist inputs for one cohort slot — no Host/OpenCode surface (rabbit §12.3).
type FinalityReviewerRequest =
    { ManagerSessionId: SessionId
      LifeId: ManagerLifeId
      RequestId: FinalityRequestId
      RequestTree: GitTreeHash
      AgentId: string
      ReviewerSessionId: SessionId option
      ReviewerOrdinal: int
      IsNew: bool }
