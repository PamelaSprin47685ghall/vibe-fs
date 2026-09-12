namespace Wanxiangshu.Foundation

open System
open Wanxiangshu.Foundation.Identity

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
        /// had to re-test for blankness to find that out — terminal completion
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
        /// PAR-005: the bounded automatic provider-recovery budget is spent.
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

    /// A writer that never attempted the caller's event. This is deliberately
    /// distinct from CommitUnknown: lifecycle refusal is known-not-committed.
    type JournalUnavailable =
        | WriterPoisoned of firstFailure: string
        | WriterClosing
        | WriterDisposed

    /// PERSIST-002: physical append is committed or uncertain. Semantic cuts and
    /// lifecycle refusal are both known-not-committed and stay explicit instead
    /// of being collapsed into physical uncertainty.
    type CommitResult<'e> =
        | Committed of 'e
        /// Bytes are durable, but the business rule cut this event and immediately
        /// wrote a durable reset fact. This invocation failed; the writer remains usable.
        | Rejected of EventId * reason: string
        | NotAttempted of EventId * JournalUnavailable
        | CommitUnknown of EventId * JournalFailure

/// Why a journal line was refused during a fold.
///
/// PERSIST-004 requires a corrupt journal to stop startup rather than be
/// absorbed. A benign duplicate is not corruption, so the two are separated
/// here: `FoldRejection` means the line is impossible, and the caller must fail
/// closed.
type FoldRejection = { Fact: string; Reason: string }

module FoldRejection =

    let reject factName reason =
        Error { Fact = factName; Reason = reason }

/// Physical fate of a single journal append.
///
/// WriteUnknown = durable-state indeterminate; WriterUnavailable = the writer
/// refused without attempting (known-not-committed); FactRejected = the line
/// bytes are durable but the semantic fold cut it — the append boundary treats
/// it as fatal evidence rather than retryable.
type JournalAppendFailure =
    | WriteUnknown of EventId * Outcome.JournalFailure
    | WriterUnavailable of EventId * Outcome.JournalUnavailable
    | FactRejected of EventId * FoldRejection

module JournalAppendFailure =

    /// Diagnostic rendering (HOST-007). The ONE place a failed append becomes a
    /// string.
    ///
    /// Nine call sites wrote `sprintf "%A" failure.Failure` — a field that does not
    /// exist on this union. Each was independently wrong in the same way, which is
    /// what a missing function looks like. `%A` is also reflection-based: under Fable
    /// it renders whatever the emitted shape happens to be, so the operator-facing
    /// text would drift with the compiler rather than with the domain.
    ///
    /// The two cases read differently on purpose. `WriteUnknown` is a physical
    /// uncertainty; `FactRejected` is a durable semantic cut and is fatal to the
    /// current process at the append boundary.
    let describe (failure: JournalAppendFailure) : string =
        match failure with
        | WriteUnknown(eventId, Outcome.WriteFailed reason) ->
            sprintf "append outcome unknown for %s: write failed: %s" (EventId.value eventId) reason
        | WriteUnknown(eventId, Outcome.FlushFailed reason) ->
            sprintf "append outcome unknown for %s: flush failed: %s" (EventId.value eventId) reason
        | WriterUnavailable(eventId, Outcome.WriterPoisoned firstFailure) ->
            sprintf
                "append not attempted for %s: writer poisoned by prior failure: %s"
                (EventId.value eventId)
                firstFailure
        | WriterUnavailable(eventId, Outcome.WriterClosing) ->
            sprintf "append not attempted for %s: writer is closing" (EventId.value eventId)
        | WriterUnavailable(eventId, Outcome.WriterDisposed) ->
            sprintf "append not attempted for %s: writer is disposed" (EventId.value eventId)
        | FactRejected(eventId, rejection) ->
            sprintf
                "journal semantic cut at %s: fact \'%s\' rejected: %s"
                (EventId.value eventId)
                rejection.Fact
                rejection.Reason
