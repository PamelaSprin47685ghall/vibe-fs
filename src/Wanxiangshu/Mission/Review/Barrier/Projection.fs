namespace Wanxiangshu.Mission.Review.Barrier

open Wanxiangshu.Composition.Durable

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

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

/// The XTrace boundary captured by a terminal while this review barrier is active.
/// It is projection-only: replay reconstructs it from the barrier and terminal facts.
type ReviewTerminalFrontier =
    { BarrierId: ReviewBarrierId
      TerminalRef: BlobRef
      TerminalDigest: BlobDigest
      Sequence: int64 }

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
        CurrentManagerSessionId: SessionId option
        LastGitTreeHash: GitTreeHash option
        Witness: ReviewWitness
        /// Frozen once the barrier reaches REVISE or Confirmed. Before that, a
        /// later terminal replaces an earlier provisional one from this barrier.
        TerminalFrontier: ReviewTerminalFrontier option
        /// Set once the first PERFECT issued its challenge; cleared by REVISE or a
        /// tree change.
        PendingChallenge: PerfectChallenge option
        /// Seals observed recently, keyed by provider run. Bounded window.
        Seals: Map<ProviderRunIdentity, ProviderInputSeal>
        /// REVIEW-004 bounded typed verdict evidence, newest first. The unique
        /// integrator records the exact attempt identity once; consumers never
        /// reconstruct it from Journal bytes or semantic trace parts.
        ObservedAttempts: ReviewAttemptIdentity list
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
    {
        /// Stored newest-first so replay cons is O(1). `inputs` restores oldest-first.
        HumanPromptInputs: ReviewRequirementInput list
        InputKeys: Set<string>
        LastConfirmedProviderRun: ProviderRunIdentity option
    }

