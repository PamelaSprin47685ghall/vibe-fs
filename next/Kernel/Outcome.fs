namespace Wanxiangshu.Next.Kernel

open Wanxiangshu.Next.Kernel.Identity

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
