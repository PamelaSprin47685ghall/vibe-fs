module Wanxiangshu.Opencode.FallbackHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodecCommon
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.PartTypeClassify
open Wanxiangshu.Shell.FallbackEventBridge
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.SubsessionEventRouter
open Wanxiangshu.Shell.SubsessionChildObserver
open Wanxiangshu.Opencode.FallbackHooksHelper

/// Zero-width space character used as the fallback `SendContinue` prompt
/// text. It is invisible in any UI but still parsed by LLMs as a non-empty
/// text, so it triggers the protocol-driven "continue" loop without leaving
/// a "continue" footprint in the LLM history that the consumer side could
/// mistake for a real user message.
let private zwsChar = "​"

let private isSyntheticText (text: string) : bool =
    let t = text.Trim()

    t = "​"
    || t.Contains("There are still incomplete todos")
    || t.Contains("You are in loop mode. You must call the submit_review")
    || t.Contains("A background runner task is still active")
    || t.Contains("the system context is about to be suspended")
    || t.Contains("You must immediately force an emergency stop")

let private tryGetModelStringFromInfo (info: obj) : string option =
    if isNull info || Dyn.isNullish info then
        None
    else
        let modelVal = Dyn.get info "model"

        if isNull modelVal || Dyn.isNullish modelVal then
            None
        elif Dyn.typeIs modelVal "string" then
            let s = string modelVal
            if s = "" then None else Some s
        else
            let providerID = Dyn.str modelVal "providerID"
            let modelID = Dyn.str modelVal "modelID"
            let variant = Dyn.str modelVal "variant"
            let suffix = if variant <> "" then ":" + variant else ""

            if providerID = "" || modelID = "" then
                let idVal = Dyn.str modelVal "id"
                if idVal <> "" then Some(idVal + suffix) else None
            else
                Some(sprintf "%s/%s%s" providerID modelID suffix)

let private isNewUserMessageImpl (runtime: FallbackRuntimeState) (sessionID: string) (rawEvent: obj) : bool = false

