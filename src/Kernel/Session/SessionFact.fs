module Wanxiangshu.Kernel.Session.SessionFact

open Wanxiangshu.Kernel.FallbackKernel.Types

type TerminalObservedInfo =
    { SessionId: string
      HostEventId: string
      TerminalEpoch: int
      Generation: int
      SourceKind: string
      Props: obj }

/// Gate carried by effect-result facts. Actor drops the fact when generation,
/// ownership, or dispatch identity no longer match the live session epoch.
type EffectIdentity =
    { ExpectedGeneration: int
      ExpectedDispatchId: string option
      ExpectedOwner: SessionOwner option }

/// Standardized facts that enter the per-physical-session mailbox.
/// Hooks decode host noise into these; domain mutation happens only inside the actor.
[<RequireQualifiedAccess>]
type SessionFact =
    | ChatMessageObserved of messageId: string * role: string * props: obj
    | HostUserMessageBound of logicalTurnId: string * hostMessageId: string
    | SessionBusyObserved of props: obj
    | AssistantObserved of messageId: string * parentId: string option * props: obj
    | SessionIdleObserved of props: obj
    | SessionErrorObserved of props: obj
    | DispatchTransportReturned of identity: EffectIdentity * accepted: bool * detail: obj option
    | AbortReturned of identity: EffectIdentity * succeeded: bool
    | TimeoutElapsed of identity: EffectIdentity * kind: string
    | HumanTurnObserved of messageId: string * agent: string * model: string option
    | SessionClosed
    | RecoveryResult of identity: EffectIdentity * ok: bool * detail: string
    | TerminalObserved of info: TerminalObservedInfo
    /// Migration bridge: opaque host lifecycle envelope still needing host-side fan-out.
    | HostLifecycleEnvelope of eventType: string * props: obj * rawInput: obj

module SessionFact =
    let isEffectResult (fact: SessionFact) : bool =
        match fact with
        | SessionFact.DispatchTransportReturned _
        | SessionFact.AbortReturned _
        | SessionFact.TimeoutElapsed _
        | SessionFact.RecoveryResult _ -> true
        | _ -> false

    let tryEffectIdentity (fact: SessionFact) : EffectIdentity option =
        match fact with
        | SessionFact.DispatchTransportReturned(identity, _, _) -> Some identity
        | SessionFact.AbortReturned(identity, _) -> Some identity
        | SessionFact.TimeoutElapsed(identity, _) -> Some identity
        | SessionFact.RecoveryResult(identity, _, _) -> Some identity
        | _ -> None

    let name (fact: SessionFact) : string =
        match fact with
        | SessionFact.ChatMessageObserved _ -> "ChatMessageObserved"
        | SessionFact.HostUserMessageBound _ -> "HostUserMessageBound"
        | SessionFact.SessionBusyObserved _ -> "SessionBusyObserved"
        | SessionFact.AssistantObserved _ -> "AssistantObserved"
        | SessionFact.SessionIdleObserved _ -> "SessionIdleObserved"
        | SessionFact.SessionErrorObserved _ -> "SessionErrorObserved"
        | SessionFact.DispatchTransportReturned _ -> "DispatchTransportReturned"
        | SessionFact.AbortReturned _ -> "AbortReturned"
        | SessionFact.TimeoutElapsed _ -> "TimeoutElapsed"
        | SessionFact.HumanTurnObserved _ -> "HumanTurnObserved"
        | SessionFact.SessionClosed -> "SessionClosed"
        | SessionFact.RecoveryResult _ -> "RecoveryResult"
        | SessionFact.TerminalObserved _ -> "TerminalObserved"
        | SessionFact.HostLifecycleEnvelope(eventType, _, _) -> "HostLifecycleEnvelope:" + eventType
