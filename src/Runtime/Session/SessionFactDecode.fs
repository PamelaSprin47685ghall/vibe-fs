module Wanxiangshu.Runtime.Session.SessionFactDecode

open Wanxiangshu.Kernel.Session.SessionFact
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Runtime.OpencodeHookInputCodec

/// Map a decoded host envelope into a standard SessionFact.
/// Unknown types fall back to HostLifecycleEnvelope so the mailbox still serializes them.
let fromHostEnvelope (envelope: HostEventEnvelope) (rawInput: obj) : SessionFact =
    let props = envelope.Props
    let eventType = envelope.EventType

    match eventType with
    | "session.status" ->
        let statusObj = get props "status"
        let statusVal = resolveStatusValue statusObj

        if statusVal = "busy" then
            SessionFact.SessionBusyObserved props
        elif statusVal = "idle" then
            SessionFact.SessionIdleObserved props
        else
            SessionFact.HostLifecycleEnvelope(eventType, props, rawInput)
    | "session.idle" -> SessionFact.SessionIdleObserved props
    | "session.error" -> SessionFact.SessionErrorObserved props
    | "session.deleted"
    | "session.delete"
    | "session.remove"
    | "session.close" -> SessionFact.SessionClosed
    | "message.updated" ->
        let info = get props "info"
        let role = str info "role"
        let messageId = str info "id"

        if role = "assistant" then
            let parentId =
                let p = str info "parentID"

                if p = "" then
                    let p2 = str info "parentId"
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
