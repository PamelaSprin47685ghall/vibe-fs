namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity

/// The skeptical challenge issued by a first PERFECT (REVIEW-003).
///
/// Kept as durable evidence so the second run's input seal can be checked for
/// the digest. Without this the only available comparison was "same authority
/// root", which REVIEW-003 explicitly forbids.
type PerfectChallenge =
    { BarrierId: ReviewBarrierId
      GitTreeHash: GitTreeHash
      ReviewerSessionId: SessionId
      FirstProviderRun: ProviderRunIdentity
      FirstToolCallId: ToolCallId
      ChallengeTextVersion: int
      ChallengeContentDigest: SealDigest }

/// REVIEW-010: the canonical provider input for one run, sealed at
/// `messages.transform` time and bound to that run per HOST-010.
type ProviderInputSeal =
    {
        SessionId: SessionId
        ProviderRun: ProviderRunIdentity
        PhysicalUserMessageId: PhysicalUserMessageId
        SealDigest: SealDigest
        CanonicalVersion: int
        /// The causal evidence. A challenge digest is either in this set or the
        /// second PERFECT does not confirm.
        IncludedToolResultDigests: Set<string>
    }

/// Review state for one session.
///
/// PERSIST-008: bounded. Seals are kept per provider run within a window, and
/// attempt keys within a window; neither grows with history length.
type ReviewGuardProjection =
    {
        CurrentBarrierId: ReviewBarrierId option
        LastGitTreeHash: GitTreeHash option
        Witness: ReviewWitness
        /// Set once the first PERFECT issued its challenge; cleared by REVISE or a
        /// tree change.
        PendingChallenge: PerfectChallenge option
        /// Seals observed recently, keyed by provider run. Bounded window.
        Seals: Map<ProviderRunIdentity, ProviderInputSeal>
        /// REVIEW-004 dedupe: extra PERFECT calls inside one provider run neither
        /// count nor journal. Replaces the old pair of "recent run ids" and
        /// "recent tool call ids" lists, which could not express the conjunction
        /// the clause requires.
        ObservedAttemptKeys: string list
    }

    member this.IsConfirmed = ReviewWitness.isConfirmed this.Witness

/// A human prompt awaiting review confirmation (REVIEW-007).
///
/// Keyed by Authority Root, not by physical message. The requirement is about
/// the task a human asked for, and PROMPT-002 makes the Authority Root that
/// task's identity. Storing the wire message instead would also require
/// converting one identity into the other, which PROMPT-001 exists to prevent.
type ReviewRequirementInput =
    { SourceSessionId: SessionId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId }

type ReviewRequirementProjection =
    { HumanPromptInputs: ReviewRequirementInput list
      LastConfirmedProviderRun: ProviderRunIdentity option }

/// Why a verdict was not applied.
type VerdictRejection =
    /// REVIEW-004: this exact (barrier, tree, reviewer, run, call) already
    /// counted.
    | DuplicateAttempt
    /// REVIEW-004: another PERFECT from the same provider run. Does not count,
    /// is not journalled.
    | SameProviderRun
    /// REVIEW-003 condition 6: no seal for the second run proves it consumed the
    /// challenge. Fail closed — never fall back to same-root guessing.
    | ChallengeNotProven