let opencodeEventTranslator (runtime: FallbackRuntimeState) : IEventTranslator =
    { new IEventTranslator with
        member _.TranslateError rawEvent =
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

        member _.ExtractSessionID rawEvent =
            getSessionID (getEventType rawEvent) (getProps rawEvent)

        member _.IsSessionError rawEvent =
            let t = getEventType rawEvent
            t = "session.error" || t = "session.interrupted"

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

        member _.ExtractNewUserMessageId(_rawEvent) = None

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

        member _.IsAssistantMessage rawEvent =
            let eventType = getEventType rawEvent
            let props = getProps rawEvent

            (eventType = "message.updated" || eventType.StartsWith("message.part."))
            && not (Dyn.isNullish props)
            && (let info = Dyn.get props "info"
                not (Dyn.isNullish info) && Dyn.str info "role" = "assistant")

        member _.ExtractAssistantMessageId rawEvent =
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

                let id = Dyn.str info "id"
                if id <> "" then Some id else None

        member _.ExtractAssistantParentId rawEvent =
            let props = getProps rawEvent
            let info = Dyn.get props "info"
            let msg = Dyn.get props "message"

            let info =
                if not (Dyn.isNullish info) then info
                else if not (Dyn.isNullish msg) then msg
                else props

            let pid = Dyn.str info "parentID"
            let pid = if pid <> "" then pid else Dyn.str info "parentId"
            if pid <> "" then Some pid else None

        member _.ExtractContinuationIdentity rawEvent =
            let props = getProps rawEvent
            let props = if Dyn.isNullish props then rawEvent else props
            let cid = Dyn.str props "continuationId"
            let cid = if cid <> "" then cid else Dyn.str props "continuationID"
            let cid = if cid <> "" then cid else Dyn.str rawEvent "continuationId"
            let cid = if cid <> "" then cid else Dyn.str rawEvent "continuationID"
            let o = Dyn.get props "continuationOrdinal"

            let o =
                if Dyn.isNullish o then
                    Dyn.get rawEvent "continuationOrdinal"
                else
                    o

            let ord = getOrdinal o
            if cid <> "" then Some(cid, ord) else None

        member _.ExtractHostRunId rawEvent =
            let props = getProps rawEvent
            let props = if Dyn.isNullish props then rawEvent else props
            let info = Dyn.get props "info"
            let info = if Dyn.isNullish info then props else info
            let tid = Dyn.str info "turnId"
            let tid = if tid <> "" then tid else Dyn.str info "turnID"
            let tid = if tid <> "" then tid else Dyn.str info "runId"
            let tid = if tid <> "" then tid else Dyn.str info "runID"
            if tid <> "" then Some tid else None

        member _.ExtractTurnObservation rawEvent : TurnObservation option =
            let eventType = getEventType rawEvent
            let props = getProps rawEvent

            if eventType = "message.updated" && not (Dyn.isNullish props) then
                let info = Dyn.get props "info"

                if not (Dyn.isNullish info) && Dyn.str info "role" = "assistant" then
                    let parts = Dyn.get props "parts"
                    let text = getPartsText parts

                    let hasToolCall =
                        if not (Dyn.isNullish parts) && Dyn.isArray parts then
                            (parts :?> obj array)
                            |> Array.exists (fun p -> let pt = Dyn.str p "type" in isToolCallPartType pt)
                        else
                            false

                    Some
                        { TurnId = TurnId.create ""
                          Evidence =
                            { CurrentTurnEvidence.empty with
                                Assistant =
                                    AssistantContent(text, Some(if hasToolCall then ToolFinish else NormalFinish)) } }
                else
                    let parts = Dyn.get props "parts"

                    if not (Dyn.isNullish parts) && Dyn.isArray parts then
                        let partsArr = parts :?> obj array

                        let hasToolResult =
                            partsArr
                            |> Array.exists (fun part -> let pt = Dyn.str part "type" in isToolResultPartType pt)

                        if hasToolResult then
                            Some
                                { TurnId = TurnId.create ""
                                  Evidence =
                                    { CurrentTurnEvidence.empty with
                                        Tool = HasToolResult } }
                        else
                            None
                    else
                        None
            else
                None }

let opencodeActionExecutor (runtime: FallbackRuntimeState) (client: obj) : IActionExecutor =
    let resolveModelAndAgent
        (liveAgentOpt: string option)
        (fallbackModel: FallbackModel)
        (sessionID: string)
        (infoOpt: obj option)
        =
        let finalModel = fallbackModel

        let modelStr =
            match finalModel.Variant with
            | Some v -> sprintf "%s/%s:%s" finalModel.ProviderID finalModel.ModelID v
            | None -> sprintf "%s/%s" finalModel.ProviderID finalModel.ModelID

        let agent =
            match liveAgentOpt with
            | Some sa -> Some sa
            | None ->
                let fromMsg =
                    infoOpt
                    |> Option.map (fun info -> Dyn.str info "agent")
                    |> Option.filter (fun value -> value <> "")

                match fromMsg with
                | Some a -> Some a
                | None ->
                    let fromRuntime = runtime.GetAgentName sessionID
                    if fromRuntime <> "" then Some fromRuntime else None

        modelStr, agent

    let fetchMessages (sessionID: string) : JS.Promise<obj array> =
        promise {
            let arg = box {| path = box {| id = sessionID |} |}
            let! resp = invokeClient client "messages" arg
            let data = Dyn.get resp "data"

            if Dyn.isArray data then
                return (data :?> obj array)
            else
                return [||]
        }

    { new IActionExecutor with
        member _.SendContinue(sessionID, model, continuationID) =
            promise {
                let! _, liveAgentOpt = tryGetSessionModelAndAgentAsync client sessionID
                let! infoOpt = tryReadLatestMessageInfo client sessionID
                let modelStr, agent = resolveModelAndAgent liveAgentOpt model sessionID infoOpt

                let body =
                    createPromptBodyWithModelAndNonce agent (Some modelStr) zwsChar (Some continuationID)

                let arg =
                    box
                        {| path = box {| id = sessionID |}
                           body = body |}

                do! invokeClient client "prompt" arg |> Promise.map ignore
            }

        member _.FetchMessages sessionID = fetchMessages sessionID

        member _.PropagateFailure _sessionID = Promise.lift ()

        member _.CaptureCurrentModel sessionID =
            promise {
                let! msgs = fetchMessages sessionID

                match Wanxiangshu.Shell.FallbackMessageCodec.tryGetLatestUserModel msgs with
                | Some m -> return Some m
                | None ->
                    match runtime.GetModel sessionID with
                    | Some m -> return Some m
                    | None -> return! tryReadCurrentModel client sessionID
            }

        member _.RecoverWithPrompt(sessionID, model, promptText, continuationID) =
            promise {
                let! _, liveAgentOpt = tryGetSessionModelAndAgentAsync client sessionID
                let! infoOpt = tryReadLatestMessageInfo client sessionID
                let modelStr, agent = resolveModelAndAgent liveAgentOpt model sessionID infoOpt

                let body =
                    createPromptBodyWithModelAndNonce agent (Some modelStr) promptText (Some continuationID)

                let arg =
                    box
                        {| path = box {| id = sessionID |}
                           body = body |}

                do! invokeClient client "prompt" arg |> Promise.map ignore
            }

        member _.AbortRun sessionID =
            promise {
                let arg = box {| path = box {| id = sessionID |} |}
                do! invokeClient client "abort" arg |> Promise.map ignore
            }

    }

