namespace Wanxiangshu.Next.Kernel

open System

module Identity =

    type RuntimeId = private RuntimeId of string
    type SessionId = private SessionId of string
    type MessageId = private MessageId of string
    type TurnId = private TurnId of string
    type EventId = private EventId of string
    type DispatchId = private DispatchId of string
    type ChildId = private ChildId of string
    type SquadId = private SquadId of string
    type ProcessId = private ProcessId of string

    type LocalEpoch = int64
    type LocalSeq = private LocalSeq of int64
    type ObservedAt = DateTimeOffset

    type PromptKeyRef = private PromptKeyRef of string

    type MessageOrigin =
        | Human of TurnId
        | PluginGenerated of promptKey: PromptKeyRef
        | HostInternal

    module RuntimeId =
        let create (value: string) = RuntimeId value
        let value (RuntimeId v) = v

    module SessionId =
        let create (value: string) = SessionId value
        let value (SessionId v) = v

    module MessageId =
        let create (value: string) = MessageId value
        let value (MessageId v) = v

    module TurnId =
        let create (value: string) = TurnId value
        let value (TurnId v) = v
        let ofMessageId (MessageId v) = TurnId v
        let toMessageId (TurnId v) = MessageId v

    module EventId =
        let create (value: string) = EventId value
        let value (EventId v) = v

    module DispatchId =
        let create (value: string) = DispatchId value
        let value (DispatchId v) = v

    module ChildId =
        let create (value: string) = ChildId value
        let value (ChildId v) = v

    module SquadId =
        let create (value: string) = SquadId value
        let value (SquadId v) = v

    module ProcessId =
        let create (value: string) = ProcessId value
        let value (ProcessId v) = v

    module LocalSeq =
        let create (v: int64) = LocalSeq v
        let value (LocalSeq v) = v

    module PromptKeyRef =
        let create (value: string) = PromptKeyRef value
        let value (PromptKeyRef v) = v
