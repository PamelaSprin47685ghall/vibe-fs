namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

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

/// Review state, derived only from witnessed verdicts.
///
/// REVIEW-005 forbids a persisted boolean for "confirmed": confirmation is a
/// property of the evidence, so it is a union case carrying that evidence
/// rather than a flag next to it.
type ReviewWitness =
    | NoReview
    | RevisionWitness of
        {| Report: string
           GitTreeHash: GitTreeHash |}
    | PerfectPending of first: VerdictWitness
    | Confirmed of
        {| BarrierId: ReviewBarrierId
           First: VerdictWitness
           Second: VerdictWitness
           GitTreeHash: GitTreeHash
           ChallengeResultDigest: SealDigest
           SecondProviderInputDigest: SealDigest |}

module ReviewWitness =

    let isConfirmed (witness: ReviewWitness) : bool =
        match witness with
        | Confirmed _ -> true
        | NoReview
        | RevisionWitness _
        | PerfectPending _ -> false

    let isPerfectPending (witness: ReviewWitness) : bool =
        match witness with
        | PerfectPending _ -> true
        | NoReview
        | RevisionWitness _
        | Confirmed _ -> false

    let isRevision (witness: ReviewWitness) : bool =
        match witness with
        | RevisionWitness _ -> true
        | NoReview
        | PerfectPending _
        | Confirmed _ -> false

    let gitTreeHash (witness: ReviewWitness) : GitTreeHash option =
        match witness with
        | Confirmed confirmed -> Some confirmed.GitTreeHash
        | PerfectPending pending -> Some pending.GitTreeHash
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
        | RevisionWitness _
        | PerfectPending _ -> None

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

    /// REVIEW-003 conditions 1-5: same reviewer session, same barrier, same
    /// tree, different provider run, different tool call.
    ///
    /// This is necessary but NOT sufficient. Condition 6 — the second provider
    /// input seal demonstrably contains the first challenge result — is the
    /// causal proof, and it lives with the seal (REVIEW-010), not here. A
    /// witness pair passing this check is a candidate, not a confirmation.
    let isDistinctAttempt (barrierId: ReviewBarrierId) (first: VerdictWitness) (second: VerdictWitness) : bool =
        ReviewAttemptIdentity.isDistinctAttempt (attemptIdentity barrierId first) (attemptIdentity barrierId second)

    /// Build a confirmed witness from a proven pair.
    ///
    /// The causal proof is a parameter this function cannot fabricate: the caller
    /// must already hold the challenge digest and the second run's input digest,
    /// and must have checked that the former appears in the latter's seal
    /// (REVIEW-010). Passing the digests rather than a boolean means the witness
    /// carries its own evidence, so REVIEW-006's self-containment does not
    /// depend on a caller remembering to copy them.
    let confirm
        (barrierId: ReviewBarrierId)
        (challengeResultDigest: SealDigest)
        (secondProviderInputDigest: SealDigest)
        (first: VerdictWitness)
        (second: VerdictWitness)
        : ReviewWitness option =
        if not (isDistinctAttempt barrierId first second) then
            None
        else
            Some(
                Confirmed
                    {| BarrierId = barrierId
                       First = first
                       Second = second
                       GitTreeHash = second.GitTreeHash
                       ChallengeResultDigest = challengeResultDigest
                       SecondProviderInputDigest = secondProviderInputDigest |}
            )
