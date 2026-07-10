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
open Wanxiangshu.Shell.FallbackEventBridge
open Wanxiangshu.Shell.FallbackRuntimeState

let private getEventType (rawEvent: obj) : string =
    Dyn.str (Dyn.get rawEvent "event") "type"

let private getProps (rawEvent: obj) : obj =
    let event = Dyn.get rawEvent "event"
    let rawProps = Dyn.get event "properties"
    if Dyn.isNullish rawProps then event else rawProps

let private opencodeErrorInput (errorObj: obj) : ErrorInput =
    let errorName = Dyn.str errorObj "name"
    let message = Dyn.str errorObj "message"

    { ErrorName = errorName
      DomainError = Some(translateJsError errorObj)
      Message = message
      StatusCode =
        let sc = Dyn.str errorObj "statusCode"
        if sc <> "" then Some(int sc) else None
      IsRetryable =
        let ir = Dyn.str errorObj "isRetryable"
        if ir <> "" then Some(ir = "true") else None }

let private invokeClient (client: obj) (method_: string) (arg: obj) : JS.Promise<obj> =
    if Dyn.isNullish client then
        Promise.lift (unbox null)
    else
        match getSessionApiFromClient client with
        | Error _ -> Promise.lift (unbox null)
        | Ok session ->
            let api: obj = Dyn.get session method_

            if Dyn.isNullish api then
                Promise.lift (unbox null)
            else
                unbox<JS.Promise<obj>> (Dyn.callMethod1 session method_ arg)

let private tryReadLatestMessageInfo (client: obj) (sessionID: string) : JS.Promise<obj option> =
    promise {
        let arg = box {| path = box {| id = sessionID |} |}
        let! resp = invokeClient client "messages" arg
        let data = Dyn.get resp "data"

        if not (Dyn.isArray data) then
            return None
        else
            let messages = data :?> obj array

            return
                messages
                |> Array.tryFindBack (fun msg ->
                    let info = Dyn.get msg "info"

                    if Dyn.isNullish info then
                        false
                    else
                        let mObj = Dyn.get info "model"
                        let aObj = Dyn.get info "agent"
                        not (Dyn.isNullish mObj) || (not (Dyn.isNullish aObj) && string aObj <> ""))
                |> Option.map (fun msg -> Dyn.get msg "info")
    }

let private tryGetSessionModelAndAgent (client: obj) : (FallbackModel option * string option) =
    match getSessionApiFromClient client with
    | Error _ -> None, None
    | Ok session ->
        let mOpt =
            let modelObj = Dyn.get session "model"
            Wanxiangshu.Shell.FallbackMessageCodec.decodeModelFromObj modelObj

        let aOpt =
            let agentStr = Dyn.str session "agent"
            if agentStr <> "" then Some agentStr else None

        mOpt, aOpt

let private tryReadCurrentModel (client: obj) (sessionID: string) : JS.Promise<FallbackModel option> =
    promise {
        if Dyn.isNullish client then
            return None
        else
            let sessionModel, _ = tryGetSessionModelAndAgent client

            match sessionModel with
            | Some m -> return Some m
            | None ->
                let! infoOpt = tryReadLatestMessageInfo client sessionID

                match infoOpt with
                | None -> return None
                | Some info ->
                    let model = Dyn.get info "model"
                    return Wanxiangshu.Shell.FallbackMessageCodec.decodeModelFromObj model
    }

let opencodeEventTranslator: IEventTranslator =
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
                let statusType = Dyn.str statusObj "type"

                if statusType = "interrupted" then
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
            let eventType = getEventType rawEvent

            if eventType = "session.idle" then
                true
            elif eventType = "session.status" then
                Dyn.str (Dyn.get (getProps rawEvent) "status") "type" = "idle"
            else
                false

        member _.IsSessionBusy rawEvent =
            let eventType = getEventType rawEvent

            if eventType = "session.status" then
                Dyn.str (Dyn.get (getProps rawEvent) "status") "type" = "busy"
            else
                false

        member _.IsNewUserMessage rawEvent =
            getEventType rawEvent = "message.updated"
            && Dyn.str (Dyn.get (getProps rawEvent) "info") "role" = "user"
            && not (Wanxiangshu.Shell.FallbackMessageCodec.isSystemForcedUserMessage (getProps rawEvent)) }

let opencodeActionExecutor (runtime: FallbackRuntimeState) (client: obj) : IActionExecutor =
    let resolveModelAndAgent (fallbackModel: FallbackModel) (sessionID: string) (infoOpt: obj option) =
        let _, sessionAgentOpt = tryGetSessionModelAndAgent client
        let finalModel = fallbackModel

        let modelStr =
            match finalModel.Variant with
            | Some v -> sprintf "%s/%s:%s" finalModel.ProviderID finalModel.ModelID v
            | None -> sprintf "%s/%s" finalModel.ProviderID finalModel.ModelID

        let agent =
            match sessionAgentOpt with
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
        member _.SendContinue(sessionID, model) =
            promise {
                let! infoOpt = tryReadLatestMessageInfo client sessionID
                let modelStr, agent = resolveModelAndAgent model sessionID infoOpt
                let body = createPromptBodyWithModel agent (Some modelStr) "continue"

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

        member _.RecoverWithPrompt(sessionID, model, promptText) =
            promise {
                let! infoOpt = tryReadLatestMessageInfo client sessionID
                let modelStr, agent = resolveModelAndAgent model sessionID infoOpt
                let body = createPromptBodyWithModel agent (Some modelStr) promptText

                let arg =
                    box
                        {| path = box {| id = sessionID |}
                           body = body |}

                do! invokeClient client "prompt" arg |> Promise.map ignore
            }

    }

let private setConsumedFromResult
    (runtime: FallbackRuntimeState)
    (sessionID: string)
    (result: FallbackHookResult)
    : unit =
    runtime.SetConsumed sessionID result.Consumed

let private clearConsumedOnNewUserMessage (runtime: FallbackRuntimeState) (sessionID: string) (rawEvent: obj) : unit =
    if opencodeEventTranslator.IsNewUserMessage rawEvent then
        runtime.ClearConsumed sessionID

let createOpencodeFallbackHandler
    (client: obj)
    (runtime: FallbackRuntimeState)
    (configLookup: ConfigLookup)
    (_registry: ChildAgentRegistry)
    : (obj -> JS.Promise<FallbackHookResult>) =
    let baseHandler =
        createHandler opencodeEventTranslator runtime configLookup (opencodeActionExecutor runtime client)

    fun rawEvent ->
        promise {
            let sessionID = opencodeEventTranslator.ExtractSessionID rawEvent
            let! result = baseHandler rawEvent
            setConsumedFromResult runtime sessionID result
            clearConsumedOnNewUserMessage runtime sessionID rawEvent
            return result
        }
