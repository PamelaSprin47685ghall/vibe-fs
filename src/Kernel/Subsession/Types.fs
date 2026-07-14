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

// ── Pure fallback policy (explicit selection ADT) ──

type ModelSelectionState =
    | StableAt of modelIndex: int
    | RetryingAt of modelIndex: int * retryCount: int
    | Scanning of candidateIndex: int * originalIndex: int

type FallbackPolicyState =
    { Selection: ModelSelectionState
      FailureCount: int
      ContinueCount: int
      RecoveryCount: int }

// ── Host start receipt ──

type HostStartReceipt =
    | UserMessageObserved of messageId: string
    | HostRunAccepted of runId: string
    | OrderedTurnMarkerObserved

/// Dispatch failure: only HostRejected may skip idle and retry.
type DispatchFailure =
    | HostRejected of ErrorInput
    | HostAcceptanceUnknown of ErrorInput

// ── Transcript ──

/// Anchor for slicing transcript to current turn only.
type TurnAnchor =
    | AnchorByUserMessageId of messageId: string
    | AnchorByHostRunId of runId: string
    | AnchorByTurnMarkerOnly

type TranscriptReadFailure = { Message: string }

/// Evidence about the current turn, accumulated from transcript slice.
/// Replaces the old boolean-based TranscriptSnapshot for turn-sliced evaluation.
type AssistantFinish =
    | ToolFinish
    | NormalFinish

type AssistantEvidence =
    | NoAssistant
    | EmptyAssistant
    | AssistantContent of text: string * finish: AssistantFinish option

type TodoEvidence =
    | TodosNotCompleted
    | TodosCompleted

type ToolEvidence =
    | NoToolResult
    | HasToolResult

type RecoveryEvidence =
    | NoRecoveryPrompt
    | RecoveryPrompt of recoveryPrompt: string

type CurrentTurnEvidence =
    { Assistant: AssistantEvidence
      Todos: TodoEvidence
      Tool: ToolEvidence
      Recovery: RecoveryEvidence }

module CurrentTurnEvidence =
    let empty: CurrentTurnEvidence =
        { Assistant = NoAssistant
          Todos = TodosNotCompleted
          Tool = NoToolResult
          Recovery = NoRecoveryPrompt }

    let merge (e1: CurrentTurnEvidence) (e2: CurrentTurnEvidence) : CurrentTurnEvidence =
        let mergedAssistant =
            match e1.Assistant, e2.Assistant with
            | NoAssistant, x -> x
            | x, NoAssistant -> x
            | EmptyAssistant, x -> x
            | x, EmptyAssistant -> x
            | AssistantContent(t1, f1), AssistantContent(t2, f2) ->
                let mergedFinish =
                    match f1, f2 with
                    | Some f, _ -> Some f
                    | _, Some f -> Some f
                    | None, None -> None

                AssistantContent(t1 + t2, mergedFinish)

        let mergedTodos =
            match e1.Todos, e2.Todos with
            | TodosCompleted, _ -> TodosCompleted
            | _, TodosCompleted -> TodosCompleted
            | TodosNotCompleted, TodosNotCompleted -> TodosNotCompleted

        let mergedTool =
            match e1.Tool, e2.Tool with
            | HasToolResult, _ -> HasToolResult
            | _, HasToolResult -> HasToolResult
            | NoToolResult, NoToolResult -> NoToolResult

        let mergedRecovery =
            match e1.Recovery, e2.Recovery with
            | RecoveryPrompt r1, RecoveryPrompt r2 ->
                if r1 = r2 then RecoveryPrompt r1
                elif r1 = "" then RecoveryPrompt r2
                elif r2 = "" then RecoveryPrompt r1
                else RecoveryPrompt(r1 + "\n" + r2)
            | RecoveryPrompt r, NoRecoveryPrompt -> RecoveryPrompt r
            | NoRecoveryPrompt, RecoveryPrompt r -> RecoveryPrompt r
            | NoRecoveryPrompt, NoRecoveryPrompt -> NoRecoveryPrompt

        { Assistant = mergedAssistant
          Todos = mergedTodos
          Tool = mergedTool
          Recovery = mergedRecovery }

type TurnObservation =
    { TurnId: TurnId
      Evidence: CurrentTurnEvidence }

type DispatchStatus =
    | Accepted of HostStartReceipt
    | DefinitelyNotAccepted
    | StillPending
    | Unknown

// ── Abort ──

/// Host abort effect result. Missing abort API is NEVER ConfirmedStopped.
type AbortResult =
    | ConfirmedStopped
    | RequestAcceptedAwaitIdle
    | AbortUnavailable

/// What to do after the host is confirmed stopped.
type AfterAbort =
    | FinishCancelled
    | FinishFailed of RunFailure
    | RetryAfterSafeStop of ErrorInput

type AbortReason =
    | UserRequested
    | TurnDeadline
    | AcceptanceUnknownAfterDispatch
    | TranscriptInspectDeadline
    | EventStoreFailure of reason: string
    | IllegalTransitionFailSafe of reason: string

type AbortContext =
    { Reason: AbortReason
      AfterStop: AfterAbort }

/// CancelContext is structurally AbortContext — reuse AbortContext directly.
/// CancellingDispatch carries an AbortContext that preserves the original cancel
/// trigger (UserRequested vs TurnDeadline) so it propagates correctly into abort.
type CancelContext = AbortContext

// ── Turn data ──

type TurnPlan =
    { TurnId: TurnId
      Ordinal: TurnOrdinal
      Model: FallbackModel
      Prompt: string }

