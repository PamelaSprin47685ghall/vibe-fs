module Wanxiangshu.Hosts.Opencode.Fallback.MessageInspection

/// Host-specific helpers for inspecting OpenCode fallback messages and events.

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.Messaging.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.TypeClassify
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection
open Wanxiangshu.Runtime.Fallback.FallbackMessageDetection

let private isSyntheticText (text: string) : bool =
    let t = text.Trim()

    t = "\u200b"
    || t.Contains("There are still incomplete todos")
    || t.Contains("command: with-review")
    || t.Contains("You are in loop mode. You must call the submit_review")
    || t.Contains("A background runner task is still active")
    || t.Contains("the system context is about to be suspended")
    || t.Contains("You must immediately force an emergency stop")

let private tryExtractTurnIdFromEvent (rawEvent: obj) : TurnId option =
    let props = getProps rawEvent
    let info = Dyn.get props "info"
    let info = if Dyn.isNullish info then props else info
    let tid = Dyn.str info "turnId"
    let tid = if tid <> "" then tid else Dyn.str info "turnID"
    let tid = if tid <> "" then tid else Dyn.str info "runId"
    let tid = if tid <> "" then tid else Dyn.str info "runID"
    if tid <> "" then Some(TurnId.create tid) else None

let internal isNewUserMessageImpl (runtime: FallbackRuntimeStore) (sessionID: string) (rawEvent: obj) : bool =
    let props = getProps rawEvent
    let parts = Dyn.get props "parts"

    if Dyn.isNullish parts then
        false
    else
        let partsArr = parts :?> obj array
        let text = getPartsText parts

        let hasSyntheticMarker =
            partsArr
            |> Array.exists (fun part ->
                let synthetic = Dyn.get part "synthetic"
                not (Dyn.isNullish synthetic) && unbox<bool> synthetic)

        not hasSyntheticMarker && not (isSyntheticText text)

let internal translateErrorImpl (rawEvent: obj) : FallbackEvent option =
    let eventType = getEventType rawEvent

    if eventType = "session.error" then
        let errorObj = Dyn.get (getProps rawEvent) "error"

        if Dyn.isNullish errorObj then
            None
        else
            Some(FallbackEvent.SessionError(opencodeErrorInput errorObj))
    elif eventType = "session.interrupted" then
        Some(
            FallbackEvent.SessionError
                { ErrorName = "MessageAbortedError"
                  DomainError = Some MessageAborted
                  Message = "interrupted"
                  StatusCode = None
                  IsRetryable = Some false }
        )
    elif eventType = "session.status" then
        let statusObj = Dyn.get (getProps rawEvent) "status"
        let status = resolveStatusValue statusObj

        if status = "interrupted" || status = "abort" then
            Some(
                FallbackEvent.SessionError
                    { ErrorName = "MessageAbortedError"
                      DomainError = Some MessageAborted
                      Message = "interrupted"
                      StatusCode = None
                      IsRetryable = Some false }
            )
        else
            None
    else
        None

let private extractAssistantObservation (rawEvent: obj) (props: obj) (info: obj) : TurnObservation option =
    let parts = Dyn.get props "parts"
    let text = getPartsText parts

    let hasToolCall =
        if not (Dyn.isNullish parts) && Dyn.isArray parts then
            (parts :?> obj array)
            |> Array.exists (fun p -> let pt = Dyn.str p "type" in isToolCallPartType pt)
        else
            false

    let assistantEvidence =
        let eventType = getEventType rawEvent

        if eventType.StartsWith("message.part.") then
            AssistantDelta("", 0L, text, Some(if hasToolCall then ToolFinish else NormalFinish))
        else
            AssistantSnapshot("", 0L, text, Some(if hasToolCall then ToolFinish else NormalFinish))

    let recovery =
        if getEventType rawEvent = "message.updated" then
            match scanToolCallAsText [| rawEvent |] with
            | Some prompt -> RawToolCallDetected prompt
            | None -> NoRecoveryPrompt
        else
            NoRecoveryPrompt

    Some
        { TurnId = tryExtractTurnIdFromEvent rawEvent
          Evidence =
            { CurrentTurnEvidence.empty with
                Assistant = assistantEvidence
                Recovery = recovery } }

let private extractToolObservation (rawEvent: obj) (props: obj) : TurnObservation option =
    let parts = Dyn.get props "parts"

    if not (Dyn.isNullish parts) && Dyn.isArray parts then
        let partsArr = parts :?> obj array

        if
            partsArr
            |> Array.exists (fun part -> let pt = Dyn.str part "type" in isToolResultPartType pt)
        then
            Some
                { TurnId = tryExtractTurnIdFromEvent rawEvent
                  Evidence =
                    { CurrentTurnEvidence.empty with
                        Tool = HasToolResult } }
        else
            None
    else
        None

let internal extractTurnObservationImpl (rawEvent: obj) : TurnObservation option =
    let eventType = getEventType rawEvent
    let props = getProps rawEvent

    if eventType = "message.updated" || eventType.StartsWith("message.part.") then
        let info = Dyn.get props "info"

        if not (Dyn.isNullish info) && Dyn.str info "role" = "assistant" then
            extractAssistantObservation rawEvent props info
        else
            extractToolObservation rawEvent props
    else
        None

let internal isAssistantMessageImpl (rawEvent: obj) : bool =
    let eventType = getEventType rawEvent
    let props = getProps rawEvent

    (eventType = "message.updated" || eventType.StartsWith("message.part."))
    && not (Dyn.isNullish props)
    && (let info = Dyn.get props "info" in not (Dyn.isNullish info) && Dyn.str info "role" = "assistant")

let internal extractAssistantMessageIdImpl (rawEvent: obj) : string option =
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

let internal extractAssistantParentIdImpl (rawEvent: obj) : string option =
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

let internal extractContinuationIdentityImpl (rawEvent: obj) : (string * int) option =
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

let internal extractHostRunIdImpl (rawEvent: obj) : string option =
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
