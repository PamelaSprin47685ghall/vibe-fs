namespace Wanxiangshu.Mission.Finality

open Wanxiangshu.Change
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Strength.Replica

open Wanxiangshu.Foundation.Identity

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
      AgentId: string
      IsNew: bool }

/// Physical reviewer session prepared by the Host before Application records
/// enlistment and opens the barrier.
type PreparedReviewer =
    { ReviewerSessionId: SessionId
      IsNew: bool }

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
