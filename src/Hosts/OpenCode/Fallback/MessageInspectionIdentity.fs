module Wanxiangshu.Hosts.Opencode.Fallback.MessageInspectionIdentity
open Wanxiangshu.Runtime

open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Messaging.OpencodeSessionEventCodec
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection
open Wanxiangshu.Runtime.Fallback.Ports

let extractAssistantMessageIdImpl (rawEvent: obj) : string option =
    let eventType = getEventType rawEvent

    if eventType = "session.idle" || eventType = "session.error" then
        None
    else
        let props = getProps rawEvent
        let info = Dyn.get props "info"
        let msg = Dyn.get props "message"

        let info =
            if not (Dyn.isNullish info) then info
            else if not (Dyn.isNullish msg) then msg
            else props

        let id = Dyn.str info "id" in
        if id <> "" then Some id else None

let extractAssistantParentIdImpl (rawEvent: obj) : string option =
    let props = getProps rawEvent
    let info = Dyn.get props "info"
    let msg = Dyn.get props "message"

    let info =
        if not (Dyn.isNullish info) then info
        else if not (Dyn.isNullish msg) then msg
        else props

    let pid = Dyn.str info "parentID" in
    let pid = if pid <> "" then pid else Dyn.str info "parentId"
    if pid <> "" then Some pid else None

let extractContinuationIdentityImpl (rawEvent: obj) : (string * int) option =
    let props = getProps rawEvent
    let props = if Dyn.isNullish props then rawEvent else props

    let cid =
        Dyn.str props "continuationId"
        |> fun c -> if c <> "" then c else Dyn.str props "continuationID"

    let cid =
        if cid <> "" then
            cid
        else
            Dyn.str rawEvent "continuationId"
            |> fun c -> if c <> "" then c else Dyn.str rawEvent "continuationID"

    let o =
        Dyn.get props "continuationOrdinal"
        |> fun x ->
            if Dyn.isNullish x then
                Dyn.get rawEvent "continuationOrdinal"
            else
                x

    let ord = getOrdinal o
    if cid <> "" then Some(cid, ord) else None

let extractHostRunIdImpl (rawEvent: obj) : string option =
    let props = getProps rawEvent
    let props = if Dyn.isNullish props then rawEvent else props
    let info = Dyn.get props "info" |> fun x -> if Dyn.isNullish x then props else x

    let tid =
        Dyn.str info "turnId" |> fun t -> if t <> "" then t else Dyn.str info "turnID"

    let tid =
        if tid <> "" then
            tid
        else
            Dyn.str info "runId" |> fun t -> if t <> "" then t else Dyn.str info "runID"

    if tid <> "" then Some tid else None
