module Wanxiangshu.Hosts.Opencode.Fallback.MessageInspectionObservation
open Wanxiangshu.Runtime

open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Messaging.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.TypeClassify
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection
open Wanxiangshu.Runtime.Fallback.FallbackMessageDetection

let tryExtractTurnIdFromEvent (rawEvent: obj) : TurnId option =
    let props = getProps rawEvent
    let info = Dyn.get props "info"
    let info = if Dyn.isNullish info then props else info
    let tid = Dyn.str info "turnId"
    let tid = if tid <> "" then tid else Dyn.str info "turnID"
    let tid = if tid <> "" then tid else Dyn.str info "runId"
    let tid = if tid <> "" then tid else Dyn.str info "runID"
    if tid <> "" then Some(TurnId.create tid) else None

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

let extractTurnObservationImpl (rawEvent: obj) : TurnObservation option =
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

let isAssistantMessageImpl (rawEvent: obj) : bool =
    let eventType = getEventType rawEvent
    let props = getProps rawEvent

    (eventType = "message.updated" || eventType.StartsWith("message.part."))
    && not (Dyn.isNullish props)
    && (let info = Dyn.get props "info" in not (Dyn.isNullish info) && Dyn.str info "role" = "assistant")
