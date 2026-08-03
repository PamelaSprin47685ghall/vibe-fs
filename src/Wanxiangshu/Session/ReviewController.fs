namespace Wanxiangshu.Session

open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal

/// One `verdict` tool call, with every identity it needs supplied by the caller.
///
/// A record rather than ten positional parameters: `ProviderRunIdentity`,
/// `ToolCallId` and the two session ids are all distinct types now, but
/// `managerJobId` / `worktreeIdentity` are both optional and adjacent, which is
/// exactly where positional arguments get silently swapped.
type VerdictSubmission =
    { BarrierId: ReviewBarrierId
      GitTreeHash: GitTreeHash
      ManagerSessionId: SessionId
      ReviewerSessionId: SessionId
      ManagerJobId: ManagerJobId option
      WorktreeIdentity: WorktreeIdentity option
      ProviderRun: ProviderRunIdentity
      ToolCallId: ToolCallId
      Verdict: ReviewGuardVerdict }

/// What a submitted verdict resolved to.
///
/// REVIEW-005 restricts the second-PERFECT judgement to exactly three answers,
/// and this is them plus the two first-call outcomes. There is deliberately no
/// `Confirmed of bool`-shaped case: `ChallengeUnproven` and `AlreadyCounted` are
/// both "not confirmed" for different reasons, and only one of them is a
/// REVIEW-003 violation worth surfacing.
[<RequireQualifiedAccess>]
type VerdictDecision =
    /// REVIEW-002: recorded and any pending PERFECT is cleared.
    | Revised
    /// REVIEW-003: first PERFECT. Carries the challenge the reviewer must be
    /// shown as the tool result — the same text whose digest was journalled.
    | ChallengeIssued of challenge: string
    /// REVIEW-003: second PERFECT with proven causal evidence.
    | Confirmed
    /// REVIEW-005 `Rejected`: no seal binds this run to the challenge. Nothing
    /// was journalled; the reviewer must re-evaluate.
    | ChallengeUnproven
    /// REVIEW-004: this attempt, or another PERFECT inside the same provider run,
    /// already counted. Not journalled.
    | AlreadyCounted

