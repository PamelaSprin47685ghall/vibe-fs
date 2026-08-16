namespace Wanxiangshu.Mission.Review.Judgement

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// One witnessed PERFECT verdict.
///
/// REVIEW-006 requires a witness to be self-contained: it must answer on its
/// own who reviewed, which tree, which provider run and which tool call.
///
/// Deliberately NO AuthorityRootUserMessageId. REVIEW-003 forbids confirming on
/// a shared authority root, and REVIEW-006's field list does not include one.
/// Carrying it "for context" is how same-root guessing gets reintroduced: once
/// the field exists, comparing it is one line away.
type VerdictWitness =
    { ProviderRun: ProviderRunIdentity
      ToolCallId: ToolCallId
      GitTreeHash: GitTreeHash
      ReviewerSessionId: SessionId }

/// Completed review evidence. Execution position never appears here: a first
/// PERFECT is a local value inside ReviewBarrierWorkflow and is not durable state.
type ReviewWitness =
    | NoReview
    | RevisionWitness of
        {| Report: string
           GitTreeHash: GitTreeHash |}
    | Confirmed of
        {| BarrierId: ReviewBarrierId
           First: VerdictWitness
           Second: VerdictWitness
           GitTreeHash: GitTreeHash
           FirstPhysicalUserMessageId: PhysicalUserMessageId
           SecondPhysicalUserMessageId: PhysicalUserMessageId |}

module ReviewWitness =

    let isConfirmed (witness: ReviewWitness) : bool =
        match witness with
        | Confirmed _ -> true
        | NoReview
        | RevisionWitness _ -> false

    let isRevision (witness: ReviewWitness) : bool =
        match witness with
        | RevisionWitness _ -> true
        | NoReview
        | Confirmed _ -> false

    let gitTreeHash (witness: ReviewWitness) : GitTreeHash option =
        match witness with
        | Confirmed confirmed -> Some confirmed.GitTreeHash
        | RevisionWitness revision -> Some revision.GitTreeHash
        | NoReview -> None

    /// The reviewer whose dual-PERFECT produced a confirmation.
    ///
    /// `None` unless confirmed. Answered from the witness rather than from a
    /// `ConfirmedReviewerSessionId` field beside it: REVIEW-005 forbids a stored
    /// flag for confirmation, and a stored reviewer id is the same mistake one
    /// step removed — it can name a reviewer while the witness says NoReview.
    let confirmedReviewer (witness: ReviewWitness) : SessionId option =
        match witness with
        | Confirmed confirmed -> Some confirmed.Second.ReviewerSessionId
        | NoReview
        | RevisionWitness _ -> None

    /// REVIEW-008: any Git tree change makes a pending challenge stale and a
    /// confirmed witness no longer sufficient for the Guard.
    ///
    /// This returns the derived predicate, not a mutation. Witness history is
    /// permanent (REVIEW-008 forbids deleting it); validity is a question asked
    /// against the current tree.
    let isValidForTree (currentTree: GitTreeHash) (witness: ReviewWitness) : bool =
        match gitTreeHash witness with
        | Some tree -> tree = currentTree
        | None -> false

    /// The attempt identity of a witnessed verdict (REVIEW-004).
    let attemptIdentity (barrierId: ReviewBarrierId) (witness: VerdictWitness) : ReviewAttemptIdentity =
        { ReviewBarrierId = barrierId
          GitTreeHash = witness.GitTreeHash
          ReviewerSessionId = witness.ReviewerSessionId
          ProviderRun = witness.ProviderRun
          ToolCallId = witness.ToolCallId }

    /// Same reviewer session, same barrier/tree, distinct provider run and tool call.
    let isDistinctAttempt (barrierId: ReviewBarrierId) (first: VerdictWitness) (second: VerdictWitness) : bool =
        ReviewAttemptIdentity.isDistinctAttempt (attemptIdentity barrierId first) (attemptIdentity barrierId second)

    /// Build completed confirmation only from the direct CE's typed physical edge.
    let confirm
        (barrierId: ReviewBarrierId)
        (firstPhysicalUserMessageId: PhysicalUserMessageId)
        (secondPhysicalUserMessageId: PhysicalUserMessageId)
        (first: VerdictWitness)
        (second: VerdictWitness)
        : ReviewWitness option =
        if
            firstPhysicalUserMessageId <> secondPhysicalUserMessageId
            || not (isDistinctAttempt barrierId first second)
        then
            None
        else
            Some(
                Confirmed
                    {| BarrierId = barrierId
                       First = first
                       Second = second
                       GitTreeHash = second.GitTreeHash
                       FirstPhysicalUserMessageId = firstPhysicalUserMessageId
                       SecondPhysicalUserMessageId = secondPhysicalUserMessageId |}
            )
