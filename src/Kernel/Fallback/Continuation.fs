module Wanxiangshu.Kernel.Fallback.Continuation

open Wanxiangshu.Kernel.FallbackKernel.Types

/// Why a continuation was requested.
[<RequireQualifiedAccess>]
type ContinuationMode =
    /// Continue the interrupted assistant turn without adding semantic input.
    | ResumeInterruptedTurn
    /// Recover from a tool-call-as-text mistake with an explicit prompt.
    | RecoverToolCallText of prompt: string

/// Lifecycle of a continuation attempt.
[<RequireQualifiedAccess>]
type ContinuationStatus =
    | Committed
    | DispatchClaimed
    | HostMessageAccepted
    | Running
    | Settled
    | Cancelled
    | Failed
    | Superseded

/// Proof of which host artifact owns this continuation.
[<RequireQualifiedAccess>]
type ContinuationHostIdentity =
    /// We have not yet observed the host user message/run that the dispatch created.
    | AwaitingUserMessage
    /// The continuation is bound to a concrete user message id in the host.
    | UserMessageIdentity of userMessageId: string
    /// The continuation is bound to a host run id.
    | RunIdentity of runId: string
    /// The host cannot give a concrete message/run id; we keep our own receipt.
    | OpaqueIdentity of receiptId: string

/// Immutable request for one continuation attempt.
type ContinuationRequest =
    { ContinuationId: string
      ContinuationOrdinal: int
      Attempt: int
      SessionId: string
      HumanTurnId: string
      SourceHumanMessageId: string option
      ContextGeneration: int
      CancelGeneration: int
      Model: FallbackModel
      Agent: string
      Mode: ContinuationMode }

/// Durable runtime state of a single continuation attempt.
type ContinuationState =
    { Request: ContinuationRequest
      Status: ContinuationStatus
      HostIdentity: ContinuationHostIdentity
      HostAssistantMessageId: string option
      Failure: string option }

/// Events persisted for the continuation v2 stream.
[<RequireQualifiedAccess>]
type ContinuationEvent =
    | Requested of ContinuationRequest
    | DispatchClaimed of continuationId: string * attempt: int * effectId: string
    | HostAccepted of continuationId: string * ContinuationHostIdentity
    | RunStarted of continuationId: string
    | AssistantMessageObserved of continuationId: string * assistantMessageId: string
    | Settled of continuationId: string * reason: string
    | Failed of continuationId: string * reason: string
    | Cancelled of continuationId: string * reason: string
    | Superseded of continuationId: string * reason: string

/// Commands that can be applied to a continuation projection.
[<RequireQualifiedAccess>]
type ContinuationCommand =
    | Request of ContinuationRequest
    | DispatchClaimed of continuationId: string * attempt: int * effectId: string
    | HostUserMessageObserved of continuationId: string * userMessageId: string
    | RunStarted of continuationId: string * runId: string
    | AssistantMessageObserved of continuationId: string * assistantMessageId: string
    | Settle of continuationId: string * reason: string
    | Fail of continuationId: string * reason: string
    | Cancel of continuationId: string * reason: string
    | Supersede of continuationId: string * reason: string
    | HumanTurnStarted of sessionId: string * humanTurnId: string * messageId: string
    | Reconcile
    | HostAbortConfirmed of continuationId: string * terminalEvent: ContinuationEvent

/// Host-side effects produced by continuation decisions.
[<RequireQualifiedAccess>]
type ContinuationEffect =
    | DispatchContinuation of request: ContinuationRequest * effectId: string
    | AbortContinuation of
        request: ContinuationRequest *
        identity: ContinuationHostIdentity *
        terminalEvent: ContinuationEvent
    | ReconcileContinuation of request: ContinuationRequest

/// Continuation projection for one session.
type SessionContinuationState =
    { Active: ContinuationState option
      ById: Map<string, ContinuationState> }

let emptySession: SessionContinuationState = { Active = None; ById = Map.empty }

/// Active continuation by session, plus all known continuations by id.
type ContinuationProjection =
    { ActiveBySession: Map<string, ContinuationState>
      ByContinuationId: Map<string, ContinuationState>
      ProcessedEffectIds: Set<string> }

let emptyProjection: ContinuationProjection =
    { ActiveBySession = Map.empty
      ByContinuationId = Map.empty
      ProcessedEffectIds = Set.empty }

/// The result of a continuation command decision.
type ContinuationDecision =
    { NextProjection: ContinuationProjection
      Events: ContinuationEvent list
      Effects: ContinuationEffect list }
