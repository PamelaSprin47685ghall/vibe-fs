module Wanxiangshu.Kernel.Fallback.Continuation

open Wanxiangshu.Kernel.FallbackKernel.Types

[<RequireQualifiedAccess>]
type ContinuationMode =
    | ResumeInterruptedTurn
    | RecoverToolCallText of prompt: string

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

[<RequireQualifiedAccess>]
type ContinuationHostIdentity =
    | AwaitingUserMessage
    | UserMessageIdentity of userMessageId: string
    | RunIdentity of runId: string
    | OpaqueIdentity of receiptId: string

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

type ContinuationState =
    { Request: ContinuationRequest
      Status: ContinuationStatus
      HostIdentity: ContinuationHostIdentity
      HostAssistantMessageId: string option
      Failure: string option }

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

[<RequireQualifiedAccess>]
type ContinuationEffect =
    | DispatchContinuation of request: ContinuationRequest * effectId: string
    | AbortContinuation of
        request: ContinuationRequest *
        identity: ContinuationHostIdentity *
        terminalEvent: ContinuationEvent
    | ReconcileContinuation of request: ContinuationRequest

type SessionContinuationState =
    { Active: ContinuationState option
      ById: Map<string, ContinuationState> }

let emptySession: SessionContinuationState = { Active = None; ById = Map.empty }

type ContinuationProjection =
    { ActiveBySession: Map<string, ContinuationState>
      ByContinuationId: Map<string, ContinuationState>
      ProcessedEffectIds: Set<string> }

let emptyProjection: ContinuationProjection =
    { ActiveBySession = Map.empty
      ByContinuationId = Map.empty
      ProcessedEffectIds = Set.empty }

type ContinuationDecision =
    { NextProjection: ContinuationProjection
      Events: ContinuationEvent list
      Effects: ContinuationEffect list }
