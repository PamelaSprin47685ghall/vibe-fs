module Wanxiangshu.Kernel.FallbackKernel.Types

open Wanxiangshu.Kernel.Domain

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

/// Per-session state tracked by the fallback runtime.
type SessionFallbackState =
    { Phase: FallbackPhase
      CurrentIndex: int
      FailureCount: int
      Cancelled: bool
      TaskComplete: bool
      ContinueCount: int
      RecoveryCount: int }

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
