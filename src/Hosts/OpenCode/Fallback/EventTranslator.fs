module Wanxiangshu.Hosts.Opencode.Fallback.EventTranslator

/// OpenCode fallback IEventTranslator implementation.

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Hosts.OpenCode.OpencodeSessionEventCodec
open Wanxiangshu.Hosts.Opencode.OpencodeHostEvent
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
open Wanxiangshu.Runtime.Fallback.HostEventInspection
open Wanxiangshu.Hosts.Opencode.Fallback.MessageInspection

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
