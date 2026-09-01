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
    { SessionId: SessionId
      Reason: string }

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