/// REVIEW-003/006/010: the only writer of `PerfectChallengeIssued` and
/// `ConfirmedReviewWitness`.
///
/// Every judgement happens here, before anything is appended. That ordering is
/// forced rather than stylistic: `Fold.verdictOutcome` REJECTS a
/// `ReviewVerdictRecorded` whose PERFECT cannot be proven causal, so a writer
/// that appended first and inspected afterwards would turn a real reviewer
/// action into a journal write failure.
module ReviewController =

    /// REVIEW-003 condition 6, asked of the seal rather than of the root.
    ///
    /// Returns the seal so the caller can put its digest inside the witness. A
    /// boolean here would leave `SecondProviderInputDigest` to be fetched again
    /// by whoever builds the witness, i.e. a second lookup that can disagree.
    let private provenSeal
        (challenge: PerfectChallenge)
        (providerRun: ProviderRunIdentity)
        (guard: ReviewGuardProjection)
        =
        match Map.tryFind providerRun guard.Seals with
        // HOST-010: a transform output that could not be bound to this provider
        // run means there is no seal, and no seal means fail closed. Never fall
        // back to comparing authority roots or physical message ids.
        | None -> None
        | Some seal ->
            if Set.contains (SealDigest.value challenge.ChallengeContentDigest) seal.IncludedToolResultDigests then
                Some seal
            else
                None

    let private append (sessionId: SessionId) (providerRun: ProviderRunIdentity) fact journal =
        match AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) fact journal with
        | Ok updated -> Ok updated
        | Error failure -> Error(JournalAppendFailure.describe failure)

    let private verdictFact (submission: VerdictSubmission) =
        AgentFact.ReviewVerdictRecorded
            {| ReviewerSessionId = submission.ReviewerSessionId
               ManagerSessionId = submission.ManagerSessionId
               BarrierId = submission.BarrierId
               GitTreeHash = submission.GitTreeHash
               ProviderRun = submission.ProviderRun
               ToolCallId = submission.ToolCallId
               Verdict = submission.Verdict |}

    /// Record one verdict.
    ///
    /// `sha256` is a parameter because this module is pure domain logic and
    /// PROMPT/REVIEW digests must be reproducible in tests without a host.
    let submit
        (journal: AgentJournal)
        (sha256: string -> string)
        (submission: VerdictSubmission)
        : Result<VerdictDecision, string> =
        let snapshot = AgentJournal.snapshot journal

        let guard =
            AgentProjection.tryFind submission.ReviewerSessionId snapshot.AgentProjections
            |> Option.bind (fun session -> session.ReviewGuard)
            |> Option.defaultValue ReviewProjection.empty

        let attempt: ReviewAttemptIdentity =
            { ReviewBarrierId = submission.BarrierId
              GitTreeHash = submission.GitTreeHash
              ReviewerSessionId = submission.ReviewerSessionId
              ProviderRun = submission.ProviderRun
              ToolCallId = submission.ToolCallId }

        if ReviewProjection.hasObservedAttempt attempt guard then
            Ok VerdictDecision.AlreadyCounted
        else
            match submission.Verdict with
            | ReviewGuardVerdict.Revise ->
                append submission.ReviewerSessionId submission.ProviderRun (verdictFact submission) journal
                |> Result.map (fun _ -> VerdictDecision.Revised)

            | ReviewGuardVerdict.Perfect ->
                match guard.PendingChallenge with
                | Some challenge when challenge.FirstProviderRun = submission.ProviderRun ->
                    // REVIEW-004: extra PERFECT inside the run that already issued
                    // the challenge. Does not count and is not journalled.
                    Ok VerdictDecision.AlreadyCounted

                | None ->
                    // REVIEW-003: a barrier that already has a confirmed dual
                    // PERFECT for THIS tree cannot be re-opened by an extra
                    // PERFECT. Issuing a new challenge would replace the
                    // confirmed witness with a pending one (applyChallengeIssued),
                    // and every reader — the reviewer guard's terminal nudge and
                    // the Orchestrator's read — would then see PendingConfirmation
                    // forever while the reviewer kept answering new challenges
                    // (measured: a post-restart review cycled through four
                    // challenge rounds and never confirmed). An extra PERFECT
                    // counts nothing new.
                    if ReviewProjection.satisfiesGuard submission.GitTreeHash guard then
                        Ok VerdictDecision.AlreadyCounted
                    else
                        // First PERFECT. The verdict and its challenge are two facts:
                        // the verdict is what happened, the challenge is the evidence
                        // the second run must consume. Recording only the latter would
                        // leave the attempt uncounted for REVIEW-004.
                        let challengeDigest = ReviewChallenge.contentDigest sha256

                        append submission.ReviewerSessionId submission.ProviderRun (verdictFact submission) journal
                        |> Result.bind (fun _ ->
                            let issued =
                                AgentFact.PerfectChallengeIssued
                                    {| BarrierId = submission.BarrierId
                                       GitTreeHash = submission.GitTreeHash
                                       ReviewerSessionId = submission.ReviewerSessionId
                                       FirstProviderRun = submission.ProviderRun
                                       FirstToolCallId = submission.ToolCallId
                                       ChallengeTextVersion = ReviewChallenge.TextVersion
                                       ChallengeContentDigest = challengeDigest |}

                            append submission.ReviewerSessionId submission.ProviderRun issued journal)
                        |> Result.map (fun _ -> VerdictDecision.ChallengeIssued ReviewChallenge.Text)

                | Some challenge ->
                    match provenSeal challenge submission.ProviderRun guard with
                    | None -> Ok VerdictDecision.ChallengeUnproven
                    | Some seal ->
                        // The fold derives the confirmed witness from this fact.
                        // ConfirmedReviewWitness then records the self-contained
                        // REVIEW-006 form and clears the requirements it covered.
                        append submission.ReviewerSessionId submission.ProviderRun (verdictFact submission) journal
                        |> Result.bind (fun _ ->
                            let witness =
                                AgentFact.ConfirmedReviewWitness
                                    {| ManagerJobId = submission.ManagerJobId
                                       ManagerSessionId = submission.ManagerSessionId
                                       ReviewerSessionId = submission.ReviewerSessionId
                                       WorktreeIdentity = submission.WorktreeIdentity
                                       BarrierId = submission.BarrierId
                                       GitTreeHash = submission.GitTreeHash
                                       FirstProviderRun = challenge.FirstProviderRun
                                       FirstToolCallId = challenge.FirstToolCallId
                                       ChallengeResultDigest = challenge.ChallengeContentDigest
                                       SecondProviderRun = submission.ProviderRun
                                       SecondProviderInputDigest = seal.SealDigest
                                       SecondToolCallId = submission.ToolCallId |}

                            append submission.ReviewerSessionId submission.ProviderRun witness journal)
                        |> Result.map (fun _ -> VerdictDecision.Confirmed)
