namespace Wanxiangshu.Kernel

open System
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Completion payload for a successful agent run.
///
/// No transport parts are kept: business programs read the typed turn
/// (HOST-004), never a raw payload.
type AgentRunResult =
    {
        SessionId: SessionId
        AuthorityRootUserMessageId: AuthorityRootUserMessageId
        /// The provider run that reached terminal. HOST-011: this is the assistant
        /// message id, which is also what tool calls in that run observed.
        ProviderRun: ProviderRunIdentity
        Role: Role
        /// The worktree this run executed in, when it has one.
        ///
        /// `option`, not `""`. An empty path is not a directory, and every consumer
        /// had to re-test for blankness to find that out — `TurnCompletionProgram`
        /// did exactly that before passing it on, which means the sentinel was
        /// converted back into an option one layer later anyway.
        Directory: string option
        /// Terminal output for this run (HOST-005 / COMPANION-003): formal text
        /// plus host-visible reasoning. Excludes tool raw streams. Becomes the
        /// LWR Final output segment; not a parallel session-wide A channel.
        TerminalText: string
        /// This turn's formal assistant text only, without reasoning. Used for
        /// blogger terminal-validity checks (COMPANION-005).
        TurnFormalText: string
    }

    /// EXEC-006: a completed run must carry terminal output. Empty means the
    /// turn was not actually reconciled, so consumers must not treat it as done.
    member this.IsValid = not (String.IsNullOrWhiteSpace this.TerminalText)

type AgentRunFailure =
    { SessionId: SessionId; Reason: string }

module Outcome =

    /// The result of asking the Host to accept a prompt (PROMPT-005 `Submitted`).
    ///
    /// The two admitted cases are separate because the Host may return either an
    /// `accepted-*` admission receipt or a real message id, and PROMPT-005 gives
    /// them different authority: only a real physical message id may become an
    /// Authority Root. One case carrying an untyped id would erase that
    /// distinction at exactly the point it matters.
    type SendOutcome =
        /// Host accepted and returned only a transport receipt. Write
        /// `Submitted`; `PhysicalAccepted` still requires a real message.
        | AdmittedWithReceipt of TransportReceipt
        /// Host accepted and returned a real physical message identity.
        | AdmittedWithPhysicalMessage of PhysicalUserMessageId
        /// Transport failed in a way that proves the prompt was not accepted.
        | Retryable of reason: string
        /// PROMPT-011: acceptance cannot be proven either way. Stay Pending and
        /// never auto-resend — the contract is at-most-one logical effect.
        | AcceptanceUnknown of reason: string
        | Fatal of reason: string

    type SessionOutcome =
        | CompletedSession of message: string
        | CancelledSession
        | TerminatedSession of reason: string

    type SessionError =
        | NoProgress of reason: string
        | SessionCancelled
        /// FALLBACK-005: the automatic recovery budget is spent. Named for the
        /// budget rather than the cycle, because the A/A/B/B cursor itself never
        /// ends — and because `FallbackExhausted` is the journal fact, and one
        /// name for two concepts is how a double model starts.
        | AutoRecoveryExhausted
        | ReviewExhausted
        /// A dispatched prompt whose physical acceptance could not be proven.
        | PromptUncertain
        | ProjectionBroken of reason: string
        | InboxFull
        | Protocol of reason: string

    type JournalFailure =
        | WriteFailed of reason: string
        | FlushFailed of reason: string

    /// PERSIST-002: append has exactly two results. There is no partial write,
    /// so there is no third case to represent one.
    type CommitResult<'e> =
        | Committed of 'e
        | CommitUnknown of EventId * JournalFailure
