namespace Wanxiangshu.Mission.Review.Barrier

open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement

type ReviewTerminalFrontier =
    { BarrierId: ReviewBarrierId
      TerminalRef: BlobRef
      TerminalDigest: BlobDigest
      Sequence: int64 }

type ClosedAttempt =
    { Attempt: ReviewAttemptIdentity
      FrozenFrontier: XTraceCursor }

type ReviewGuardProjection =
    { CurrentBarrierId: ReviewBarrierId option
      CurrentManagerSessionId: SessionId option
      LastGitTreeHash: GitTreeHash option
      Witness: ReviewWitness
      TerminalFrontier: ReviewTerminalFrontier option
      ObservedAttempts: ReviewAttemptIdentity list
      ClosedAttempts: ClosedAttempt list }

    member IsConfirmed: bool

type ReviewRequirementInput =
    { SourceSessionId: SessionId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId }

type ReviewRequirementProjection =
    { HumanPromptInputs: ReviewRequirementInput list
      InputKeys: Set<string>
      LastConfirmedProviderRun: ProviderRunIdentity option }

type VerdictRejection =
    | DuplicateAttempt
    | NotDistinctAttempt

module ReviewProjection =
    val empty: ReviewGuardProjection

    val startBarrier:
        managerSessionId: SessionId ->
        barrierId: ReviewBarrierId ->
        gitTreeHash: GitTreeHash ->
        current: ReviewGuardProjection ->
            ReviewGuardProjection

    val recordTerminalFrontier:
        terminalRef: BlobRef ->
        terminalDigest: BlobDigest ->
        sequence: int64 ->
        current: ReviewGuardProjection ->
            ReviewGuardProjection

    val recordClosedAttemptFrontier:
        terminalRef: BlobRef ->
        terminalDigest: BlobDigest ->
        closed: ClosedAttempt ->
        current: ReviewGuardProjection ->
            ReviewGuardProjection

    val applyVerdict:
        attempt: ReviewAttemptIdentity ->
        verdict: ReviewGuardVerdict ->
        current: ReviewGuardProjection ->
            Result<ReviewGuardProjection, VerdictRejection>

    val applyConfirmedWitness:
        barrierId: ReviewBarrierId ->
        firstPhysicalUserMessageId: PhysicalUserMessageId ->
        secondPhysicalUserMessageId: PhysicalUserMessageId ->
        first: VerdictWitness ->
        second: VerdictWitness ->
        current: ReviewGuardProjection ->
            Result<ReviewGuardProjection, VerdictRejection>

    val hasObservedAttempt: attempt: ReviewAttemptIdentity -> current: ReviewGuardProjection -> bool
    val latestObservedAttempt: current: ReviewGuardProjection -> ReviewAttemptIdentity option
    val applyAttemptClosed: closed: ClosedAttempt -> current: ReviewGuardProjection -> ReviewGuardProjection
    val closedAttemptOf: attempt: ReviewAttemptIdentity -> current: ReviewGuardProjection -> ClosedAttempt option
    val satisfiesGuard: currentTree: GitTreeHash -> current: ReviewGuardProjection -> bool

module ReviewRequirementProjection =
    val empty: ReviewRequirementProjection
    val inputs: current: ReviewRequirementProjection -> ReviewRequirementInput list

    val addRequirement:
        sourceSessionId: SessionId ->
        authorityRoot: AuthorityRootUserMessageId ->
        current: ReviewRequirementProjection ->
            ReviewRequirementProjection

    val clearOnConfirmation:
        providerRun: ProviderRunIdentity ->
        current: ReviewRequirementProjection ->
            ReviewRequirementProjection
