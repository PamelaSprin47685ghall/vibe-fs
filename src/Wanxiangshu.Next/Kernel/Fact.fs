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
        //
        // Every review fact carries ReviewerSessionId, and the ReviewGuard
        // projection is keyed by it. The review conversation happens in the
        // reviewer's session, so that is where its state belongs; the Manager
        // Guard resolves the reviewer through its own handle projection, which
        // is a keyed lookup it already owns.
        //
        // The previous shape keyed review state by manager session and then
        // searched every session to discover the parent of a reviewer, which is
        // a full scan (PERSIST-008) and silently tolerated a hit under the wrong
        // parent.

        /// A new review barrier opened for a tree.
        | ReviewBarrierStarted of
            {| ReviewerSessionId: SessionId
               ManagerSessionId: SessionId
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash |}

        /// A verdict was executed. REVIEW-002: any REVISE clears a pending
        /// PERFECT, so both verdicts flow through one fact.
        | ReviewVerdictRecorded of
            {| ReviewerSessionId: SessionId
               ManagerSessionId: SessionId
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

        /// A handle was created and bound to a child session.
        ///
        /// `ChildSessionId` is what makes EXEC-009's "restart recovers the same
        /// ID" achievable: a handle id is minted by the plugin and does not exist
        /// on the Host side, so a recovered handle with no session recorded points
        /// at nothing. It cannot be derived either — the session id is issued by
        /// the Host, and deriving one from the handle would fabricate an identity
        /// every later operation silently no-ops against.
        ///
        /// `CanonicalRole` is not optional. Every fork has a role fixed by its
        /// Authority Root (PROMPT-008), so an absent role could only mean recovery
        /// has to invent one — and an invented role decides the child's whole tool
        /// surface.
        | HandleLinked of
            {| ParentSessionId: SessionId
               ChildSessionId: SessionId
               Handle: HandleId
               TargetAgent: string
               CanonicalRole: Role |}

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

        /// COMPANION-003: Y is X's long-lived companion Blogger Session, so which
        /// session that is must survive a restart.
        ///
        /// A Companion fact, deliberately NOT `HandleLinked`. A handle is something
        /// `list` shows and `join` may consume (EXEC-004/005); the Blogger is an
        /// internal agent the model never joins, and AGENT-008 keeps it out of the
        /// resource view entirely. Recording it as a handle would put it there.
        | CompanionBloggerLinked of
            {| SessionId: SessionId
               BloggerSessionId: SessionId
               BloggerAgent: string |}

        /// The Blogger child was aborted and unbound. A later transform creates a
        /// fresh one rather than reviving this session.
        | CompanionBloggerClosed of {| SessionId: SessionId |}

        // ── lifecycle work record (SSOT/08, HOST-005) ───────────────────────

        /// COMPANION-003: the Session's opening task prompt, captured verbatim at
        /// the physical acceptance point. Idempotent and never overwritten
        /// (PERSIST-010): replaying the same capture changes nothing, and a second
        /// different capture is a line no correct writer produces.
        ///
        /// Inline rather than blob: the opening is the first task prompt, bounded
        /// and human-sized, and the fold needs the text to materialise the LWR
        /// without a second read step.
        ///
        /// `ProviderRun` is `None` because the capture happens at the physical
        /// acceptance point (chat.message), before any provider run exists.
        | OpeningPromptCaptured of
            {| SessionId: SessionId
               AssignmentText: string
               AuthoritativeRequirements: string list
               ProviderRun: ProviderRunIdentity option |}

        /// COMPANION-003 / HOST-005: one semantic part appended to the XTrace.
        /// Strictly ordered, append-only; the body goes to a blob (PERSIST-007)
        /// and the line carries cursor/digest/provenance.
        ///
        /// `CursorSequence` is the XTraceCursor sequence — strictly monotonic,
        /// independent of Host transcript numbering, so a Host compaction voids
        /// no cursor (COMPANION-008). `Kind` is one of
        /// text / reasoning / tool_call / tool_result / media, matching
        /// `SemanticPart`; `ToolName` exists only for tool_call.
        | XTracePartAppended of
            {| SessionId: SessionId
               CursorSequence: int64
               Role: string
               Turn: int
               PartIndex: int
               Kind: string
               ToolName: string option
               TextRef: BlobRef
               TextDigest: BlobDigest
               Provenance: string
               ProviderRun: ProviderRunIdentity option |}

        /// COMPANION-003: the Session's terminal output, captured verbatim at
        /// reconcile. Idempotent and never overwritten. The body goes to a blob.
        | TerminalOutputCaptured of
            {| SessionId: SessionId
               TextRef: BlobRef
               TextDigest: BlobDigest
               ProviderRun: ProviderRunIdentity |}

        // ── failure-driven context recovery (SSOT/12) ───────────────────────

        /// COMPANION-008: one Blogger entry landed, and the coverage it proves
        /// advanced. ONE fact, not two: the clause makes frame append and
        /// coverage advance the same domain commit, so a shape that could record
        /// either alone would make the forbidden intermediate states expressible.
        ///
        /// Both cursors are recorded so the fold can verify monotonicity without
        /// trusting the writer (PERSIST-010).
        ///
        /// `IngestedThroughSequence` is the RecordCoverage advance in XTraceCursor
        /// coordinates (COMPANION-003); it may sit mid-turn. The cutoff/digest pair
        /// is the PrefixCoverage advance and only ever sits on a complete turn
        /// boundary (COMPANION-011). The two prove different claims and neither
        /// may be derived from the other.
        /// ENFORCER-045: one atomic BloggerMain cycle — frame + coverage +
        /// enforcement half. No separate EnforcementCycleCommitted.
        | BlogEntryCommitted of
            {| SessionId: SessionId
               BloggerSessionId: SessionId
               FrameEpochId: FrameEpochId
               PreviousIngestedThroughSequence: int64
               NextIngestedThroughSequence: int64
               PreviousCoverableTurnCutoffExclusive: int
               NextCoverableTurnCutoffExclusive: int
               NextCoveredPrefixDigest: string
               TextRef: BlobRef
               TextDigest: BlobDigest
               ProviderRun: ProviderRunIdentity
               ToolCallIds: ToolCallId list
               ScoreVectorRef: BlobRef option
               EvidenceRef: BlobRef option
               ObservedPrefixEpochId: PrefixEpochId |}

        /// CTX-012: a valid squash rewrote the oldest frames. Permanent once
        /// committed, even if the same slot's main request then fails.
        ///
        /// Carries no coverage fields: a squash changes how B is REPRESENTED, not
        /// which X turns it covers. Including them would let a writer silently
        /// move coverage under cover of a compression.
        | BlogSquashCommitted of
            {| SessionId: SessionId
               BloggerSessionId: SessionId
               PreviousFrameEpochId: FrameEpochId
               NextFrameEpochId: FrameEpochId
               CoveredFrameCount: int
               TextRef: BlobRef
               TextDigest: BlobDigest
               ProviderRun: ProviderRunIdentity |}

        /// CTX-012: a probe attempt produced a valid terminal, so its candidate
        /// prefix is promoted to the committed epoch.
        ///
        /// There is deliberately no counterpart for a failed probe. CTX-010 makes
        /// the candidate attempt-local, so a discarded one never became a fact and
        /// has nothing to roll back.
        | PrefixRebaseCommitted of
            {| SessionId: SessionId
               PreviousEpochId: PrefixEpochId
               NextEpochId: PrefixEpochId
               FrozenRecordPrefixRef: BlobRef
               FrozenRecordPrefixDigest: BlobDigest
               CutoffExclusive: int
               CoveredPrefixDigest: string
               SealRoot: string
               SyntheticMessageId: string
               ProbeId: string
               SolvingProviderRun: ProviderRunIdentity |}

        /// HOST-006 containment: a Host compaction was observed, so the prefix
        /// epoch is retired and Companion coverage is zeroed.
        ///
        /// `ObservedCompactionRun` is which message PROVES it happened — a
        /// physical fact. There is no reason or source field: CTX-005 forbids
        /// classifying, and a user's `/compact` and an unexpected Host compaction
        /// get identical handling, so a discriminator would only grow a branch
        /// that never executes.
        | ContextReanchored of
            {| SessionId: SessionId
               PreviousEpochId: PrefixEpochId
               NextEpochId: PrefixEpochId
               ObservedCompactionRun: ProviderRunIdentity |}

        // There is deliberately no `CompanionEpochSwitched`. COMPANION-009's epoch has
        // exactly two movers now — `PrefixRebaseCommitted` (CTX-012) and
        // `ContextReanchored` (HOST-006) — and the old fact was a third: it carried the
        // FrozenRecordPrefix text inline and was written from a token-budget comparison, which
        // CTX-001 and CTX-002 both forbid. Its replacements carry a `BlobRef` instead
        // (PERSIST-007) and are driven by a real attempt outcome.

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
