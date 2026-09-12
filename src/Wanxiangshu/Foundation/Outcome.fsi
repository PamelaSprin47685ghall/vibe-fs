namespace Wanxiangshu.Foundation

open Wanxiangshu.Foundation.Identity

type AgentRunResult =
    { SessionId: SessionId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      ProviderRun: ProviderRunIdentity
      Role: Role
      Directory: string option
      TerminalText: string
      TurnFormalText: string }

    member IsValid: bool

type AgentRunFailure =
    { SessionId: SessionId; Reason: string }

module Outcome =
    type SendOutcome =
        | AdmittedWithReceipt of TransportReceipt
        | AdmittedWithPhysicalMessage of PhysicalUserMessageId
        | Retryable of reason: string
        | AcceptanceUnknown of reason: string
        | Fatal of reason: string

    type SessionOutcome =
        | CompletedSession of message: string
        | CancelledSession
        | TerminatedSession of reason: string

    type SessionError =
        | NoProgress of reason: string
        | SessionCancelled
        | AutoRecoveryExhausted
        | ReviewExhausted
        | PromptUncertain
        | ProjectionBroken of reason: string
        | InboxFull
        | Protocol of reason: string

    type JournalFailure =
        | WriteFailed of reason: string
        | FlushFailed of reason: string

    type JournalUnavailable =
        | WriterPoisoned of firstFailure: string
        | WriterClosing
        | WriterDisposed

    type CommitResult<'e> =
        | Committed of 'e
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
    val reject: factName: string -> reason: string -> Result<'a, FoldRejection>

/// Physical fate of a single journal append.
type JournalAppendFailure =
    | WriteUnknown of EventId * Outcome.JournalFailure
    | WriterUnavailable of EventId * Outcome.JournalUnavailable
    | FactRejected of EventId * FoldRejection

module JournalAppendFailure =
    val describe: failure: JournalAppendFailure -> string
