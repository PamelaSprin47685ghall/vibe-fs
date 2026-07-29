namespace Wanxiangshu.Next.OpenCode

open Wanxiangshu.Next.Kernel.Identity

type RetrySignal =
    { SessionId: SessionId
      Attempt: string
      Reason: string
      MessageId: MessageId option }

/// SSOT-only host signals. Abort is never a separate event type — it is
/// classified from the full assistant snapshot after SessionIdle reconcile.
type ProviderFailureWakeup =
    { SessionId: SessionId
      Reason: string
      MessageId: MessageId option }

type HostSignal =
    | SessionIdle of SessionId
    | ProviderRetry of RetrySignal
    | ProviderFailure of ProviderFailureWakeup
    | SessionDeleted of SessionId

type SessionSignalSource =
    | LocalPluginEvent
    | GlobalForeignDirectoryEvent
