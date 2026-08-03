namespace Wanxiangshu.OpenCode

open Wanxiangshu.Kernel.Identity

/// FALLBACK-010: `Attempt` is the Host's own retry counter. Diagnostics only —
/// it may not reach ConsecutiveFailureCount, the budget test, Offset, or the
/// decision to send a continuation.
type RetrySignal =
    { SessionId: SessionId
      Attempt: string
      Reason: string }

/// FALLBACK-003: Host signals wake, they do not carry facts.
///
/// No message id on any case. A retry event's `messageID` was previously read as
/// the failed assistant message and written into the cursor, which is deriving a
/// domain fact from an event field. The failed provider run comes from the
/// reconciled snapshot instead (HOST-004).
///
/// Abort is likewise not an event type: it is classified from the full assistant
/// snapshot after a SessionIdle reconcile.
type HostSignal =
    | SessionIdle of SessionId
    | ProviderRetry of RetrySignal
    | ProviderFailure of sessionId: SessionId * reason: string
    | SessionDeleted of SessionId

type SessionSignalSource =
    | LocalPluginEvent
    | GlobalForeignDirectoryEvent
