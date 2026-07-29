namespace Wanxiangshu.Next.Kernel

open System

/// Typed identities. The cheapest border guard: if two concepts share a
/// primitive type, the compiler cannot tell them apart and every function
/// signature becomes a promise nobody checks.
///
/// PROMPT-001 is the reason this module exists at all: `PhysicalUserMessage ≠
/// AuthorityTurn`. As long as both are `string`, or share one generic message-id
/// type, that clause is a comment. Here it is a type error.
module Identity =

    // ── runtime and transport ───────────────────────────────────────────────

    /// One plugin process incarnation. Journal envelopes are ordered per runtime.
    type RuntimeId = private RuntimeId of string

    /// A Host session. Never a child, never a PTY.
    type SessionId = private SessionId of string

    /// A Host session created as a direct child through a fork runtime.
    type ChildId = private ChildId of string

    /// An OS process the plugin owns.
    type ProcessId = private ProcessId of string

    type EventId = private EventId of string

    /// Monotonic per-runtime append counter. Ordering within a runtime is by
    /// this; ordering across runtimes is by ObservedAt (PERSIST-001).
    type LocalSeq = private LocalSeq of int64

    type LocalEpoch = int64
    type ObservedAt = DateTimeOffset

    // ── prompt authority (SSOT/03) ──────────────────────────────────────────

    /// A complete conversation sequence caused by one Authority Root
    /// (PROMPT-002). Continuations extend it; they never create a new one.
    type LogicalRunId = private LogicalRunId of string

    /// The physical user message that became an Authority Root. Only this
    /// identity may change the execution profile (PROMPT-002).
    type AuthorityRootUserMessageId = private AuthorityRootUserMessageId of string

    /// A `role=user` message on the wire. Transport format, not authority
    /// (PROMPT-001). Zero-width characters, whitespace, templates, timing and
    /// text length are not identity evidence.
    type PhysicalUserMessageId = private PhysicalUserMessageId of string

    /// Stable idempotency key for one dispatched prompt (PROMPT-011). Written
    /// into Host prompt metadata so an unresolved claim can be reconciled after
    /// a crash. Derived, never invented at the call site.
    type PromptKey = private PromptKey of string

    /// What the Host prompt-send endpoint hands back: an `accepted-*` admission
    /// receipt.
    ///
    /// PROMPT-005 forbids treating this as a physical message id. Keeping it in
    /// its own type is how that clause is enforced rather than remembered —
    /// there is no function from a receipt to any message identity.
    type TransportReceipt = private TransportReceipt of string

    // ── provider runs (SSOT/07) ─────────────────────────────────────────────

    /// One provider request and the assistant message it produces.
    ///
    /// HOST-010 and HOST-011: the Host creates and persists the assistant
    /// message before triggering `messages.transform`, and hands the same id to
    /// every tool call in that run as `ToolContext.messageID`. So one assistant
    /// message id is exactly one provider run, and this is the only per-run
    /// identity the SDK exposes — `modelID` and `providerID` are configuration
    /// labels, identical across every run of a session.
    ///
    /// SSOT names this concept twice (`ProviderRunIdentity` in HOST-010 and
    /// REVIEW-004, `ProviderAttemptIdentity` in PROMPT-008 and FALLBACK-007).
    /// One Host assistant message is one provider request is one attempt, so
    /// there is one type here. Two types would be a double model.
    type ProviderRunIdentity = private ProviderRunIdentity of string

    /// One tool invocation inside a provider run. `ToolContext.callID`.
    ///
    /// Distinct from ProviderRunIdentity because REVIEW-004 requires both: two
    /// PERFECT verdicts must differ in run AND in call. Sharing a type would
    /// make that check expressible but meaningless.
    type ToolCallId = private ToolCallId of string

    /// Which system prompt a provider request carried (PROMPT-008).
    ///
    /// An id, not the text. The prompt body lives in `prompts/*.md` and is
    /// loaded at the Host boundary; carrying it in the profile would put a
    /// multi-kilobyte string into every diagnostic and every journal line that
    /// mentions an attempt.
    type SystemPromptId = private SystemPromptId of string

    // ── review (SSOT/05) ────────────────────────────────────────────────────

    /// One review barrier: the question "is this tree good?" asked once.
    type ReviewBarrierId = private ReviewBarrierId of string

    /// A Git tree hash. Not a commit — REVIEW-008 invalidates a witness on tree
    /// change, and two commits can share a tree.
    type GitTreeHash = private GitTreeHash of string

    /// Digest of the canonical provider input for one run (REVIEW-010). The
    /// evidence that a second PERFECT actually consumed the first challenge.
    type SealDigest = private SealDigest of string

    // ── execution handles (SSOT/09) ─────────────────────────────────────────

    /// A forked agent child, persisted across restart (EXEC-009).
    type AgentHandleId = private AgentHandleId of string

    /// A PTY session. DevOps only (AGENT-013).
    type PtyHandleId = private PtyHandleId of string

    /// One orchestrated Manager job: one worktree, one Manager (ORCH-003).
    type ManagerJobId = private ManagerJobId of string

    /// EXEC-009 requires typed handles per resource kind. The union exists so a
    /// join mailbox can hold all three without erasing which kind it holds; the
    /// cases are not interchangeable.
    [<RequireQualifiedAccess>]
    type HandleId =
        | Agent of AgentHandleId
        | Pty of PtyHandleId
        | ManagerJob of ManagerJobId

    // ── git and worktrees (SSOT/06) ─────────────────────────────────────────

    /// Stable identity of a worktree, independent of where it currently lives.
    /// ORCH-006 records both: recovery locates by identity, diagnostics show the
    /// path. A path is mutable state; an identity is not.
    type WorktreeIdentity = private WorktreeIdentity of string

    /// Filesystem location of a worktree. Diagnostic only.
    type WorktreePath = private WorktreePath of string

    /// A branch reference frozen at fork time via `git symbolic-ref`
    /// (ORCH-008). Never resolved to HEAD on failure.
    type TargetRef = private TargetRef of string

    /// A commit. Separate from TargetRef because ORCH-007's recovery compares a
    /// ref's current head against an expected commit; one type for both would
    /// make that comparison a tautology.
    type CommitHash = private CommitHash of string

    // ── constructors and accessors ──────────────────────────────────────────
    //
    // `create` is total: validation belongs to the boundary that has the Host
    // context to fail closed with a reason (HOST-001), not to a string wrapper.

    module RuntimeId =
        let create (value: string) = RuntimeId value
        let value (RuntimeId v) = v

    module SessionId =
        let create (value: string) = SessionId value
        let value (SessionId v) = v

    module ChildId =
        let create (value: string) = ChildId value
        let value (ChildId v) = v
        /// A child is a Host session, so it can be addressed as one.
        let toSessionId (ChildId v) = SessionId v

    module ProcessId =
        let create (value: string) = ProcessId value
        let value (ProcessId v) = v

    module EventId =
        let create (value: string) = EventId value
        let value (EventId v) = v

    module LocalSeq =
        let create (v: int64) = LocalSeq v
        let value (LocalSeq v) = v

    module LogicalRunId =
        let create (value: string) = LogicalRunId value
        let value (LogicalRunId v) = v

    module AuthorityRootUserMessageId =
        let create (value: string) = AuthorityRootUserMessageId value
        let value (AuthorityRootUserMessageId v) = v

    module PhysicalUserMessageId =
        let create (value: string) = PhysicalUserMessageId value
        let value (PhysicalUserMessageId v) = v

        /// Promote a physical message to Authority Root.
        ///
        /// Deliberately explicit and one-way. PROMPT-005 allows this only once
        /// `PhysicalAccepted` is proven, so every call site is a place where
        /// that proof must exist. There is no inverse: an Authority Root is a
        /// semantic fact, not a wire address.
        let promoteToAuthorityRoot (PhysicalUserMessageId v) = AuthorityRootUserMessageId v

    module PromptKey =
        let create (value: string) = PromptKey value
        let value (PromptKey v) = v

    module TransportReceipt =
        let create (value: string) = TransportReceipt value
        let value (TransportReceipt v) = v

        /// Host admission ids are `accepted-*`. A receipt that does not look
        /// like one means the Host returned something else and the caller must
        /// decide, so this is a predicate rather than a validating constructor.
        let isAdmissionShaped (TransportReceipt v) =
            v.StartsWith("accepted-", StringComparison.Ordinal)

    module ProviderRunIdentity =
        let create (value: string) = ProviderRunIdentity value
        let value (ProviderRunIdentity v) = v

    module ToolCallId =
        let create (value: string) = ToolCallId value
        let value (ToolCallId v) = v

    module SystemPromptId =
        let create (value: string) = SystemPromptId value
        let value (SystemPromptId v) = v

    module ReviewBarrierId =
        let create (value: string) = ReviewBarrierId value
        let value (ReviewBarrierId v) = v

    module GitTreeHash =
        let create (value: string) = GitTreeHash value
        let value (GitTreeHash v) = v

    module SealDigest =
        let create (value: string) = SealDigest value
        let value (SealDigest v) = v

    module AgentHandleId =
        let create (value: string) = AgentHandleId value
        let value (AgentHandleId v) = v

    module PtyHandleId =
        let create (value: string) = PtyHandleId value
        let value (PtyHandleId v) = v

    module ManagerJobId =
        let create (value: string) = ManagerJobId value
        let value (ManagerJobId v) = v

    module HandleId =
        /// Diagnostic rendering only (HOST-007). Never parsed back: EXEC-009
        /// forbids turning a retired handle string into a fresh fork target.
        let describe (handle: HandleId) =
            match handle with
            | HandleId.Agent id -> "agent:" + AgentHandleId.value id
            | HandleId.Pty id -> "pty:" + PtyHandleId.value id
            | HandleId.ManagerJob id -> "manager-job:" + ManagerJobId.value id

    module WorktreeIdentity =
        let create (value: string) = WorktreeIdentity value
        let value (WorktreeIdentity v) = v

    module WorktreePath =
        let create (value: string) = WorktreePath value
        let value (WorktreePath v) = v

    module TargetRef =
        let create (value: string) = TargetRef value
        let value (TargetRef v) = v

    module CommitHash =
        let create (value: string) = CommitHash value
        let value (CommitHash v) = v

    // ── composite identities ────────────────────────────────────────────────

    /// Dedupe key for one failed provider attempt (FALLBACK-003). The same
    /// failure observed twice — a retry signal plus an idle reconcile — must
    /// advance the cursor once.
    ///
    /// Scoped by Logical Run and Authority Root so a new Authority Root starts a
    /// fresh cursor (FALLBACK-001) without any explicit reset.
    type FallbackAttemptIdentity =
        { SessionId: SessionId
          LogicalRunId: LogicalRunId
          AuthorityRootUserMessageId: AuthorityRootUserMessageId
          ProviderRun: ProviderRunIdentity }

    /// Identity of one PERFECT verdict (REVIEW-004). Extra PERFECT calls inside
    /// the same provider run do not count and are not journalled, which is
    /// exactly "same Run, different ToolCall" being representable here.
    type ReviewAttemptIdentity =
        { ReviewBarrierId: ReviewBarrierId
          GitTreeHash: GitTreeHash
          ReviewerSessionId: SessionId
          ProviderRun: ProviderRunIdentity
          ToolCallId: ToolCallId }

    module FallbackAttemptIdentity =
        /// Stable string form for set membership in a projection.
        let dedupeKey (identity: FallbackAttemptIdentity) =
            String.Join(
                "\u001f",
                [| SessionId.value identity.SessionId
                   LogicalRunId.value identity.LogicalRunId
                   AuthorityRootUserMessageId.value identity.AuthorityRootUserMessageId
                   ProviderRunIdentity.value identity.ProviderRun |]
            )

    module ReviewAttemptIdentity =
        let dedupeKey (identity: ReviewAttemptIdentity) =
            String.Join(
                "\u001f",
                [| ReviewBarrierId.value identity.ReviewBarrierId
                   GitTreeHash.value identity.GitTreeHash
                   SessionId.value identity.ReviewerSessionId
                   ProviderRunIdentity.value identity.ProviderRun
                   ToolCallId.value identity.ToolCallId |]
            )

        /// REVIEW-003 conditions 4 and 5: the second PERFECT must come from a
        /// different provider run AND a different tool call. Same barrier, same
        /// tree, same reviewer session.
        let isDistinctAttempt (first: ReviewAttemptIdentity) (second: ReviewAttemptIdentity) =
            first.ReviewBarrierId = second.ReviewBarrierId
            && first.GitTreeHash = second.GitTreeHash
            && first.ReviewerSessionId = second.ReviewerSessionId
            && first.ProviderRun <> second.ProviderRun
            && first.ToolCallId <> second.ToolCallId
