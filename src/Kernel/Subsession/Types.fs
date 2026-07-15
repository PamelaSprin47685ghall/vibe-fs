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
        RunId("run-" + System.Guid.NewGuid().ToString("N"))

module TurnId =
    let create (s: string) : TurnId =
        if s = "" then
            failwith "TurnId cannot be empty"

        TurnId s

    let value (TurnId s) : string = s

module SessionId =
    let create (s: string) : SessionId =
        if s = "" then
            failwith "SessionId cannot be empty"

        SessionId s

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
type RecordedOutcome =
    | NoOutcome
    | FailureObserved of ErrorInput
    | CompletionRequested of output: string

/// Evidence about the current turn, accumulated from transcript slice.
/// Replaces the old boolean-based TranscriptSnapshot for turn-sliced evaluation.
type AssistantFinish =
    | ToolFinish
    | NormalFinish

type AssistantEvidence =
    | NoAssistant
    | EmptyAssistant
    | AssistantSnapshot of messageId: string * revision: int64 * text: string * finish: AssistantFinish option
    | AssistantDelta of messageId: string * revision: int64 * text: string * finish: AssistantFinish option

type TodoEvidence =
    | NoTodoInfo
    | TodosNotCompleted
    | TodosCompleted

type ToolEvidence =
    | NoToolResult
    | HasToolResult

type RecoveryEvidence =
    | NoRecoveryPrompt
    | RecoveryPrompt of recoveryPrompt: string
    | RawToolCallDetected of recoveryPrompt: string

type CurrentTurnEvidence =
    { Assistant: AssistantEvidence
      Todos: TodoEvidence
      Tool: ToolEvidence
      Recovery: RecoveryEvidence
      Outcome: RecordedOutcome }

module CurrentTurnEvidence =
    let empty: CurrentTurnEvidence =
        { Assistant = NoAssistant
          Todos = NoTodoInfo
          Tool = NoToolResult
          Recovery = NoRecoveryPrompt
          Outcome = NoOutcome }

    let private mergeAssistant e1 e2 =
        match e1, e2 with
        | NoAssistant, x -> x
        | x, NoAssistant -> x
        | EmptyAssistant, x -> x
        | x, EmptyAssistant -> x
        | AssistantSnapshot(id1, rev1, t1, f1), AssistantSnapshot(id2, rev2, t2, f2) ->
            if id1 <> "" && id2 <> "" && id1 = id2 then
                if rev2 >= rev1 then
                    AssistantSnapshot(id2, rev2, t2, f2)
                else
                    AssistantSnapshot(id1, rev1, t1, f1)
            elif id1 = "" && id2 = "" then
                AssistantSnapshot("", 0L, t2, f2)
            elif rev2 > rev1 then
                AssistantSnapshot(id2, rev2, t2, f2)
            else
                AssistantSnapshot(id1, rev1, t1, f1)
        | AssistantDelta(id1, rev1, t1, f1), AssistantDelta(id2, rev2, t2, f2) ->
            if id1 <> "" && id2 <> "" && id1 = id2 then
                AssistantDelta(id1, max rev1 rev2, t1 + t2, (if rev2 > rev1 then f2 else f1))
            elif id1 = "" && id2 = "" then
                AssistantDelta("", max rev1 rev2, t1 + t2, (if rev2 >= rev1 then f2 else f1))
            elif rev2 > rev1 then
                AssistantDelta(id2, rev2, t2, f2)
            else
                AssistantDelta(id1, rev1, t1, f1)
        | AssistantSnapshot(id1, rev1, t1, f1), AssistantDelta(id2, rev2, t2, f2)
        | AssistantDelta(id2, rev2, t2, f2), AssistantSnapshot(id1, rev1, t1, f1) ->
            if id1 <> "" && id2 <> "" && id1 = id2 then
                if rev2 > rev1 then
                    AssistantSnapshot(id1, rev2, t1 + t2, f2)
                else
                    AssistantSnapshot(id1, rev1, t1, f1)
            elif id1 = "" && id2 = "" then
                AssistantSnapshot("", 0L, t2, f2)
            elif rev2 > rev1 then
                AssistantSnapshot(id2, rev2, t2, f2)
            else
                AssistantSnapshot(id1, rev1, t1, f1)


    let private mergeTodos e1 e2 =
        match e2 with
        | NoTodoInfo -> e1
        | _ -> e2

    let private mergeTool e1 e2 =
        match e1, e2 with
        | HasToolResult, _ -> HasToolResult
        | _, HasToolResult -> HasToolResult
        | NoToolResult, NoToolResult -> NoToolResult

    let private mergeRecovery e1 e2 =
        match e1, e2 with
        | RawToolCallDetected r1, RawToolCallDetected r2 ->
            if r1 = r2 then RawToolCallDetected r1
            elif r1 = "" then RawToolCallDetected r2
            elif r2 = "" then RawToolCallDetected r1
            else RawToolCallDetected(r1 + "\n" + r2)
        | RawToolCallDetected r, _ -> RawToolCallDetected r
        | _, RawToolCallDetected r -> RawToolCallDetected r
        | RecoveryPrompt r1, RecoveryPrompt r2 ->
            if r1 = r2 then RecoveryPrompt r1
            elif r1 = "" then RecoveryPrompt r2
            elif r2 = "" then RecoveryPrompt r1
            else RecoveryPrompt(r1 + "\n" + r2)
        | RecoveryPrompt r, NoRecoveryPrompt -> RecoveryPrompt r
        | NoRecoveryPrompt, RecoveryPrompt r -> RecoveryPrompt r
        | NoRecoveryPrompt, NoRecoveryPrompt -> NoRecoveryPrompt

    let private mergeOutcome e1 e2 =
        match e1, e2 with
        | CompletionRequested _, _ -> e1
        | _, CompletionRequested _ -> e2
        | FailureObserved _, _ -> e1
        | _, FailureObserved _ -> e2
        | NoOutcome, NoOutcome -> NoOutcome

    let merge (e1: CurrentTurnEvidence) (e2: CurrentTurnEvidence) : CurrentTurnEvidence =
        { Assistant = mergeAssistant e1.Assistant e2.Assistant
          Todos = mergeTodos e1.Todos e2.Todos
          Tool = mergeTool e1.Tool e2.Tool
          Recovery = mergeRecovery e1.Recovery e2.Recovery
          Outcome = mergeOutcome e1.Outcome e2.Outcome }

