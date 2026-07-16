namespace Wanxiangshu.Kernel.Subsession.Types

open Wanxiangshu.Kernel.FallbackKernel.Types

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
