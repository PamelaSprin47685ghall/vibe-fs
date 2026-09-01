namespace Wanxiangshu.Mission.Review.Judgement

open Wanxiangshu.Foundation.Identity

type VerdictWitness =
    { ProviderRun: ProviderRunIdentity
      ToolCallId: ToolCallId
      GitTreeHash: GitTreeHash
      ReviewerSessionId: SessionId }

type ReviewWitness =
    | NoReview
    | RevisionWitness of {| Report: string; GitTreeHash: GitTreeHash |}
    | Confirmed of
        {| BarrierId: ReviewBarrierId
           First: VerdictWitness
           Second: VerdictWitness
           GitTreeHash: GitTreeHash
           FirstPhysicalUserMessageId: PhysicalUserMessageId
           SecondPhysicalUserMessageId: PhysicalUserMessageId |}

module ReviewWitness =
    val isConfirmed: witness: ReviewWitness -> bool
    val isRevision: witness: ReviewWitness -> bool
    val gitTreeHash: witness: ReviewWitness -> GitTreeHash option
    val confirmedReviewer: witness: ReviewWitness -> SessionId option
    val isValidForTree: currentTree: GitTreeHash -> witness: ReviewWitness -> bool
    val attemptIdentity: barrierId: ReviewBarrierId -> witness: VerdictWitness -> ReviewAttemptIdentity
    val isDistinctAttempt: barrierId: ReviewBarrierId -> first: VerdictWitness -> second: VerdictWitness -> bool
    val isQualifiedConfirmationFor:
        reviewerSessionId: SessionId ->
        barrierId: ReviewBarrierId ->
        gitTreeHash: GitTreeHash ->
        witness: ReviewWitness ->
        bool
    val confirm:
        barrierId: ReviewBarrierId ->
        firstPhysicalUserMessageId: PhysicalUserMessageId ->
        secondPhysicalUserMessageId: PhysicalUserMessageId ->
        first: VerdictWitness ->
        second: VerdictWitness ->
        ReviewWitness option

type ConfirmedReviewWitness =
    private
    | ConfirmedReviewWitness of
        {| LifeId: ManagerLifeId
           RequestId: FinalityRequestId
           GitTreeHash: GitTreeHash
           Witnesses: (SessionId * ReviewBarrierId * ReviewWitness) list |}

type CandidateVerificationFailure =
    | StaleWitness of candidateTree: GitTreeHash * witnessTree: GitTreeHash
    | IncompleteCohort of reason: string

module ConfirmedReviewWitness =
    val gitTreeHash: ConfirmedReviewWitness -> GitTreeHash
    val lifeId: ConfirmedReviewWitness -> ManagerLifeId
    val requestId: ConfirmedReviewWitness -> FinalityRequestId
    val witnesses: ConfirmedReviewWitness -> (SessionId * ReviewBarrierId * ReviewWitness) list
    val create:
        lifeId: ManagerLifeId ->
        requestId: FinalityRequestId ->
        gitTreeHash: GitTreeHash ->
        memberWitnesses: (SessionId * ReviewBarrierId * ReviewWitness) list ->
        Result<ConfirmedReviewWitness, string>

module ReviewCandidate =
    val verifyCandidate:
        candidateTree: GitTreeHash ->
        witness: ConfirmedReviewWitness ->
        Result<unit, CandidateVerificationFailure>
    val isWitnessValidForTree: candidateTree: GitTreeHash -> witness: ConfirmedReviewWitness -> bool