/// Why a verdict or witness was not applied.
type VerdictRejection =
    /// REVIEW-004: this exact (barrier, tree, reviewer, run, call) already
    /// counted. Expected on replay, so it is absorbed.
    | DuplicateAttempt
    /// REVIEW-003 conditions 1-5 do not hold between the two witnesses. A
    /// correct writer cannot produce this, so the journal line is rejected
    /// rather than absorbed.
    | NotDistinctAttempt

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
          CurrentManagerSessionId = None
          LastGitTreeHash = None
          Witness = ReviewWitness.NoReview
          TerminalFrontier = None
          PendingChallenge = None
          Seals = Map.empty
          ObservedAttempts = [] }

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
    /// attempt window. REVIEW-008: a confirmed witness stays auditable (validity
    /// is still asked against the current tree / barrier); unfinished revision
    /// or pending-PERFECT state is barrier-scoped and must not leak into the
    /// next request (GLORY-045 reuse of the same Reviewer session).
    let startBarrier
        (managerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (gitTreeHash: GitTreeHash)
        (current: ReviewGuardProjection)
        =
        if current.CurrentBarrierId = Some barrierId then
            current
        else
            { current with
                CurrentBarrierId = Some barrierId
                CurrentManagerSessionId = Some managerSessionId
                LastGitTreeHash = Some gitTreeHash
                TerminalFrontier = None
                PendingChallenge = None
                ObservedAttempts = []
                Witness =
                    match current.Witness with
                    | ReviewWitness.Confirmed _ -> current.Witness
                    | ReviewWitness.NoReview
                    | ReviewWitness.RevisionWitness _
                    | ReviewWitness.PerfectPending _ -> ReviewWitness.NoReview }

    /// REVIEW-010: record a seal. Pure storage; the causal judgement happens at
    /// verdict time.
    let applySeal (seal: ProviderInputSeal) (current: ReviewGuardProjection) =
        { current with
            Seals = rememberSeal seal current.Seals }

    /// GLORY-072/073: bind a terminal's exclusive XTrace frontier to the barrier
    /// that was active when the terminal fact folded. ProviderRun is deliberately
    /// not used: XTrace parts do not carry a comparable run identity.
    let recordTerminalFrontier
        (terminalRef: BlobRef)
        (terminalDigest: BlobDigest)
        (sequence: int64)
        (current: ReviewGuardProjection)
        =
        match current.CurrentBarrierId with
        | None -> current
        | Some barrierId ->
            let frozen =
                match current.Witness with
                | ReviewWitness.RevisionWitness _
                | ReviewWitness.Confirmed _ -> current.TerminalFrontier.IsSome
                | ReviewWitness.NoReview
                | ReviewWitness.PerfectPending _ -> false

            if frozen then
                current
            else
                { current with
                    TerminalFrontier =
                        Some
                            { BarrierId = barrierId
                              TerminalRef = terminalRef
                              TerminalDigest = terminalDigest
                              Sequence = sequence } }

    /// REVIEW-003: the first PERFECT issued its challenge.
    ///
    /// This is also what makes the witness pending. The previous version stored
    /// only `PendingChallenge`, so `ReviewWitness.isPerfectPending` was never
    /// true and both readers of it — the reviewer guard's confirmation nudge and
    /// the Orchestrator's `PendingConfirmation` branch — waited for a state the
    /// fold could not produce. A first PERFECT looked indistinguishable from no
    /// review at all.
    ///
    /// The pending witness is built from the challenge rather than passed in: the
    /// challenge already carries the whole first `VerdictWitness`, and a second
    /// parameter would let the two disagree.
    let applyChallengeIssued (challenge: PerfectChallenge) (current: ReviewGuardProjection) =
        let first =
            { ProviderRun = challenge.FirstProviderRun
              ToolCallId = challenge.FirstToolCallId
              GitTreeHash = challenge.GitTreeHash
              ReviewerSessionId = challenge.ReviewerSessionId }

        { current with
            PendingChallenge = Some challenge
            LastGitTreeHash = Some challenge.GitTreeHash
            Witness = ReviewWitness.PerfectPending first }

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
    /// Records that the attempt counted (REVIEW-004) and, for REVISE, clears any
    /// unfinished confirmation (REVIEW-002). It deliberately does NOT judge
    /// REVIEW-003's causal proof or build a `Confirmed` witness.
    ///
    /// Confirmation arrives as its own fact. Re-proving it here would mean asking
    /// the seal window at replay time, and `Seals` is bounded to `SealWindow`
    /// entries — an old journal's seal has long rolled out, so the fold would
    /// reject a line that was fully proven when written and the whole replay
    /// would fail. A writer proves; a fold applies.
    let applyVerdict
        (attempt: ReviewAttemptIdentity)
        (verdict: ReviewGuardVerdict)
        (current: ReviewGuardProjection)
        : Result<ReviewGuardProjection, VerdictRejection> =
        if List.contains attempt current.ObservedAttempts then
            Error DuplicateAttempt
        else
            let observed =
                { current with
                    ObservedAttempts = remember attempt current.ObservedAttempts
                    LastGitTreeHash = Some attempt.GitTreeHash }

            match verdict with
            | ReviewGuardVerdict.Revise -> Ok(applyRevise attempt.GitTreeHash observed)
            | ReviewGuardVerdict.Perfect -> Ok observed

    /// REVIEW-003/006: the confirmed witness, already proven by its writer.
    ///
    /// Takes the two witnesses and both digests as given. That is the point of
    /// REVIEW-006's self-containment: every identity the Guard needs is inline in
    /// the fact, so this function never consults a surrounding map to complete
    /// one. `ReviewWitness.confirm` still enforces conditions 1–5, which are pure
    /// comparisons of the two witnesses and stay valid at any replay time.
    let applyConfirmedWitness
        (barrierId: ReviewBarrierId)
        (challengeResultDigest: SealDigest)
        (secondProviderInputDigest: SealDigest)
        (first: VerdictWitness)
        (second: VerdictWitness)
        (current: ReviewGuardProjection)
        : Result<ReviewGuardProjection, VerdictRejection> =
        match ReviewWitness.confirm barrierId challengeResultDigest secondProviderInputDigest first second with
        | None -> Error NotDistinctAttempt
        | Some confirmed ->
            Ok
                { current with
                    Witness = confirmed
                    LastGitTreeHash = Some second.GitTreeHash
                    PendingChallenge = None }

    /// REVIEW-004: has this exact attempt already counted.
    ///
    /// Exposed so the writer can decide before appending. `applyVerdict` answers
    /// the same question, but only by rejecting a fact that has already been
    /// written — and `Fold.verdictOutcome` turns one of its rejections into a
    /// journal write failure.
    let hasObservedAttempt (attempt: ReviewAttemptIdentity) (current: ReviewGuardProjection) =
        List.contains attempt current.ObservedAttempts

    let latestObservedAttempt (current: ReviewGuardProjection) =
        List.tryHead current.ObservedAttempts

    /// REVIEW-007: the Guard asks only whether the CURRENT tree has a confirmed
    /// PERFECT. A witness for another barrier or tree is auditable but not sufficient.
    let satisfiesGuard (currentTree: GitTreeHash) (current: ReviewGuardProjection) =
        match current.CurrentBarrierId, current.Witness with
        | Some barrierId, ReviewWitness.Confirmed confirmed ->
            confirmed.BarrierId = barrierId
            && ReviewWitness.isValidForTree currentTree current.Witness
        | _ -> false

module ReviewRequirementProjection =

    let empty =
        { HumanPromptInputs = []
          InputKeys = Set.empty
          LastConfirmedProviderRun = None }

    let private inputKey (sourceSessionId: SessionId) (authorityRoot: AuthorityRootUserMessageId) =
        SessionId.value sourceSessionId
        + "\x1f"
        + AuthorityRootUserMessageId.value authorityRoot

    /// Oldest-first. The stored field is newest-first.
    let inputs (current: ReviewRequirementProjection) = List.rev current.HumanPromptInputs

    let addRequirement
        (sourceSessionId: SessionId)
        (authorityRoot: AuthorityRootUserMessageId)
        (current: ReviewRequirementProjection)
        =
        let key = inputKey sourceSessionId authorityRoot

        if Set.contains key current.InputKeys then
            current
        else
            let input =
                { SourceSessionId = sourceSessionId
                  AuthorityRootUserMessageId = authorityRoot }

            { current with
                HumanPromptInputs = input :: current.HumanPromptInputs
                InputKeys = Set.add key current.InputKeys }

    /// A confirmed review clears the requirements it covered.
    let clearOnConfirmation (providerRun: ProviderRunIdentity) (current: ReviewRequirementProjection) =
        if current.LastConfirmedProviderRun = Some providerRun then
            current
        else
            { HumanPromptInputs = []
              InputKeys = Set.empty
              LastConfirmedProviderRun = Some providerRun }
