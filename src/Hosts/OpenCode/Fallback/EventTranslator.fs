module Wanxiangshu.Hosts.Opencode.Fallback.EventTranslator

/// Private implementation helpers for the OpenCode fallback event translator.

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.OpencodeHostEvent
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.PartTypeClassify
open Wanxiangshu.Runtime.Fallback.FallbackEventBridge
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.FallbackBridgePorts
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection

let private tryExtractTurnIdFromEvent (rawEvent: obj) : TurnId option =
    let props = getProps rawEvent
    let info = Dyn.get props "info"
    let info = if Dyn.isNullish info then props else info
    let tid = Dyn.str info "turnId"
    let tid = if tid <> "" then tid else Dyn.str info "turnID"
    let tid = if tid <> "" then tid else Dyn.str info "runId"
    let tid = if tid <> "" then tid else Dyn.str info "runID"
    if tid <> "" then Some(TurnId.create tid) else None

let private tryGetModelStringFromInfo (info: obj) : string option =
    if Dyn.isNullish info then
        None
    else
        let mv = Dyn.get info "model"

        if Dyn.isNullish mv then
            None
        elif Dyn.typeIs mv "string" then
            let s = string mv in if s = "" then None else Some s
        else
            let pID, mID, variant =
                Dyn.str mv "providerID", Dyn.str mv "modelID", Dyn.str mv "variant"

            let suffix = if variant <> "" then ":" + variant else ""

            if pID = "" || mID = "" then
                let idVal = Dyn.str mv "id" in if idVal <> "" then Some(idVal + suffix) else None
            else
                Some(sprintf "%s/%s%s" pID mID suffix)

let isNewUserMessageImpl (runtime: FallbackRuntimeStore) (sessionID: string) (rawEvent: obj) : bool =
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

        not hasSyntheticMarker

let private translateErrorImpl (rawEvent: obj) : FallbackEvent option =
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

let private extractTurnObservationImpl (rawEvent: obj) : TurnObservation option =
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

let private isAssistantMessageImpl (rawEvent: obj) : bool =
    let eventType = getEventType rawEvent
    let props = getProps rawEvent

    (eventType = "message.updated" || eventType.StartsWith("message.part."))
    && not (Dyn.isNullish props)
    && (let info = Dyn.get props "info" in not (Dyn.isNullish info) && Dyn.str info "role" = "assistant")

let private extractAssistantMessageIdImpl (rawEvent: obj) : string option =
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

let private extractAssistantParentIdImpl (rawEvent: obj) : string option =
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

let private extractContinuationIdentityImpl (rawEvent: obj) : (string * int) option =
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

let private extractHostRunIdImpl (rawEvent: obj) : string option =
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

let opencodeEventTranslator (runtime: FallbackRuntimeStore) : IEventTranslator =
    { new IEventTranslator with
        member _.TranslateError rawEvent = translateErrorImpl rawEvent

        member _.ExtractSessionID rawEvent =
            getSessionID (getEventType rawEvent) (getProps rawEvent)

        member _.IsSessionError rawEvent =
            let t = getEventType rawEvent in t = "session.error" || t = "session.interrupted"

        member _.IsSessionIdle rawEvent =
            let t = getEventType rawEvent

            t = "session.idle"
            || (t = "session.status"
                && resolveStatusValue (Dyn.get (getProps rawEvent) "status") = "idle")

        member _.IsSessionBusy rawEvent =
            let t = getEventType rawEvent

            t = "session.status"
            && resolveStatusValue (Dyn.get (getProps rawEvent) "status") = "busy"

        member _.IsNewUserMessage(sessionID, rawEvent) =
            isNewUserMessageImpl runtime sessionID rawEvent

        member _.ExtractNewUserMessageId(rawEvent) =
            let props = getProps rawEvent
            let info = Dyn.get props "info" |> fun x -> if Dyn.isNullish x then props else x
            let id = Dyn.str info "id" in
            if id = "" then None else Some id

        member _.ExtractRoutingContext(rawEvent) =
            let props = getProps rawEvent
            let info = Dyn.get props "info"
            let modelStr = tryGetModelStringFromInfo info
            let agentVal = Dyn.get info "agent"

            let agent =
                if Dyn.isNullish agentVal then
                    None
                else
                    Some(string agentVal)

            modelStr, agent

        member _.IsAssistantMessage rawEvent = isAssistantMessageImpl rawEvent
        member _.ExtractAssistantMessageId rawEvent = extractAssistantMessageIdImpl rawEvent
        member _.ExtractAssistantParentId rawEvent = extractAssistantParentIdImpl rawEvent

        member _.ExtractContinuationIdentity rawEvent =
            extractContinuationIdentityImpl rawEvent

        member _.ExtractHostRunId rawEvent = extractHostRunIdImpl rawEvent
        member _.ExtractTurnObservation rawEvent = extractTurnObservationImpl rawEvent }

type HostEventIdentity =
    { SessionId: string
      UserMessageId: string option
      AssistantMessageId: string option
      ParentMessageId: string option }

let extractHostEventIdentity (rawEvent: obj) : HostEventIdentity =
    let sessionId = getSessionID (getEventType rawEvent) (getProps rawEvent)

    let props = getProps rawEvent
    let info = Dyn.get props "info" |> fun x -> if Dyn.isNullish x then props else x
    let msgId = Dyn.str info "id"
    let messageId = if msgId = "" then None else Some msgId

    let assistantMessageId = extractAssistantMessageIdImpl rawEvent
    let parentMessageId = extractAssistantParentIdImpl rawEvent

    { SessionId = sessionId
      UserMessageId = messageId
      AssistantMessageId = assistantMessageId
      ParentMessageId = parentMessageId }
