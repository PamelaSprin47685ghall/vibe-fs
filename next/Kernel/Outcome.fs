namespace Wanxiangshu.Next.Kernel

open Wanxiangshu.Next.Kernel.Identity

/// Typed completion payload for a successful agent run.
/// Carries the full result from ReconciledTurn directly.
type AgentRunResult =
    { SessionId: SessionId
      RootUserMessageId: MessageId
      AssistantMessageId: MessageId
      Role: string // AgentRole serialized as string (avoid Kernel→Session dep)
      Directory: string
      /// Session-wide formal assistant text (A): cumulative across the whole
      /// Session, not the last turn alone. Excludes reasoning / tool raw streams.
      FinalText: string
      Parts: obj array }

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
