module Wanxiangshu.Runtime.Session.SessionFactDecode

open Wanxiangshu.Kernel.Session.SessionFact
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Runtime.OpencodeHookInputCodec

let private decodeTerminalObserved
    (eventType: string)
    (props: obj)
    (hostEventId: string)
    (source: string)
    : SessionFact =
    let genObj = Dyn.get props "generation"
    let gen = if Dyn.isNullish genObj then 0 else unbox<int> genObj

    SessionFact.TerminalObserved
        { SessionId = getSessionID eventType props
          HostEventId = hostEventId
          TerminalEpoch = 0
          Generation = gen
          SourceKind = source
          Props = props }

let private parseHostEventId (props: obj) : string =
    let eid = Dyn.str props "id"

    if eid = "" then
        let eventIdObj = Dyn.get props "eventId"

        if Dyn.isNullish eventIdObj then
            "evt-" + System.Guid.NewGuid().ToString("N")
        else
            box eventIdObj |> string
    else
        eid

/// Map a decoded host envelope into a standard SessionFact.
/// Unknown types fall back to HostLifecycleEnvelope so the mailbox still serializes them.
let fromHostEnvelope (envelope: HostEventEnvelope) (rawInput: obj) : SessionFact =
    let props = envelope.Props
    let eventType = envelope.EventType
    let hostEventId = parseHostEventId props

    match eventType with
    | "session.status" ->
        let statusObj = Dyn.get props "status"
        let statusVal = resolveStatusValue statusObj

        if statusVal = "busy" then
            SessionFact.SessionBusyObserved props
        elif statusVal = "idle" then
            decodeTerminalObserved eventType props hostEventId "session.status.idle"
        else
            SessionFact.HostLifecycleEnvelope(eventType, props, rawInput)
    | "session.idle" -> decodeTerminalObserved eventType props hostEventId "session.idle"
    | "session.error" -> decodeTerminalObserved eventType props hostEventId "session.error"
    | "session.deleted"
    | "session.delete"
    | "session.remove"
    | "session.close" -> SessionFact.SessionClosed
    | "message.updated" ->
        let info = Dyn.get props "info"
        let role = Dyn.str info "role"
        let messageId = Dyn.str info "id"

        if role = "assistant" then
            let parentId =
                let p = Dyn.str info "parentID"

                if p = "" then
                    let p2 = Dyn.str info "parentId"
                    if p2 = "" then None else Some p2
                else
                    Some p

            SessionFact.AssistantObserved(messageId, parentId, props)
        elif role = "user" then
            SessionFact.ChatMessageObserved(messageId, role, props)
        else
            SessionFact.HostLifecycleEnvelope(eventType, props, rawInput)
    | _ -> SessionFact.HostLifecycleEnvelope(eventType, props, rawInput)

let tryFromHostInput (input: obj) : (string * SessionFact) option =
    match decodeHostEventEnvelope input with
    | None -> None
    | Some envelope ->
        let sid = getSessionID envelope.EventType envelope.Props

        if sid = "" then
            None
        else
            Some(sid, fromHostEnvelope envelope input)
