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

/// The XTrace boundary captured by a terminal while this review barrier is active.
/// It is projection-only: replay reconstructs it from the barrier and terminal facts.
type ReviewTerminalFrontier =
    { BarrierId: ReviewBarrierId
      TerminalRef: BlobRef
      TerminalDigest: BlobDigest
      Sequence: int64 }

/// REVIEW-013/017: one reviewer attempt's reconciled turn fully closed.
///
/// The frontier is frozen at closure time. A consumer that re-read the session's
/// current head instead would fold a finished attempt's late-landing XTrace tail
/// into the next barrier's request range.
type ClosedAttempt =
    { Attempt: ReviewAttemptIdentity
      FrozenFrontier: XTraceCursor }

/// Completed review facts for one session. Attempt/closure audit windows are bounded;
/// no field encodes where the Finality review CE is currently executing.
/// DSL-state-combination: domain — optional barrier/session/tree/frontier
/// identities are durable review evidence; no field stores an execution stage.
type ReviewGuardProjection =
    {
        CurrentBarrierId: ReviewBarrierId option
        CurrentManagerSessionId: SessionId option
        LastGitTreeHash: GitTreeHash option
        Witness: ReviewWitness
        /// Frozen once the barrier reaches REVISE or Confirmed. Before that, a
        /// later terminal replaces an earlier provisional one from this barrier.
        TerminalFrontier: ReviewTerminalFrontier option
        /// REVIEW-004 bounded typed verdict evidence, newest first. The unique
        /// integrator records the exact attempt identity once; consumers never
        /// reconstruct it from Journal bytes or semantic trace parts.
        ObservedAttempts: ReviewAttemptIdentity list
        /// REVIEW-013/017 bounded closed-attempt evidence, newest first. An
        /// attempt is closed only after its reconciled turn completed and its
        /// XTrace frontier froze; `TodoReviewConcluded` consumes only these.
        ClosedAttempts: ClosedAttempt list
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

    let empty =
        { CurrentBarrierId = None
          CurrentManagerSessionId = None
          LastGitTreeHash = None
          Witness = ReviewWitness.NoReview
          TerminalFrontier = None
          ObservedAttempts = []
          ClosedAttempts = [] }

    let private remember key keys =
        key :: (keys |> List.filter ((<>) key)) |> List.truncate AttemptWindow

    let private witnessAfterBarrier currentWitness =
        match currentWitness with
        | ReviewWitness.Confirmed _ -> currentWitness
        | ReviewWitness.NoReview
        | ReviewWitness.RevisionWitness _ -> ReviewWitness.NoReview

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
                ObservedAttempts = []
                ClosedAttempts = []
                Witness = witnessAfterBarrier current.Witness }

    let private frontierAlreadyFrozen (current: ReviewGuardProjection) =
        match current.Witness with
        | ReviewWitness.RevisionWitness _
        | ReviewWitness.Confirmed _ -> current.TerminalFrontier.IsSome
        | ReviewWitness.NoReview -> false

    let private recordFrontierIfOpen
        (barrierId: ReviewBarrierId)
        (terminalRef: BlobRef)
        (terminalDigest: BlobDigest)
        (sequence: int64)
        (current: ReviewGuardProjection) =
        if frontierAlreadyFrozen current then
            current
        else
            { current with
                TerminalFrontier =
                    Some
                        { BarrierId = barrierId
                          TerminalRef = terminalRef
                          TerminalDigest = terminalDigest
                          Sequence = sequence } }

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
        | Some barrierId -> recordFrontierIfOpen barrierId terminalRef terminalDigest sequence current

    /// REVIEW-002: any REVISE clears an unfinished PERFECT confirmation.
    let private applyRevise (gitTreeHash: GitTreeHash) (current: ReviewGuardProjection) =
        { current with
            LastGitTreeHash = Some gitTreeHash
            Witness =
                ReviewWitness.RevisionWitness
                    {| Report = ""
                       GitTreeHash = gitTreeHash |} }

    let private applyNewVerdict
        (attempt: ReviewAttemptIdentity)
        (verdict: ReviewGuardVerdict)
        (current: ReviewGuardProjection) =
        let observed =
            { current with
                ObservedAttempts = remember attempt current.ObservedAttempts
                LastGitTreeHash = Some attempt.GitTreeHash }

        match verdict with
        | ReviewGuardVerdict.Revise -> Ok(applyRevise attempt.GitTreeHash observed)
        | ReviewGuardVerdict.Perfect -> Ok observed

    /// Apply one verdict.
    ///
    /// Records that the attempt counted (REVIEW-004) and, for REVISE, clears any
    /// unfinished confirmation (REVIEW-002). It deliberately does NOT judge
    /// REVIEW-003's causal proof or build a `Confirmed` witness.
    ///
    /// Confirmation arrives as its own completed fact. The fold applies that fact;
    /// it never reconstructs the direct CE's first/challenge/second call order.
    let applyVerdict
        (attempt: ReviewAttemptIdentity)
        (verdict: ReviewGuardVerdict)
        (current: ReviewGuardProjection)
        : Result<ReviewGuardProjection, VerdictRejection> =
        if List.contains attempt current.ObservedAttempts then
            Error DuplicateAttempt
        else
            applyNewVerdict attempt verdict current

    /// REVIEW-003/006: the confirmed witness, already proven by its writer.
    ///
    /// Takes the two witnesses and their physical prompt identities as given.
    /// REVIEW-006's self-containment keeps every identity the Guard needs inline
    /// in the completed fact, so replay never consults a surrounding map or
    /// reconstructs the CE's execution position.
    let applyConfirmedWitness
        (barrierId: ReviewBarrierId)
        (firstPhysicalUserMessageId: PhysicalUserMessageId)
        (secondPhysicalUserMessageId: PhysicalUserMessageId)
        (first: VerdictWitness)
        (second: VerdictWitness)
        (current: ReviewGuardProjection)
        : Result<ReviewGuardProjection, VerdictRejection> =
        match ReviewWitness.confirm barrierId firstPhysicalUserMessageId secondPhysicalUserMessageId first second with
        | None -> Error NotDistinctAttempt
        | Some confirmed ->
            Ok
                { current with
                    Witness = confirmed
                    LastGitTreeHash = Some second.GitTreeHash }

    /// REVIEW-004: has this exact attempt already counted.
    ///
    /// Exposed so the writer can decide before appending. `applyVerdict` answers
    /// the same question, but only by rejecting a fact that has already been
    /// written — and `Fold.verdictOutcome` turns one of its rejections into a
    /// journal write failure.
    let hasObservedAttempt (attempt: ReviewAttemptIdentity) (current: ReviewGuardProjection) =
        List.contains attempt current.ObservedAttempts

    let latestObservedAttempt (current: ReviewGuardProjection) = List.tryHead current.ObservedAttempts

    /// REVIEW-013/017: record one attempt's closure. Idempotent by attempt
    /// identity — the reviewer's turn observation may legitimately re-run
    /// (idle revisit), and re-appending the same closure must be a no-op
    /// rather than a second window entry.
    let applyAttemptClosed (closed: ClosedAttempt) (current: ReviewGuardProjection) =
        { current with
            ClosedAttempts =
                closed
                :: (current.ClosedAttempts
                    |> List.filter (fun existing -> existing.Attempt <> closed.Attempt))
                |> List.truncate AttemptWindow }

    /// The closure evidence for one exact attempt, if its turn has closed.
    let closedAttemptOf (attempt: ReviewAttemptIdentity) (current: ReviewGuardProjection) =
        current.ClosedAttempts |> List.tryFind (fun closed -> closed.Attempt = attempt)

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
