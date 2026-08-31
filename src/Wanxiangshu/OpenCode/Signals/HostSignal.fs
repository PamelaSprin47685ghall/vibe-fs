namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Failure

type HostFailureObservation =
    { SessionId: SessionId
      Failure: ExecutionFailure
      Diagnostic: string }

/// FALLBACK-010: `Attempt` is the Host's own retry counter. Diagnostics only —
/// it may not reach ConsecutiveFailureCount, the budget test, Offset, or the
/// decision to send a continuation.
type RetrySignal =
    { SessionId: SessionId
      Attempt: string
      Failure: ExecutionFailure
      Diagnostic: string }

/// FALLBACK-003: Host signals never carry provider-run identity.
///
/// No message id on any case. A retry event's `messageID` was previously read as
/// the failed assistant message and written into the cursor, which is deriving a
/// domain fact from an event field. The failed provider run comes from the
/// reconciled snapshot instead (HOST-004). ProviderFailure contributes only a
/// typed physical failure witness; it becomes TurnFailed only when reconciliation
/// independently sees the exact current assistant for the physical material.
///
/// `AttemptAborted` is only a typed physical wake that revokes continuation
/// capability. The business `TurnAborted` outcome still comes from the full
/// assistant snapshot (HOST-002/004).
type HostSignal =
    | SessionIdle of SessionId
    | ProviderRetry of RetrySignal
    | ProviderFailure of HostFailureObservation
    | SessionDeleted of sessionId: SessionId * parentSessionId: SessionId option
    /// HOST-002/004: operator abort (MessageAbortedError / AbortError) is a
    /// typed signal that revokes the current attempt's idle-derived continuation
    /// capability. It is NOT ProviderFailure (it never advances fallback); it
    /// only means the attempt is no longer eligible to mint/consume a
    /// QuiescencePermit for a missing-final-report / interaction repair.
    | AttemptAborted of HostFailureObservation
