module Wanxiangshu.Kernel.FallbackKernel.Types

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality

/// Model variant qualifier (e.g. "high", "medium", "low").
type ModelVariant = string

/// One candidate model in a fallback chain.
type FallbackModel =
    { ProviderID: string
      ModelID: string
      Variant: ModelVariant option
      Temperature: float option
      TopP: float option
      MaxTokens: int option
      ReasoningEffort: string option
      Thinking: bool }

/// Ordered list of candidate models tried in sequence.
type FallbackChain = FallbackModel list

[<RequireQualifiedAccess>]
/// Owner of the session's active lifecycle operation.
type SessionOwner =
    | NoOwner
    | Human
    | Fallback
    | Nudge
    | Compaction
    | Title

[<RequireQualifiedAccess>]
/// Lifecycle state of a pending continuation or nudge lease.
type LeaseStatus =
    | Requested
    | DispatchStarted
    /// Transport returned without a verifiable host receipt. Must not be
    /// rewritten to Failed and must not be blindly re-dispatched on restart.
    | AcceptanceUnknown
    | Dispatched
    | Running
    | Cancelled
    | Settled

[<RequireQualifiedAccess>]
/// Terminal result emitted when a continuation lease is finished.
type ContinuationOutcome =
    | Failed
    | Cancelled
    /// Transport returned without verifiable acceptance; reconciliation only.
    | AcceptanceUnknown
    /// Abort was requested but the host has no reliable abort API.
    | AbortUnknown
    | Settled

[<RequireQualifiedAccess>]
/// Terminal result emitted when a nudge lease is finished.
type NudgeOutcome =
    | Failed
    | Cancelled
    /// Abort was requested but the host has no reliable abort API.
    /// Must never be recorded as Cancelled.
    | AbortUnknown
    | Dispatched
    | Settled

/// Per-agent and global fallback policy.
type FallbackConfig =
    { DefaultChain: FallbackChain
      AgentChains: Map<string, FallbackChain>
      MaxRetries: int
      LoopMaxContinues: int
      MaxRecoveries: int }

/// Structured error extracted from a session.error or status event.
type ErrorInput =
    { ErrorName: string
      DomainError: DomainError option
      Message: string
      StatusCode: int option
      IsRetryable: bool option }

/// How the state machine classifies an error before acting.
[<RequireQualifiedAccess>]
type ErrorClass =
    | Ignore
    /// cancelled / task-complete / abort → consume silently
    | ImmediateFallback
    /// auth / permanent quota → skip retries, scan chain now
    | RetrySame
    /// transient / retryable → stay on same model, increment counter
    | Exhausted

/// retries exhausted → scan chain for next model
/// Phases a session traverses through a fallback episode.
[<RequireQualifiedAccess>]
type FallbackPhase =
    | Idle
    | Retrying of retryCount: int
    | Scanning of scanIndex: int * originalIndex: int
    | ScanningToolCallText
    | RecoveringToolCallText
    | Exhausted

[<RequireQualifiedAccess>]
type FallbackAction =
    | DoNothing
    | SendContinue of model: FallbackModel
    | RecoverWithPrompt of model: FallbackModel * promptText: string
    | ScanToolCallAsText
    | PropagateFailure

/// Lifecycle status of a fallback session (replaces Cancelled+TaskComplete booleans).
[<RequireQualifiedAccess>]
type FallbackLifecycle =
    | Active
    | Cancelled
    | TaskComplete
    | RecoveryRequired

/// Per-session state tracked by the fallback runtime.
type SessionFallbackState =
    { Phase: FallbackPhase
      CurrentIndex: int
      FailureCount: int
      Lifecycle: FallbackLifecycle
      ContinueCount: int
      RecoveryCount: int
      LastAssistantMessageId: string }

/// Result returned by the event-bridge hook to the host.
type FallbackHookResult =
    { Consumed: bool
      State: SessionFallbackState }

/// Host events fed into the fallback state machine.
type FallbackEvent =
    | SessionError of err: ErrorInput
    | SessionBusy
    | SessionIdle
    | NewUserMessage
    | TaskCompleteCalled
