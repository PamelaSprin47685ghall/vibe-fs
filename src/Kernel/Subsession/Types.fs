module Wanxiangshu.Kernel.Subsession.Types

open Wanxiangshu.Kernel.FallbackKernel.Types

// ── Strong-typed identifiers ──

type RunId = private RunId of string
type TurnId = private TurnId of string
type SessionId = private SessionId of string
type TurnOrdinal = private TurnOrdinal of int

module RunId =
    let create (s: string) : RunId = RunId s
    let value (RunId s) : string = s

    let newId () : RunId =
        RunId("run-" + System.Guid.NewGuid().ToString("N").Substring(0, 8))

module TurnId =
    let create (s: string) : TurnId = TurnId s
    let value (TurnId s) : string = s

module SessionId =
    let create (s: string) : SessionId = SessionId s
    let value (SessionId s) : string = s

module TurnOrdinal =
    let first: TurnOrdinal = TurnOrdinal 0
    let value (TurnOrdinal n) : int = n
    let next (TurnOrdinal n) : TurnOrdinal = TurnOrdinal(n + 1)

// ── Run final result ──

type RunFailure =
    | NoModelConfigured
    | FallbackExhausted of lastError: ErrorInput
    | RecoveryExhausted of reason: string
    | ProtocolViolation of reason: string
    | InfrastructureFailure of reason: string

type RunResult =
    | Succeeded of output: string
    | Failed of RunFailure
    | Cancelled

// ── Outcome observed before idle ──

type RecordedOutcome =
    | FailureObserved of ErrorInput
    | CompletionRequested of output: string

// ── Pure fallback policy state (decoupled from lifecycle) ──

type FallbackPolicyState =
    { ModelIndex: int
      RetryCount: int
      FailureCount: int
      ContinueCount: int
      RecoveryCount: int }

// ── Host start receipt ──

type HostStartReceipt =
    | UserMessageObserved of messageId: string
    | HostRunAccepted of runId: string
    | OrderedTurnMarkerObserved

// ── Turn data ──

type TurnPlan =
    { TurnId: TurnId
      Ordinal: TurnOrdinal
      Model: FallbackModel
      Prompt: string }

type StartedTurn =
    { Plan: TurnPlan
      StartReceipt: HostStartReceipt }

// ── Transcript snapshot (pre-extracted facts) ──

type TranscriptSnapshot =
    { AllTodosCompleted: bool
      ToolCallAsTextRecoveryPrompt: string option
      LastAssistantToolFinish: bool
      HasToolResultAfterLastAssistant: bool
      LastAssistantText: string }

// ── State ADT ──

type AvailableState = { SessionId: SessionId }

type RunContext =
    { RunId: RunId
      ParentSessionId: SessionId
      SessionId: SessionId
      Policy: FallbackPolicyState
      FallbackConfig: FallbackConfig
      Chain: FallbackChain
      NextTurnOrdinal: TurnOrdinal }

type ActiveTurn =
    | NotYetStarted of TurnPlan
    | Started of StartedTurn

type AbortReason =
    | UserRequested
    | TurnDeadline

type AbortContext =
    { Reason: AbortReason
      FinalResult: RunResult }

type PoisonReason =
    | AbortDidNotSettle of TurnId
    | HostProtocolBroken of string
    | SessionStateUnknownAfterRestart

type SubsessionState =
    | Available of AvailableState
    | Dispatching of RunContext * TurnPlan
    | Running of RunContext * StartedTurn
    | Draining of RunContext * StartedTurn * RecordedOutcome
    | InspectingTranscript of RunContext * StartedTurn
    | Aborting of RunContext * ActiveTurn * AbortContext
    | Poisoned of PoisonReason

// ── Start-run errors ──

type StartRunError =
    | AlreadyRunning
    | SessionPoisoned of PoisonReason
    | NoModelAvailable

type StartRunRequest =
    { RunId: RunId
      SessionId: SessionId
      ParentSessionId: SessionId
      Prompt: string
      FallbackConfig: FallbackConfig
      Chain: FallbackChain }

// ── Command ADT ──

type Command =
    | StartRun of StartRunRequest
    | DispatchAccepted of TurnId * HostStartReceipt
    | DispatchRejected of TurnId * ErrorInput
    | TurnErrorObserved of ErrorInput
    | TaskCompleteObserved of output: string
    | SessionIdleObserved
    | TranscriptLoaded of TranscriptSnapshot
    | CancelRequested
    | TurnDeadlineExpired of TurnId
    | AbortAcknowledged of TurnId
    | AbortDeadlineExpired of TurnId
    | SessionClosed

// ── Domain events ──

type RunStartedData =
    { RunId: RunId
      ParentSessionId: SessionId
      SessionId: SessionId }

type TurnData =
    { RunId: RunId
      TurnId: TurnId
      Ordinal: TurnOrdinal
      Model: FallbackModel
      Prompt: string }

type TurnStartedData =
    { RunId: RunId
      TurnId: TurnId
      Receipt: HostStartReceipt }

type TurnFinishOutcome =
    | TurnCompleted of output: string
    | TurnFailed of ErrorInput
    | TurnCancelled
    | TurnRecovering

type SubsessionEvent =
    | RunStarted of RunStartedData
    | TurnDispatchRequested of TurnData
    | TurnStarted of TurnStartedData
    | TurnOutcomeObserved of TurnId * RecordedOutcome
    | TurnFinished of TurnId * TurnFinishOutcome
    | AbortRequested of TurnId
    | RunFinished of RunId * RunResult
    | SessionPoisoned of SessionId * PoisonReason

// ── Effect ADT ──

type Effect =
    | AppendDomainEvents of SubsessionEvent list
    | DispatchPrompt of TurnPlan
    | ReadTranscript of SessionId
    | AbortHostSession of SessionId * TurnId
    | ArmTurnDeadline of TurnId
    | CancelTurnDeadline of TurnId
    | ArmAbortDeadline of TurnId
    | CancelAbortDeadline of TurnId
    | CompleteCaller of RunId * RunResult
    | RejectStart of StartRunError

// ── Decision types ──

type IgnoreReason =
    | DuplicateIdleBeforeTurnMarker
    | DuplicateTaskComplete
    | DuplicateError
    | CompletionAlreadyWins
    | StaleTimer
    | StaleTurnMarker

type Decision =
    { NextState: SubsessionState
      Events: SubsessionEvent list
      Effects: Effect list }

type DecisionResult =
    | Decided of Decision
    | NoChange of IgnoreReason

type DecisionError =
    | IllegalTransition of state: string * command: string
    | StaleTurnCommand of expected: TurnId * actual: TurnId