let private setConsumedFromResult
    (runtime: FallbackRuntimeState)
    (sessionID: string)
    (result: FallbackHookResult)
    : unit =
    runtime.SetConsumed sessionID result.Consumed

let private clearConsumedOnNewUserMessage (runtime: FallbackRuntimeState) (sessionID: string) (rawEvent: obj) : unit =
    if isNewUserMessageImpl runtime sessionID rawEvent then
        runtime.ClearConsumed sessionID

let createOpencodeFallbackHandler
    (client: obj)
    (runtime: FallbackRuntimeState)
    (configLookup: ConfigLookup)
    (workspaceRoot: string)
    (_registry: ChildAgentRegistry)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    : (obj -> JS.Promise<FallbackHookResult>) =
    let translator = opencodeEventTranslator runtime

    let pendingReview sid =
        reviewStore.getPendingReviewIds () |> List.contains sid

    let baseHandler =
        createHandler
            translator
            runtime
            configLookup
            (opencodeActionExecutor runtime client)
            workspaceRoot
            (Some pendingReview)

    fun rawEvent ->
        promise {
            let sessionID = translator.ExtractSessionID rawEvent

            // Child sessions are owned solely by SubsessionActor. Route control facts
            // there; absorb busy/message.* metadata without main fallback.
            let! routed =
                promise {
                    if translator.IsSessionError rawEvent then
                        let errorObj =
                            match translator.TranslateError rawEvent with
                            | Some(FallbackEvent.SessionError err) -> err
                            | _ ->
                                { ErrorName = "UnknownError"
                                  DomainError = None
                                  Message = "An unknown error occurred"
                                  StatusCode = None
                                  IsRetryable = None }

                        return! tryError sessionID errorObj
                    elif translator.IsSessionIdle rawEvent then
                        return! tryIdle sessionID
                    elif isChildSession sessionID then
                        // busy / message.updated / part.* — observe model/agent only.
                        match translator.ExtractTurnObservation rawEvent with
                        | Some obs -> do! routeToChild sessionID (EvidenceUpdated obs) |> Promise.map ignore
                        | None -> ()

                        return absorbChildMetadata runtime sessionID rawEvent
                    else
                        return false
                }

            if routed then
                return
                    { Consumed = true
                      State = runtime.GetOrCreateState sessionID }
            else
                let! result = baseHandler rawEvent
                setConsumedFromResult runtime sessionID result
                clearConsumedOnNewUserMessage runtime sessionID rawEvent
                return result
        }