type StartedTurn =
    { Plan: TurnPlan
      StartReceipt: HostStartReceipt }

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

type PoisonReason =
    | AbortDidNotSettle of TurnId
    | HostProtocolBroken of string
    | SessionStateUnknownAfterRestart
    | SessionClosedUnexpectedly
    | EventStoreCorrupt of string

/// Abort protocol:
///   beginAbort → IssuingAbort (host abort not yet accepted)
///   AbortHostAccepted → AwaitingAbortSettle (idle may prove stop)
///   ConfirmedStopped → apply AfterAbort immediately
///   AbortUnavailable → remain IssuingAbort until deadline / SessionClosed
///
/// CancellingDispatch: cancel requested while dispatch in-flight — must wait
/// for DispatchAccepted/Rejected before deciding abort vs safe-cancel.
///
/// ReconcilingUnknownDispatch: AcceptanceUnknown after cancel — dispatch may or
/// may not have been accepted by Host. Query Host to determine, or poison if
/// cannot resolve.
///
/// ReconcilingAbortSettle: Host idle observed while in IssuingAbort. If AbortHostAccepted
/// arrives, settle immediately.
///
/// Draining: host reported a turn error (TurnErrorObserved) but has not yet
/// gone idle. The error is held here — NOT acted on — until SessionIdleObserved
/// proves the host has actually stopped for this turn, then resolved via the
/// fallback policy (afterError). Same idle-barrier discipline as
/// CurrentTurnEvidence classification in Running.
type SubsessionState =
    | Available of AvailableState
    /// Third field buffers CurrentTurnEvidence that arrives BEFORE the host
    /// confirms acceptance of the dispatched prompt (DispatchAccepted). Host
    /// truth: session.prompt resolving and the host's event bus delivering
    /// message.updated(role=assistant) are two independent async chains —
    /// nothing orders one before the other. A fast provider can deliver the
    /// full assistant reply while we are still Dispatching. This evidence
    /// MUST survive into Running (see Decision.fs Dispatching+DispatchAccepted),
    /// not be silently destroyed — that was the root cause of subagent runs
    /// (investigator/coder/browser/meditator) spuriously failing with
    /// "No assistant message in current turn".
    | Dispatching of RunContext * TurnPlan * CurrentTurnEvidence
    | CancellingDispatch of RunContext * TurnPlan * CancelContext
    | ReconcilingUnknownDispatch of RunContext * TurnPlan * CancelContext
    | Running of RunContext * StartedTurn * CurrentTurnEvidence
    | Draining of RunContext * StartedTurn * ErrorInput
    | IssuingAbort of RunContext * ActiveTurn * AbortContext
    | AwaitingAbortSettle of RunContext * ActiveTurn * AbortContext
    | ReconcilingAbortSettle of RunContext * ActiveTurn * AbortContext
    | Poisoned of PoisonReason

// ── Start-run ──

type StartRunError =
    | AlreadyRunning
    | SessionPoisoned of PoisonReason
    | NoModelAvailable

type StartRunRequest =
    {
        RunId: RunId
        SessionId: SessionId
        ParentSessionId: SessionId
        Prompt: string
        FallbackConfig: FallbackConfig
        Chain: FallbackChain
        /// True when AbortSignal was already aborted at StartRun commit time.
        InitiallyCancelled: bool
    }

// ── Command ADT ──

type Command =
    | StartRun of StartRunRequest
    | DispatchAccepted of TurnId * HostStartReceipt
    | DispatchRejected of TurnId * DispatchFailure
    | TurnErrorObserved of ErrorInput
    | SessionIdleObserved
    /// Host confirmed dispatch status for reconciliation (AcceptanceUnknown path).
    | DispatchStatusResolved of DispatchStatus
    | EvidenceUpdated of TurnObservation
    | CancelRequested
    | TurnDeadlineExpired of TurnId
    | AbortDeadlineExpired of TurnId
    /// Host confirmed session stopped (safe to apply AfterAbort).
    | AbortConfirmed of TurnId
    /// Host accepted abort request; subsequent idle may settle.
    | AbortHostAccepted of TurnId
    /// Host abort call failed or API unavailable.
    | AbortRequestFailed of TurnId * ErrorInput
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
    | TurnInfrastructureFailed of reason: string

type SubsessionEvent =
    | RunStarted of RunStartedData
    | TurnDispatchRequested of TurnData
    | TurnStarted of TurnStartedData
    | TurnFinished of TurnId * TurnFinishOutcome
    | AbortRequested of RunId * TurnId
    | RunFinished of RunId * RunResult
    | SessionPoisoned of SessionId * PoisonReason
    | PhysicalSessionClosed of SessionId

// ── Effect ADT ──

type Effect =
    | DispatchPrompt of TurnPlan
    | QueryDispatchStatus of SessionId * TurnId
    | AbortHostSession of SessionId * TurnId
    | CancelPendingDispatch of TurnId
    | ArmTurnDeadline of TurnId
    | CancelTurnDeadline of TurnId
    | ArmAbortDeadline of TurnId
    | CancelAbortDeadline of TurnId
    | CompleteCaller of RunId * RunResult
    | RejectStart of StartRunError
    | DisposeActor

// ── Decision types ──

type IgnoreReason =
    | DuplicateIdleBeforeTurnMarker
    | DuplicateError
    | StaleTimer
    | StaleTurnMarker
    | UnattributedObservationBeforeStart
    | AbortInProgress
    | EvidenceBeforeRun
    | IdleBeforeAbortBarrier

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