module ReviewProjection =

    /// Enough to hold one barrier's two attempts plus slack. REVIEW-004 only
    /// needs to recognise repeats within the current barrier.
    [<Literal>]
    let private AttemptWindow = 8

    /// Seals matter only until the verdict that consumes them.
    [<Literal>]
    let private SealWindow = 8

    let empty =
        { CurrentBarrierId = None
          LastGitTreeHash = None
          Witness = ReviewWitness.NoReview
          PendingChallenge = None
          Seals = Map.empty
          ObservedAttemptKeys = [] }

    let private remember key keys =
        key :: (keys |> List.filter ((<>) key)) |> List.truncate AttemptWindow

    /// Drop the oldest seals once the window overflows. Map has no insertion
    /// order, so the bound is by provider run count, which is what the window is
    /// about.
    let private rememberSeal (seal: ProviderInputSeal) seals =
        let next = Map.add seal.ProviderRun seal seals

        if Map.count next <= SealWindow then
            next
        else
            let dropped =
                next
                |> Map.toList
                |> List.map fst
                |> List.filter (fun run -> run <> seal.ProviderRun)
                |> List.truncate (Map.count next - SealWindow)

            dropped |> List.fold (fun acc run -> Map.remove run acc) next

    /// A new barrier discards the previous barrier's pending challenge and
    /// attempt window. REVIEW-008: the confirmed witness is NOT discarded — it
    /// stays auditable, and validity against the current tree is derived.
    let startBarrier (barrierId: ReviewBarrierId) (gitTreeHash: GitTreeHash) (current: ReviewGuardProjection) =
        if current.CurrentBarrierId = Some barrierId then
            current
        else
            { current with
                CurrentBarrierId = Some barrierId
                LastGitTreeHash = Some gitTreeHash
                PendingChallenge = None
                ObservedAttemptKeys = [] }

    /// REVIEW-010: record a seal. Pure storage; the causal judgement happens at
    /// verdict time.
    let applySeal (seal: ProviderInputSeal) (current: ReviewGuardProjection) =
        { current with
            Seals = rememberSeal seal current.Seals }

    /// REVIEW-003: the first PERFECT issued its challenge.
    let applyChallengeIssued (challenge: PerfectChallenge) (current: ReviewGuardProjection) =
        { current with
            PendingChallenge = Some challenge
            LastGitTreeHash = Some challenge.GitTreeHash }

    /// REVIEW-002: any REVISE clears an unfinished PERFECT confirmation.
    let private applyRevise (gitTreeHash: GitTreeHash) (current: ReviewGuardProjection) =
        { current with
            LastGitTreeHash = Some gitTreeHash
            Witness =
                ReviewWitness.RevisionWitness
                    {| Report = ""
                       GitTreeHash = gitTreeHash |}
            PendingChallenge = None }

    /// Apply one verdict.
    ///
    /// Returns a rejection reason rather than the unchanged projection: the old
    /// fold returned `existing` on every refusal path, so "duplicate", "same
    /// run" and "no causal proof" were indistinguishable from a successful
    /// no-op — and the last of those is a REVIEW-003 violation that must be
    /// visible.
    let applyVerdict
        (attempt: ReviewAttemptIdentity)
        (verdict: ReviewGuardVerdict)
        (current: ReviewGuardProjection)
        : Result<ReviewGuardProjection, VerdictRejection> =
        let key = ReviewAttemptIdentity.dedupeKey attempt

        if List.contains key current.ObservedAttemptKeys then
            Error DuplicateAttempt
        else
            let observed =
                { current with
                    ObservedAttemptKeys = remember key current.ObservedAttemptKeys
                    LastGitTreeHash = Some attempt.GitTreeHash }

            match verdict with
            | ReviewGuardVerdict.Revise -> Ok(applyRevise attempt.GitTreeHash observed)
            | ReviewGuardVerdict.Perfect ->
                match observed.PendingChallenge with
                // No challenge outstanding: this is a first PERFECT. The
                // challenge fact itself arrives separately, so the witness only
                // becomes pending once that fact is folded.
                | None -> Ok observed
                | Some challenge when challenge.FirstProviderRun = attempt.ProviderRun -> Error SameProviderRun
                | Some challenge ->
                    match Map.tryFind attempt.ProviderRun observed.Seals with
                    // HOST-010: a transform output that cannot be bound to this
                    // provider run means no seal, and no seal means fail closed.
                    | None -> Error ChallengeNotProven
                    | Some seal when
                        not (
                            Set.contains
                                (SealDigest.value challenge.ChallengeContentDigest)
                                seal.IncludedToolResultDigests
                        )
                        ->
                        Error ChallengeNotProven
                    | Some seal ->
                        let first =
                            { ProviderRun = challenge.FirstProviderRun
                              ToolCallId = challenge.FirstToolCallId
                              GitTreeHash = challenge.GitTreeHash
                              ReviewerSessionId = challenge.ReviewerSessionId }

                        let second =
                            { ProviderRun = attempt.ProviderRun
                              ToolCallId = attempt.ToolCallId
                              GitTreeHash = attempt.GitTreeHash
                              ReviewerSessionId = attempt.ReviewerSessionId }

                        match
                            ReviewWitness.confirm
                                challenge.BarrierId
                                challenge.ChallengeContentDigest
                                seal.SealDigest
                                first
                                second
                        with
                        | None -> Error ChallengeNotProven
                        | Some confirmed ->
                            Ok
                                { observed with
                                    Witness = confirmed
                                    PendingChallenge = None }

    /// REVIEW-007: the Guard asks only whether the CURRENT tree has a confirmed
    /// PERFECT. A witness for another tree is auditable but not sufficient.
    let satisfiesGuard (currentTree: GitTreeHash) (current: ReviewGuardProjection) =
        ReviewWitness.isConfirmed current.Witness
        && ReviewWitness.isValidForTree currentTree current.Witness

module ReviewRequirementProjection =

    let empty =
        { HumanPromptInputs = []
          LastConfirmedProviderRun = None }

    let addRequirement
        (sourceSessionId: SessionId)
        (authorityRoot: AuthorityRootUserMessageId)
        (current: ReviewRequirementProjection)
        =
        let input =
            { SourceSessionId = sourceSessionId
              AuthorityRootUserMessageId = authorityRoot }

        if List.contains input current.HumanPromptInputs then
            current
        else
            { current with
                HumanPromptInputs = current.HumanPromptInputs @ [ input ] }

    /// A confirmed review clears the requirements it covered.
    let clearOnConfirmation (providerRun: ProviderRunIdentity) (current: ReviewRequirementProjection) =
        if current.LastConfirmedProviderRun = Some providerRun then
            current
        else
            { HumanPromptInputs = []
              LastConfirmedProviderRun = Some providerRun }
