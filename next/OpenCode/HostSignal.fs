namespace Wanxiangshu.Next.OpenCode

open Wanxiangshu.Next.Kernel.Identity

type RetrySignal =
    { SessionId: SessionId
      Attempt: string
      Reason: string
      MessageId: MessageId option }

/// Host failed a provider call without an assistant message (e.g. non-retryable
/// 4xx). Idle reconcile cannot see a TurnFailed; plugin must drive AABB itself.
type ProviderErrorSignal =
    { SessionId: SessionId
      Reason: string
      StatusCode: int option
      IsRetryable: bool option
      MessageId: MessageId option }

/// SSOT-only host signals. Abort is never a separate event type — it is
/// classified from the full assistant snapshot after SessionIdle reconcile.
type HostSignal =
    | SessionIdle of SessionId
    | ProviderRetry of RetrySignal
    | ProviderError of ProviderErrorSignal
    | SessionDeleted of SessionId

type SessionSignalSource =
    | LocalPluginEvent
    | GlobalForeignDirectoryEvent