module AssistantEvidence =
    let content text finish = AssistantSnapshot("", 0L, text, finish)

    let snapshot messageId revision text finish =
        AssistantSnapshot(messageId, revision, text, finish)

    let delta messageId revision text finish =
        AssistantDelta(messageId, revision, text, finish)

    let isSnapshot =
        function
        | AssistantSnapshot _ -> true
        | _ -> false

    let isDelta =
        function
        | AssistantDelta _ -> true
        | _ -> false

    let isContent =
        function
        | AssistantSnapshot _
        | AssistantDelta _ -> true
        | _ -> false

type TurnObservation =
    { TurnId: TurnId option
      Evidence: CurrentTurnEvidence }

type DispatchStatus =
    | Accepted of HostStartReceipt
    | TransportRejectedBeforeSend of ErrorInput
    | TransportFailedAfterUnknownAcceptance of ErrorInput
    | StillPending
    | Unknown

/// Proof that a physical session has stopped after abort. Stopped means the
/// host reports no active turn for this session; StillRunning means it does;
/// StopUnknown means the host cannot tell.
type QuiescenceStatus =
    | Stopped
    | StillRunning
    | StopUnknown

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
      Model: FallbackModel option
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
/// fallback policy (afterError) unless the turn already produced a
/// CompletionRequested outcome, which takes priority. The buffered
/// CurrentTurnEvidence is preserved so task_complete / error ordering cannot
/// lose a successful completion.
type SubsessionState =
    | Available of AvailableState
    /// Third field buffers CurrentTurnEvidence that arrives BEFORE the host
    /// confirms acceptance of the dispatched prompt (DispatchAccepted). Host
    /// truth: session.prompt resolving and the host's event bus delivering
    /// message.updated(role=assistant) are two INDEPENDENT async chains —
    /// nothing orders one before the other. A fast provider can deliver the
    /// full assistant reply while we are still Dispatching. This evidence
    /// MUST survive into Running (see Decision.fs Dispatching+DispatchAccepted),
    /// not be silently destroyed — that was the root cause of subagent runs
    /// (investigator/coder/browser/meditator) spuriously failing with
    /// "No assistant message in current turn".
    | Dispatching of RunContext * TurnPlan * CurrentTurnEvidence
    | CancellingDispatch of RunContext * TurnPlan * CancelContext
    | ReconcilingUnknownDispatch of RunContext * TurnPlan * CancelContext * retryCount: int
    | ClosingUnknownDispatch of RunContext * TurnPlan * PoisonReason
    | Running of RunContext * StartedTurn * CurrentTurnEvidence
    | Draining of RunContext * StartedTurn * ErrorInput * CurrentTurnEvidence
    | IssuingAbort of RunContext * ActiveTurn * AbortContext * idleObserved: bool
    | AwaitingAbortSettle of RunContext * ActiveTurn * AbortContext
    | ReconcilingAbortSettle of RunContext * ActiveTurn * AbortContext
    | Poisoned of PoisonReason

// ── Start-run ──

type StartRunError =
    | AlreadyRunning
    | SessionPoisoned of PoisonReason
    | NoModelAvailable

// ── Model directive: retry-capable chain vs delegate-to-host ──

/// Whether this run should be driven by wanxiangshu's own fallback retry
/// policy (RetryChain, chain non-empty is the caller's invariant to uphold)
/// or should delegate model selection entirely to the host (DelegateToHost:
/// no model field is ever passed to the host; the host's own static agent
/// config / currentModel resolution takes effect, matching OpenCode's
/// session.prompt priority: input.model ?? ag.model ?? currentModel).
type ModelDirective =
    | RetryChain of FallbackChain
    | DelegateToHost

type StartRunRequest =
    {
        RunId: RunId
        SessionId: SessionId
        ParentSessionId: SessionId
        Prompt: string
        FallbackConfig: FallbackConfig
        Directive: ModelDirective
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
    /// Deadline for dispatch reconciliation has expired; query again or poison.
    | ReconciliationDeadlineExpired of TurnId
    /// Host confirmed session stopped (safe to apply AfterAbort).
    | AbortConfirmed of TurnId
    /// Host accepted abort request; subsequent idle may settle.
    | AbortHostAccepted of TurnId
    /// Host abort call failed or API unavailable.
    | AbortRequestFailed of TurnId * ErrorInput
    /// Host confirmed whether the session is still running after abort.
    | SessionQuiescenceResolved of QuiescenceStatus
    | PhysicalCloseResolved of QuiescenceStatus
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
    | QuerySessionQuiescence of SessionId * TurnId
    | ClosePhysicalSession of SessionId
    | AbortHostSession of SessionId * TurnId
    | CancelPendingDispatch of TurnId
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
    | UnattributableObservation

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
