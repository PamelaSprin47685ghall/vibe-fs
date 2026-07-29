namespace Wanxiangshu.Next.Kernel

open Wanxiangshu.Next.Kernel.Identity

/// Typed completion payload for a successful agent run.
/// Carries the completion payload for a successful agent run.
/// No transport parts are kept; business programs inspect the typed turn directly.
type AgentRunResult =
    { SessionId: SessionId
      RootUserMessageId: MessageId
      AssistantMessageId: MessageId
      Role: string // AgentRole serialized as string (avoid Kernel→Session dep)
      Directory: string
      /// Session-wide A: cumulative formal text + reasoning/thinking across the
      /// whole Session, not the last turn alone. Excludes tool raw streams.
      FinalText: string
      /// Current turn's formal assistant text only (no reasoning/thinking).
      /// B record for blogger companions is built from this field.
      FormalText: string }

    /// Hard invariant: completed runs must have non-empty session-wide A text.
    member this.IsValid = not (System.String.IsNullOrWhiteSpace this.FinalText)

type AgentRunFailure =
    { SessionId: SessionId; Reason: string }


module Outcome =

    type SendOutcome =
        | Delivered of MessageId
        | Retryable of reason: string
        | AcceptanceUnknown of reason: string * messageId: MessageId option
        | Fatal of reason: string

    type SessionOutcome =
        | CompletedSession of message: string
        | CancelledSession
        | TerminatedSession of reason: string

    type SessionError =
        | NoProgress of reason: string
        | SessionCancelled
        | FallbackExhausted
        | ReviewExhausted
        | PromptUncertain
        | ProjectionBroken of reason: string
        | InboxFull
        | Protocol of reason: string

    type JournalFailure =
        | WriteFailed of reason: string
        | FlushFailed of reason: string

    type CommitResult<'e> =
        | Committed of 'e
        | CommitUnknown of EventId * JournalFailure
