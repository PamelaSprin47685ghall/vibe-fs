namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Sphinx

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Durable domain facts (docs/what/persist.md).
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

    /// REVIEW-001: the judge tool's verdict argument accepts exactly these two values and no
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

    /// EXEC-009: why a handle left Active/CompletedAwaitingJoin without a joinable
    /// completion cell. Distinct from `HandleCompletionKind.Cancelled`, which still
    /// lands in `CompletedAwaitingJoin` and may be joined as an empty body.
    ///
    /// Only irreversible loss. Never encode loop-detect interrupt, Host abort that
    /// will continue (LOOP-006), ProviderRetry, or any wake that keeps the run Active.
    /// Interrupt ≠ terminal; abandon is terminal without join.
    [<RequireQualifiedAccess>]
    type HandleAbandonReason =
        /// Parent cancelled the owned resource (cancelChildren).
        | ParentCancelled
        /// Management/process deadline elapsed without a settled completion.
        | DeadlineExceeded
        /// Child Host session is gone and cannot be recovered.
        | HostSessionGone

    /// Clean-break: why a durable completion cell was rejected as false finality.
    /// Legacy abort blobs are Host observations written as if they were terminals.
    [<RequireQualifiedAccess>]
    type FalseCompletionReason = | LegacyAbortWasObservation

    /// Durable agent facts by bounded context (DSL-003). The journal's single
    /// top-level dispatch is `AgentFact` over these families: each family owns
    /// its cases and fold branch, so no caller depends on a 54-case global event
    /// catalogue. The wire shape is byte-identical to the former flat union —
    /// Thoth encodes a case name and payload, never the declaring type — so no
    /// journal migration is needed. Each family ships with a same-named module
    /// of `PluginPromptClaimed`-style functions, which are the ONLY way to build
    /// an `AgentFact`: they wrap the family case so every existing construction
    /// site keeps its exact source form.

    type PromptFactCases =

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

    type FallbackFactCases =

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

    type ReviewFactCases =

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

        /// REVIEW-013/017: the reviewer's reconciled turn that carried a verdict
        /// has fully completed and its XTrace converged. `FrozenFrontierSequence`
        /// is the exclusive XTraceCursor.Sequence captured at closure time —
        /// consumers (TodoReviewConcluded) must take their record frontier from
        /// here, never from the session's current head, so a finished attempt's
        /// tail cannot leak into the next barrier's request range.
        | ReviewAttemptClosed of
            {| ReviewerSessionId: SessionId
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash
               ProviderRun: ProviderRunIdentity
               ToolCallId: ToolCallId
               FrozenFrontierSequence: int64 |}

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

    /// Who owns a forked child handle (GLORY-002 / SURFACE-006).
    ///
    /// A `DurableParentHandle` is an ordinary child: it appears in the parent's
    /// `list` / `join` / background guard and is restored into the parent's
    /// runtime after a restart. A `HostOwnedHidden` handle belongs to a
    /// Host-owned workflow (the hidden Finality Reviewer): it stays out of every
    /// parent-visible surface and is never restored into a parent runtime.
    [<RequireQualifiedAccess>]
    type HandleOwnership =
        | DurableParentHandle
        | HostOwnedHidden

    type ExecutionFactCases =

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
               Byname: string
               CanonicalRole: Role
               Ownership: HandleOwnership |}

        /// `CompletionRef` / `CompletionDigest` locate the durable join payload
        /// (EXEC-009). Written before the fact. `Cancelled` carries `None`: parent
        /// abort has no body to join. 0.5.1 lines missing the fields migrate to
        /// `None` on read (forward-compatible).
        | HandleCompleted of
            {| ParentSessionId: SessionId
               Handle: HandleId
               Kind: HandleCompletionKind
               CompletionRef: BlobRef option
               CompletionDigest: BlobDigest option |}

        /// The durable tombstone. EXEC-009: a retired id returns RetiredHandle
        /// forever and must never degrade into "treat the input as an agent name
        /// and fork again".
        | HandleRetired of
            {| ParentSessionId: SessionId
               Handle: HandleId |}

        /// EXEC-009: handle left the join protocol without a joinable completion.
        /// Single-assignment into `HandleLifecycle.Abandoned`; not joinable; no reverse.
        | HandleAbandoned of
            {| ParentSessionId: SessionId
               Handle: HandleId
               Reason: HandleAbandonReason
               AbandonedAt: DateTimeOffset |}

        /// Clean-break: durable completion cell held a legacy abort observation
        /// (blob status=aborted), not a proven business terminal. Fold may revert
        /// CompletedAwaitingJoin → Active only when ref/digest match exactly.
        | HandleFalseCompletionRejected of
            {| ParentSessionId: SessionId
               Handle: HandleId
               ExpectedCompletionRef: BlobRef
               ExpectedCompletionDigest: BlobDigest
               Reason: FalseCompletionReason |}

        /// Clean-break: parent already retired a false terminal. Records the bad
        /// cell so replacement migration is pure and idempotent.
        | HandleFalseTerminalReported of
            {| ParentSessionId: SessionId
               Handle: HandleId
               BadCompletionRef: BlobRef
               BadCompletionDigest: BlobDigest
               Reason: FalseCompletionReason |}

        /// Clean-break: parent was notified that a prior aborted join result is void;
        /// child continues under a deterministic replacement handle.
        | ParentJoinCorrectionRequested of
            {| ParentSessionId: SessionId
               OriginalHandle: HandleId
               ReplacementHandle: HandleId
               BadCompletionDigest: BlobDigest |}

        /// Durable observation that a Host turn reached a terminal snapshot.
        /// Idempotent identity = SessionId + ProviderRun (when present). Wake only;
        /// business completion still derives from full snapshot (ARCH-002).
        | HostTurnObserved of
            {| SessionId: SessionId
               ProviderRun: ProviderRunIdentity option
               ObservedAt: DateTimeOffset |}

    type OrchestratorFactCases =

        // Each fact determines exactly one recovery action (ORCH-007). The old
        // set was stage-like: CandidateRegistered could mean "waiting for
        // review" or "ready to publish", which is precisely the branch ORCH-006
        // forbids.

        | ManagerJobCreated of
            {| ManagerJobId: ManagerJobId
               ManagerSessionId: SessionId
               ManagerAgent: string
               Byname: string
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

        // ── Durable effects (PERSIST-009) ───────────────────────────────────
        // Typed domain facts, same protocol: Requested → effect → Accepted.
        // Effect identity = WorktreeIdentity (`WorktreeCommands.identityOf`).
        // After a crash, Requested-without-Created is treated as not happened;
        // reconcile is git worktree list --porcelain / OrchestratorSweep.

        | WorktreeCreateRequested of
            {| ManagerJobId: ManagerJobId
               WorktreeIdentity: WorktreeIdentity
               WorktreePath: WorktreePath |}

        | WorktreeCreated of
            {| ManagerJobId: ManagerJobId
               WorktreeIdentity: WorktreeIdentity
               WorktreePath: WorktreePath |}

    type CompanionFactCases =

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

        // ── lifecycle work record (HOST-005) ───────────────────────────────────────

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
               ProviderRun: ProviderRunIdentity option
               ToolCallId: ToolCallId option
               HostToolPartId: HostToolPartId option |}

        /// COMPANION-003: the Session's terminal output, captured verbatim at
        /// reconcile. Idempotent and never overwritten. The body goes to a blob.
        | TerminalOutputCaptured of
            {| SessionId: SessionId
               TextRef: BlobRef
               TextDigest: BlobDigest
               ProviderRun: ProviderRunIdentity |}

    type ContextFactCases =

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
        /// Tip v2 (ENFORCER-020..026 / 045): TipRuleId is the stable catalog
        /// identity; FieldNameAtCommit is an optional audit snapshot.
        /// ScoreVectorRef is deleted (ENFORCER-072).
        | BlogObservationCommitted of
            {| SessionId: SessionId
               BloggerSessionId: SessionId
               RequestId: BloggerRequestId
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
               TipRuleId: string
               FieldNameAtCommit: string option
               EvidenceRef: BlobRef option
               ObservedPrefixEpochId: PrefixEpochId |}

        /// CTX-012: a valid squash rewrote the oldest frames. Permanent once
        /// committed, even if the same slot's main request then fails.
        ///
        /// Carries no coverage fields: a squash changes how B is REPRESENTED, not
        /// which X turns it covers. Including them would let a writer silently
        /// move coverage under cover of a compression.
        | BlogObservationsSquashed of
            {| SessionId: SessionId
               BloggerSessionId: SessionId
               RequestId: BloggerRequestId
               PreviousFrameEpochId: FrameEpochId
               NextFrameEpochId: FrameEpochId
               CoveredFrameCount: int
               TextRef: BlobRef
               TextDigest: BlobDigest
               ProviderRun: ProviderRunIdentity |}

        /// C5: one external Blogger request's irrecomputable semantic input,
        /// written BEFORE physical send. Not a program counter — recovery reads
        /// this + Host snapshot + receipts, never reverse-parses TOML or guesses X.
        | BloggerRequestMaterialized of
            {| RequestId: BloggerRequestId
               MainSessionId: SessionId
               BloggerSessionId: SessionId
               RequestKind: string
               ContextRef: BlobRef
               ContextDigest: BlobDigest
               ObservedPrefixEpochId: PrefixEpochId
               PreviousIngestedThroughSequence: int64
               NextIngestedThroughSequence: int64
               FrameEpochId: FrameEpochId
               SelectedFrameDigests: BlobDigest list
               PromptKey: PromptKey option |}

        /// C5: open request abandoned (send failed, dispose, explicit fail).
        /// Clears the open materialization without producing an Entry/Squash.
        | BloggerRequestAbandoned of
            {| RequestId: BloggerRequestId
               MainSessionId: SessionId
               BloggerSessionId: SessionId
               Reason: string |}

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

    /// Rulebook Main tip presentation for auto-injected guidance (Full vs IdentityOnly).
    [<RequireQualifiedAccess>]
    type TipPresentation =
        | Full
        | IdentityOnly

    /// HOST-013: permanent auto-injected pairs for one provider transcript.
    type HostFactCases =
        /// One permanent auto-injected pair was anchored.
        ///
        /// `Ordinal` is the transcript-local append counter (1-based). `CallId` is
        /// stable across restarts. `MarkerText` is the exact tool-result body the
        /// provider saw for this pair — history restores these bytes verbatim.
        /// `CallGap` / `ResultGap` anchor the two halves to transcript positions:
        /// the bracket spans one real tool batch (`real calls → synthetic call →
        /// real results → synthetic result`). A pair's identity, bytes and both
        /// placements must commit atomically — two split facts would leave a
        /// crash-time half-pair.
        | PairProgrammingGuidelineAnchored of
            {| SessionId: SessionId
               Ordinal: int64
               CallId: ToolCallId
               MarkerText: string
               CallGap: TranscriptGap
               ResultGap: TranscriptGap |}

        /// Main session received tip guidance (Full main.md or IdentityOnly name).
        /// Folded into TipDeliveryProjection so first/repeat is restart-safe.
        | TipGuidanceDelivered of
            {| SessionId: SessionId
               TipName: string
               Presentation: TipPresentation |}

        | SessionStartedAtBound of
            {| SessionId: SessionId
               StartedAt: DateTimeOffset |}

    type DelegationFactCases =
        | DelegatedToolEstimateReplaced of
            {| SessionId: SessionId
               ExpectedToolCalls: int |}
        | DelegatedToolCallObserved of
            {| SessionId: SessionId
               ToolCallId: ToolCallId |}

    /// INTRA-PARTICIPANT-PARALLELISM: durable facts for one logical participant
    /// temporarily executing through several coequal physical presents. Physical
    /// lane SessionIds are recovery identities only; they never become public handles.
    [<RequireQualifiedAccess>]
    type FissionFactCases =
        | FissionAdmitted of
            {| GroupId: string
               OwnerSessionId: SessionId
               ParentSessionId: SessionId option
               OriginToolCallId: ToolCallId
               LaneCount: int
               LaneSessions: SessionId list
               LanePrompts: string list
               OwnerWorkRecordRef: BlobRef
               OwnerWorkRecordDigest: BlobDigest
               PreFissionCompletionIds: string list |}
        | FissionLaneMaterialized of
            {| GroupId: string
               OwnerSessionId: SessionId
               LaneIndex: int
               LaneSessionId: SessionId
               ProviderRun: ProviderRunIdentity
               WorkRecordRef: BlobRef
               WorkRecordDigest: BlobDigest |}
        | FissionCompletionCaptured of
            {| GroupId: string
               OwnerSessionId: SessionId
               CompletionId: string
               PayloadRef: BlobRef
               PayloadDigest: BlobDigest |}
        | FissionCompletionDelivered of
            {| GroupId: string
               OwnerSessionId: SessionId
               CompletionId: string
               LaneIndex: int |}
        | FissionExternalAffinityBound of
            {| GroupId: string
               OwnerSessionId: SessionId
               ExternalId: string
               LaneIndex: int |}
        | FissionConverged of
            {| GroupId: string
               OwnerSessionId: SessionId
               TerminalLaneSessionId: SessionId
               TerminalProviderRun: ProviderRunIdentity
               AggregateWorkRecordRef: BlobRef
               AggregateWorkRecordDigest: BlobDigest |}
        | FissionFailed of
            {| GroupId: string
               OwnerSessionId: SessionId
               Reason: string |}

    // There is deliberately no `CompanionEpochSwitched`. COMPANION-009's epoch has
    // exactly two movers now — `PrefixRebaseCommitted` (CTX-012) and
    // `ContextReanchored` (HOST-006) — and the old fact was a third: it carried the
    // FrozenRecordPrefix text inline and was written from a token-budget comparison, which
    // CTX-001 and CTX-002 both forbid. Its replacements carry a `BlobRef` instead
    // (PERSIST-007) and are driven by a real attempt outcome.

    /// One journal line for the agent domain: exactly one family. The family
    /// case is dispatch data for replay, not a program counter (PERSIST-010).
    /// DSL-class: DurableFact — bounded-context dispatch over immutable facts.
    [<RequireQualifiedAccess>]
    type AgentFact =
        | Prompt of PromptFactCases
        | Fallback of FallbackFactCases
        | Review of ReviewFactCases
        | Execution of ExecutionFactCases
        | Orchestrator of OrchestratorFactCases
        | Companion of CompanionFactCases
        | Context of ContextFactCases
        | Host of HostFactCases
        | Fission of FissionFactCases
        | Delegation of DelegationFactCases

    module DelegationFact =
        let inline DelegatedToolEstimateReplaced payload =
            AgentFact.Delegation(DelegationFactCases.DelegatedToolEstimateReplaced payload)

        let inline DelegatedToolCallObserved payload =
            AgentFact.Delegation(DelegationFactCases.DelegatedToolCallObserved payload)

    module FissionFact =
        let inline FissionAdmitted payload =
            AgentFact.Fission(FissionFactCases.FissionAdmitted payload)

        let inline FissionLaneMaterialized payload =
            AgentFact.Fission(FissionFactCases.FissionLaneMaterialized payload)

        let inline FissionCompletionCaptured payload =
            AgentFact.Fission(FissionFactCases.FissionCompletionCaptured payload)

        let inline FissionCompletionDelivered payload =
            AgentFact.Fission(FissionFactCases.FissionCompletionDelivered payload)

        let inline FissionExternalAffinityBound payload =
            AgentFact.Fission(FissionFactCases.FissionExternalAffinityBound payload)

        let inline FissionConverged payload =
            AgentFact.Fission(FissionFactCases.FissionConverged payload)

        let inline FissionFailed payload =
            AgentFact.Fission(FissionFactCases.FissionFailed payload)

    /// HOST-013 constructor surface.
    module HostFact =
        let inline PairProgrammingGuidelineAnchored payload =
            AgentFact.Host(HostFactCases.PairProgrammingGuidelineAnchored payload)

        let inline TipGuidanceDelivered payload =
            AgentFact.Host(HostFactCases.TipGuidanceDelivered payload)

        let inline SessionStartedAtBound payload =
            AgentFact.Host(HostFactCases.SessionStartedAtBound payload)

    /// Constructor surface for the PromptFact family: each function wraps its
    /// family case in the single-case Prompt dispatch.
    module PromptFact =
        let inline PluginPromptClaimed payload =
            AgentFact.Prompt(PromptFactCases.PluginPromptClaimed payload)

        let inline PluginPromptSubmitted payload =
            AgentFact.Prompt(PromptFactCases.PluginPromptSubmitted payload)

        let inline PluginPromptPhysicalAccepted payload =
            AgentFact.Prompt(PromptFactCases.PluginPromptPhysicalAccepted payload)

        let inline PluginPromptAbandoned payload =
            AgentFact.Prompt(PromptFactCases.PluginPromptAbandoned payload)

        let inline AuthorityRootAccepted payload =
            AgentFact.Prompt(PromptFactCases.AuthorityRootAccepted payload)

    /// Constructor surface for the FallbackFact family: each function wraps its
    /// family case in the single-case Fallback dispatch.
    module FallbackFact =
        let inline FallbackCursorAdvanced payload =
            AgentFact.Fallback(FallbackFactCases.FallbackCursorAdvanced payload)

        let inline FallbackExhausted payload =
            AgentFact.Fallback(FallbackFactCases.FallbackExhausted payload)

    /// Constructor surface for the ReviewFact family: each function wraps its
    /// family case in the single-case Review dispatch.
    module ReviewFact =
        let inline ReviewBarrierStarted payload =
            AgentFact.Review(ReviewFactCases.ReviewBarrierStarted payload)

        let inline ReviewVerdictRecorded payload =
            AgentFact.Review(ReviewFactCases.ReviewVerdictRecorded payload)

        let inline ReviewAttemptClosed payload =
            AgentFact.Review(ReviewFactCases.ReviewAttemptClosed payload)

        let inline PerfectChallengeIssued payload =
            AgentFact.Review(ReviewFactCases.PerfectChallengeIssued payload)

        let inline ProviderInputSealed payload =
            AgentFact.Review(ReviewFactCases.ProviderInputSealed payload)

        let inline ConfirmedReviewWitness payload =
            AgentFact.Review(ReviewFactCases.ConfirmedReviewWitness payload)

    /// Constructor surface for the ExecutionFact family: each function wraps its
    /// family case in the single-case Execution dispatch.
    module ExecutionFact =
        let inline HandleLinked payload =
            AgentFact.Execution(ExecutionFactCases.HandleLinked payload)

        let inline HandleCompleted payload =
            AgentFact.Execution(ExecutionFactCases.HandleCompleted payload)

        let inline HandleRetired payload =
            AgentFact.Execution(ExecutionFactCases.HandleRetired payload)

        let inline HandleAbandoned payload =
            AgentFact.Execution(ExecutionFactCases.HandleAbandoned payload)

        let inline HandleFalseCompletionRejected payload =
            AgentFact.Execution(ExecutionFactCases.HandleFalseCompletionRejected payload)

        let inline HandleFalseTerminalReported payload =
            AgentFact.Execution(ExecutionFactCases.HandleFalseTerminalReported payload)

        let inline ParentJoinCorrectionRequested payload =
            AgentFact.Execution(ExecutionFactCases.ParentJoinCorrectionRequested payload)

        let inline HostTurnObserved payload =
            AgentFact.Execution(ExecutionFactCases.HostTurnObserved payload)

    /// Constructor surface for the OrchestratorFact family: each function wraps its
    /// family case in the single-case Orchestrator dispatch.
    module OrchestratorFact =
        let inline ManagerJobCreated payload =
            AgentFact.Orchestrator(OrchestratorFactCases.ManagerJobCreated payload)

        let inline CandidateReady payload =
            AgentFact.Orchestrator(OrchestratorFactCases.CandidateReady payload)

        let inline ConflictDetected payload =
            AgentFact.Orchestrator(OrchestratorFactCases.ConflictDetected payload)

        let inline RebasedCandidateReady payload =
            AgentFact.Orchestrator(OrchestratorFactCases.RebasedCandidateReady payload)

        let inline PublishClaimed payload =
            AgentFact.Orchestrator(OrchestratorFactCases.PublishClaimed payload)

        let inline Published payload =
            AgentFact.Orchestrator(OrchestratorFactCases.Published payload)

        let inline JobFailed payload =
            AgentFact.Orchestrator(OrchestratorFactCases.JobFailed payload)

        let inline JobAbandoned payload =
            AgentFact.Orchestrator(OrchestratorFactCases.JobAbandoned payload)

        let inline WorktreeCreateRequested payload =
            AgentFact.Orchestrator(OrchestratorFactCases.WorktreeCreateRequested payload)

        let inline WorktreeCreated payload =
            AgentFact.Orchestrator(OrchestratorFactCases.WorktreeCreated payload)

    /// Constructor surface for the CompanionFact family: each function wraps its
    /// family case in the single-case Companion dispatch.
    module CompanionFact =
        let inline CompanionBloggerLinked payload =
            AgentFact.Companion(CompanionFactCases.CompanionBloggerLinked payload)

        let inline CompanionBloggerClosed payload =
            AgentFact.Companion(CompanionFactCases.CompanionBloggerClosed payload)

        let inline OpeningPromptCaptured payload =
            AgentFact.Companion(CompanionFactCases.OpeningPromptCaptured payload)

        let inline XTracePartAppended payload =
            AgentFact.Companion(CompanionFactCases.XTracePartAppended payload)

        let inline TerminalOutputCaptured payload =
            AgentFact.Companion(CompanionFactCases.TerminalOutputCaptured payload)

    /// Constructor surface for the ContextFact family: each function wraps its
    /// family case in the single-case Context dispatch.
    module ContextFact =
        let inline BlogObservationCommitted payload =
            AgentFact.Context(ContextFactCases.BlogObservationCommitted payload)

        let inline BlogObservationsSquashed payload =
            AgentFact.Context(ContextFactCases.BlogObservationsSquashed payload)

        let inline BloggerRequestMaterialized payload =
            AgentFact.Context(ContextFactCases.BloggerRequestMaterialized payload)

        let inline BloggerRequestAbandoned payload =
            AgentFact.Context(ContextFactCases.BloggerRequestAbandoned payload)

        let inline PrefixRebaseCommitted payload =
            AgentFact.Context(ContextFactCases.PrefixRebaseCommitted payload)

        let inline ContextReanchored payload =
            AgentFact.Context(ContextFactCases.ContextReanchored payload)

    // ── Manager lifecycle (docs/what/glory.md GLORY-010) ────────────────────────────────
    //
    // One Manager Life: LifeOpened → WorkActivated → FinalityRequested →
    // (FinalityReviewerEnlisted…, FinalityRejected loop) → FinalityBlessed →
    // LifeCompleted. Every fact is an event that HAPPENED; no fact carries a
    // next step (ARCH-001).

    /// GLORY-010: the Manager lifecycle fact algebra.
    [<RequireQualifiedAccess>]
    type ManagerLifecycleFact =
        /// A Life opened for a new HumanRoot (GLORY-012/013). Opening text is
        /// blob-addressed; `OpeningCursorSequence` is the XTraceCursor.Sequence
        /// of its first XTrace part (Kernel stays free of Domain types).
        | LifeOpened of
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               OpeningUserMessageId: PhysicalUserMessageId
               OpeningTextRef: BlobRef
               OpeningTextDigest: BlobDigest
               OpeningCursorSequence: int64 |}

        /// Activation was physically accepted; the compression floor is fixed
        /// after the Activation prompt's XTrace end (GLORY-021).
        | WorkActivated of
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               ActivationPromptKey: PromptKey
               ProtectedPrefixEndSequence: int64 |}

        /// A legal suicide was accepted (GLORY-040). Reviewer not yet created,
        /// so no barrier here.
        | FinalityRequested of
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               RequestId: FinalityRequestId
               GitTreeHash: GitTreeHash
               LastWordsRef: BlobRef
               LastWordsDigest: BlobDigest
               ProviderRun: ProviderRunIdentity
               ToolCallId: ToolCallId |}

        /// One reviewer was enlisted into the request's cohort (GLORY-003/040/
        /// 045). `ReviewerOrdinal` is the member's stable position within the
        /// Life (used to order the blessing bundle); `IsNewReviewer` records
        /// whether this request created the session or re-enlisted a
        /// still-ungraduated historical reviewer.
        | FinalityReviewerEnlisted of
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               RequestId: FinalityRequestId
               ReviewerSessionId: SessionId
               ReviewerOrdinal: int
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash
               IsNewReviewer: bool |}

        /// REVISE: the rejecting reviewer's canonical work record is the wound
        /// record (GLORY-004/051). Blob-addressed; digest verified at write.
        /// The request closes immediately; the reviewer stays ungraduated.
        | FinalityRejected of
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               RequestId: FinalityRequestId
               RejectingReviewerSessionId: SessionId
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash
               WorkRecordRef: BlobRef
               WorkRecordDigest: BlobDigest |}

        /// GLORY-044: a later durable sibling REVISE was steered to the Manager
        /// as continuation (not merged into FinalityRejected). Does not change
        /// Resolution. Blob-addressed; digest verified at write.
        | FinalitySiblingSteered of
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               RequestId: FinalityRequestId
               ReviewerSessionId: SessionId
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash
               WorkRecordRef: BlobRef
               WorkRecordDigest: BlobDigest |}

        /// Every current member confirmed with fresh dual-PERFECT evidence; the
        /// stable-ordinal canonical work-record bundle is the minor-work
        /// evidence (GLORY-059/060). The Life is NOT completed here — the
        /// Manager keeps working until its second suicide (GLORY-061/062).
        | FinalityBlessed of
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               RequestId: FinalityRequestId
               GitTreeHash: GitTreeHash
               WorkRecordBundleRef: BlobRef
               WorkRecordBundleDigest: BlobDigest |}

        /// GLORY-057: infrastructure failure closed the request without a verdict.
        /// Closes the request so a new suicide is possible; never fabricates a
        /// wound record. `ReviewerSessionId` is the member whose attempt failed
        /// (or the Manager session when no reviewer exists yet).
        | FinalityUndecided of
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               RequestId: FinalityRequestId
               ReviewerSessionId: SessionId
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash |}

        /// The Life ended in glory: last_words is the terminal (GLORY-060).
        | LifeCompleted of
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               RequestId: FinalityRequestId
               TerminalRef: BlobRef
               TerminalDigest: BlobDigest |}

    type Fact =
        | Runtime of RuntimeFact
        | Agent of AgentFact
        | ManagerLifecycle of ManagerLifecycleFact
        /// Typed Magic Todo facts cross this earlier Kernel boundary as canonical codec bytes.
        | MagicTodo of payload: string
