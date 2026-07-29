namespace Wanxiangshu.Next.Kernel

open System
open Wanxiangshu.Next.Kernel.Identity

/// Durable domain facts (SSOT/11).
///
/// ARCH-005: only what still holds across process boundaries lives here. The
/// Host transcript owns the conversation, Git owns the code, and this journal
/// owns the domain facts neither of them can answer.
///
/// A fact records that something HAPPENED. Replaying history may not reject a
/// fact because today's rules changed, so no fact carries a decision or a
/// "next step" — that is ARCH-001 applied to persistence.
module Fact =

    type RuntimeFact =
        | RuntimeStarted of
            {| RuntimeId: RuntimeId
               ProcessId: int
               StartedAt: DateTimeOffset |}

    /// REVIEW-001: the verdict tool accepts exactly these two values and no
    /// description field.
    [<RequireQualifiedAccess>]
    type ReviewGuardVerdict =
        | Perfect
        | Revise

    /// PROMPT-005 `Abandoned` reason. Two cases, not a free-form string: the
    /// difference decides whether an operator must investigate a possible
    /// double effect.
    [<RequireQualifiedAccess>]
    type PromptAbandonReason =
        /// Transport proved the prompt was not accepted. Nothing happened.
        | SendFailed of error: string
        /// PROMPT-011: the recovery budget expired without proving physical
        /// acceptance. At-most-one effect holds, but which one is unknown.
        | UnresolvedAfterRecovery

    /// EXEC-004: whichever of terminal / send-failure / cancel won the
    /// single-assignment completion cell.
    [<RequireQualifiedAccess>]
    type HandleCompletionKind =
        | Terminal
        | SendFailure
        | Cancelled

    [<RequireQualifiedAccess>]
    type AgentFact =

        // ── Prompt dispatch (PROMPT-005) ────────────────────────────────────
        // Exactly four facts. Claimed → Submitted → PhysicalAccepted, or
        // Claimed → Abandoned, or Claimed → Submitted → Abandoned.

        /// Persisted BEFORE the send. PROMPT-011 needs the claim to exist even
        /// if the process dies during the Host call.
        | PluginPromptClaimed of
            {| PromptKey: PromptKey
               SessionId: SessionId
               ContinuationKind: string
               LogicalRunId: LogicalRunId option
               AuthorityRootUserMessageId: AuthorityRootUserMessageId option
               EffectiveAgent: string option
               PayloadDigest: string |}

        /// The Host call returned. The receipt may be an `accepted-*` admission
        /// id, which PROMPT-005 forbids treating as a message identity — hence
        /// the typed TransportReceipt rather than a message field.
        | PluginPromptSubmitted of
            {| PromptKey: PromptKey
               SessionId: SessionId
               Receipt: TransportReceipt |}

        /// A real physical user message was proven to exist. Only now may an
        /// Authority Root take effect.
        | PluginPromptPhysicalAccepted of
            {| PromptKey: PromptKey
               SessionId: SessionId
               PhysicalUserMessageId: PhysicalUserMessageId |}

        /// Terminal failure. Must not change the Active Logical Run.
        | PluginPromptAbandoned of
            {| PromptKey: PromptKey
               SessionId: SessionId
               Reason: PromptAbandonReason |}

        // ── Authority (PROMPT-002, PROMPT-004) ──────────────────────────────

        /// An Authority Root took effect, fixing the profile for the whole
        /// Logical Run.
        ///
        /// No model id: PROMPT-002 forbids an Authority Root selecting one, and
        /// VERIFY-006 lists a journal that stores model ids as a No-Go. The
        /// absent field is the enforcement.
        | AuthorityRootAccepted of
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               AuthorityKind: string
               SelectedAgent: string
               PeerAgent: string
               CanonicalRole: string
               SelectedTier: string |}

        // ── Fallback (FALLBACK-007) ─────────────────────────────────────────

        /// One confirmed failed attempt advanced the cursor.
        ///
        /// Both offsets are recorded so the fold can verify
        /// `NextOffset = (PreviousOffset + 1) mod 4` and reject a line that
        /// disagrees, rather than absorbing it. Success writes NOTHING: the
        /// count reset is derived from the Host snapshot, which keeps
        /// FALLBACK-003's single writer intact.
        | FallbackCursorAdvanced of
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               ProviderRun: ProviderRunIdentity
               PreviousOffset: byte
               NextOffset: byte
               ConsecutiveFailureCount: int
               Reason: string |}

        /// FALLBACK-005: the automatic recovery budget is spent. After this, the
        /// same (run, root) accepts no further advance.
        | FallbackExhausted of
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               FinalConsecutiveFailureCount: int
               FinalOffset: byte |}

        // ── Review (REVIEW-003, REVIEW-006, REVIEW-010) ─────────────────────

        /// A new review barrier opened for a tree.
        | ReviewBarrierStarted of
            {| ManagerSessionId: SessionId
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash |}

        /// A verdict was executed. REVIEW-002: any REVISE clears a pending
        /// PERFECT, so both verdicts are recorded through one fact.
        | ReviewVerdictRecorded of
            {| ManagerSessionId: SessionId
               ReviewerSessionId: SessionId
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash
               ProviderRun: ProviderRunIdentity
               ToolCallId: ToolCallId
               Verdict: ReviewGuardVerdict |}

        /// First PERFECT issued the skeptical challenge as its tool result.
        ///
        /// The digest is of a fixed, versioned sentence (REVIEW-003), so the
        /// second run's input seal can be checked for it.
        | PerfectChallengeIssued of
            {| BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash
               ReviewerSessionId: SessionId
               FirstProviderRun: ProviderRunIdentity
               FirstToolCallId: ToolCallId
               ChallengeTextVersion: int
               ChallengeContentDigest: SealDigest |}

        /// REVIEW-010: the canonical provider input for one run was sealed at
        /// `messages.transform` time and bound to that run (HOST-010).
        ///
        /// `IncludedToolResultDigests` is what makes causal proof possible: the
        /// challenge digest is either in this set or the second PERFECT does not
        /// confirm.
        | ProviderInputSealed of
            {| SessionId: SessionId
               ProviderRun: ProviderRunIdentity
               PhysicalUserMessageId: PhysicalUserMessageId
               SealDigest: SealDigest
               CanonicalVersion: int
               IncludedToolResultDigests: SealDigest list |}

        /// REVIEW-006: a self-contained confirmed witness.
        ///
        /// Every identity needed to answer "who reviewed what, and did the
        /// second run really see the first challenge" is inline. The Guard may
        /// not consult a surrounding map to complete it, so no field may be
        /// omitted here on the grounds that it is available elsewhere.
        ///
        /// REVIEW-008: this is never deleted. Validity against the current tree
        /// is a derived predicate, not a stored flag.
        | ConfirmedReviewWitness of
            {| ManagerJobId: ManagerJobId option
               ManagerSessionId: SessionId
               ReviewerSessionId: SessionId
               WorktreeIdentity: WorktreeIdentity option
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash
               FirstProviderRun: ProviderRunIdentity
               FirstToolCallId: ToolCallId
               ChallengeResultDigest: SealDigest
               SecondProviderRun: ProviderRunIdentity
               SecondProviderInputDigest: SealDigest
               SecondToolCallId: ToolCallId |}

        // ── Execution handles (EXEC-009) ────────────────────────────────────
        // Three facts, three states: active, completed-awaiting-join, retired.
        // The previous Linked/Unlinked pair could not express the middle state,
        // so a completed-but-unjoined child was indistinguishable from a live
        // one.

        | HandleLinked of
            {| ParentSessionId: SessionId
               Handle: HandleId
               TargetAgent: string
               CanonicalRole: string option |}

        | HandleCompleted of
            {| ParentSessionId: SessionId
               Handle: HandleId
               Kind: HandleCompletionKind |}

        /// The durable tombstone. EXEC-009: a retired id returns RetiredHandle
        /// forever and must never degrade into "treat the input as an agent name
        /// and fork again".
        | HandleRetired of
            {| ParentSessionId: SessionId
               Handle: HandleId |}

        // ── Orchestrator (ORCH-006) ─────────────────────────────────────────
        // Each fact determines exactly one recovery action (ORCH-007). The old
        // set was stage-like: CandidateRegistered could mean "waiting for
        // review" or "ready to publish", which is precisely the branch ORCH-006
        // forbids.

        | ManagerJobCreated of
            {| ManagerJobId: ManagerJobId
               ManagerSessionId: SessionId
               ManagerAgent: string
               WorktreeIdentity: WorktreeIdentity
               WorktreePath: WorktreePath
               TargetRef: TargetRef
               TargetBranchFrozen: string |}

        | CandidateReady of
            {| ManagerJobId: ManagerJobId
               CandidateCommit: CommitHash
               PreRebaseReviewBarrierId: ReviewBarrierId |}

        /// ORCH-007 needs this to tell "Manager is resolving a conflict" from
        /// "Manager has not produced a candidate yet". Without it, recovery
        /// either re-rebases or loses the conflict context.
        | ConflictDetected of
            {| ManagerJobId: ManagerJobId
               CandidateCommit: CommitHash
               TargetHeadSnapshot: CommitHash
               ConflictFiles: string list
               DiagnosticsDigest: string |}

        | RebasedCandidateReady of
            {| ManagerJobId: ManagerJobId
               RebasedCommit: CommitHash
               TargetHeadSnapshot: CommitHash
               PostRebaseReviewBarrierId: ReviewBarrierId |}

        /// ORCH-005: written inside the short CAS window, immediately before the
        /// ref mutation. ExpectedHead is what recovery compares against.
        | PublishClaimed of
            {| ManagerJobId: ManagerJobId
               TargetRef: TargetRef
               ExpectedHead: CommitHash |}

        | Published of
            {| ManagerJobId: ManagerJobId
               CandidateCommit: CommitHash
               ResultingTargetHead: CommitHash |}

        | JobFailed of
            {| ManagerJobId: ManagerJobId
               Reason: string |}

        | JobAbandoned of {| ManagerJobId: ManagerJobId |}

        // ── Companion (SSOT/08) ─────────────────────────────────────────────

        | CompanionBaselineSet of
            {| SessionId: SessionId
               Projection: string |}

        | CompanionCheckpointReplaced of
            {| SessionId: SessionId
               Content: string |}

        | CompanionAdvanced of
            {| SessionId: SessionId
               Projection: string
               Content: string |}

        | CompanionReplacementActiveSet of {| SessionId: SessionId; Active: bool |}

        /// COMPANION-009: an epoch switch creates a new SealRoot and is the one
        /// sanctioned prefix-cache cold boundary.
        | CompanionEpochSwitched of
            {| SessionId: SessionId
               EpochId: string
               FrozenB: string
               CutoffMessageIndex: int
               CoveredPrefixDigest: string |}

        // ── Durable effects (PERSIST-009) ───────────────────────────────────
        // Requested → idempotent side effect → Accepted. After a crash,
        // Requested-without-Accepted is treated as not having happened.

        | DurableEffectRequested of
            {| EffectId: string
               SessionId: SessionId
               Target: string
               Payload: string |}

        | DurableEffectAccepted of
            {| EffectId: string
               SessionId: SessionId
               Result: string |}

    type Fact =
        | Runtime of RuntimeFact
        | Agent of AgentFact
