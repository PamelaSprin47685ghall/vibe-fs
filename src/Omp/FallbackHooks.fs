module Wanxiangshu.Omp.FallbackHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackEventBridge
open Wanxiangshu.Shell.FallbackRuntimeState

let ompErrorInput (errorObj: obj) : ErrorInput =
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

let ompEventTranslator: IEventTranslator =
    { new IEventTranslator with
        member _.TranslateError(rawEvent: obj) : FallbackEvent option =
            let eventObj = Dyn.get rawEvent "event"
            let eventType = Dyn.str eventObj "type"

            if eventType = "session.error" then
                let errorObj = Dyn.get eventObj "error"

                if Dyn.isNullish errorObj then
                    None
                else
                    Some(SessionError(ompErrorInput errorObj))
            elif eventType = "session.abort" || eventType = "session.interrupted" then
                Some(
                    SessionError
                        { ErrorName = "MessageAbortedError"
                          DomainError = Some MessageAborted
                          Message = "aborted"
                          StatusCode = None
                          IsRetryable = Some false }
                )
            else
                None

        member _.ExtractSessionID(rawEvent: obj) : string =
            Dyn.str (Dyn.get rawEvent "props") "sessionID"

        member _.IsSessionError(rawEvent: obj) : bool =
            let t = Dyn.str (Dyn.get rawEvent "event") "type"
            t = "session.error" || t = "session.abort" || t = "session.interrupted"

        member _.IsSessionIdle(rawEvent: obj) : bool =
            Dyn.str (Dyn.get rawEvent "event") "type" = "session.idle"

        member _.IsSessionBusy(rawEvent: obj) : bool =
            Dyn.str (Dyn.get rawEvent "event") "type" = "session.busy"

        member _.IsNewUserMessage(rawEvent: obj) : bool =
            let eventObj = Dyn.get rawEvent "event"

            Dyn.str eventObj "type" = "message.updated"
            && Dyn.str (Dyn.get eventObj "info") "role" = "user"
            && not (Wanxiangshu.Shell.FallbackMessageCodec.isSystemForcedUserMessage eventObj) }

let private tryGetSession (sessionID: string) (sessionApi: obj) : obj option =
    match Wanxiangshu.Omp.ExecutorTools.ompScope.TryFindKey("omp_session_" + sessionID) with
    | Some s -> Some s
    | None ->
        if not (Dyn.isNullish sessionApi) then
            Some sessionApi
        else
            None

let private captureModel (session: obj) : FallbackModel option =
    let model = Dyn.get session "model"
    Wanxiangshu.Shell.FallbackMessageCodec.decodeModelFromObj model

let private captureAgent (session: obj) : string option =
    let agent = Dyn.str session "agent"
    if agent <> "" then Some agent else None

let ompActionExecutor (runtime: FallbackRuntimeState) (sessionApi: obj) : IActionExecutor =
    let invoke (method_: string) (arg: obj) : JS.Promise<obj> =
        if Dyn.isNullish sessionApi then
            Promise.lift (unbox null)
        else
            unbox<JS.Promise<obj>> (sessionApi?(method_) (arg))

    let resolveModelAndAgent (fallbackModel: FallbackModel) (sessionID: string) =
        let sessionOpt = tryGetSession sessionID sessionApi
        let sessionAgentOpt = sessionOpt |> Option.bind captureAgent
        let finalModel = fallbackModel

        let modelStr =
            match finalModel.Variant with
            | Some v -> sprintf "%s/%s:%s" finalModel.ProviderID finalModel.ModelID v
            | None -> sprintf "%s/%s" finalModel.ProviderID finalModel.ModelID

        let agent =
            match sessionAgentOpt with
            | Some sa -> sa
            | None -> runtime.GetAgentName sessionID

        modelStr, agent

    let fetchMessages (sessionID: string) : JS.Promise<obj array> =
        promise {
            let arg = box {| sessionId = sessionID |}
            let! resp = invoke "sessionMessages" arg
            let data = Dyn.get resp "data"

            if Dyn.isArray data then
                return (data :?> obj array)
            else
                return [||]
        }

    { new IActionExecutor with
        member _.SendContinue(sessionID, model) =
            promise {
                let modelStr, agent = resolveModelAndAgent model sessionID

                let pObj =
                    let p =
                        {| text = "continue"
                           model = modelStr |}

                    if agent <> "" then Dyn.withKey p "agent" agent else box p

                let body = box {| prompt = pObj |}

                let arg = box {| sessionId = sessionID; body = body |}
                do! invoke "sessionPrompt" arg |> Promise.map ignore
            }

        member _.RecoverWithPrompt(sessionID, model, promptText) =
            promise {
                let modelStr, agent = resolveModelAndAgent model sessionID

                let pObj =
                    let p =
                        {| text = promptText
                           model = modelStr |}

                    if agent <> "" then Dyn.withKey p "agent" agent else box p

                let body = box {| prompt = pObj |}

                let arg = box {| sessionId = sessionID; body = body |}
                do! invoke "sessionPrompt" arg |> Promise.map ignore
            }

        member _.FetchMessages sessionID = fetchMessages sessionID

        member _.PropagateFailure(_sessionID: string) = Promise.lift ()

        member _.CaptureCurrentModel(sessionID: string) =
            promise {
                let! msgs = fetchMessages sessionID
                match Wanxiangshu.Shell.FallbackMessageCodec.tryGetLatestUserModel msgs with
                | Some m -> return Some m
                | None ->
                    match runtime.GetModel sessionID with
                    | Some m -> return Some m
                    | None ->
                        match tryGetSession sessionID sessionApi with
                        | Some sess -> return captureModel sess
                        | None -> return None
            }

    }

let private setConsumedFromResult
    (runtime: FallbackRuntimeState)
    (sessionID: string)
    (result: FallbackHookResult)
    : unit =
    runtime.SetConsumed sessionID result.Consumed

let private clearConsumedOnNewUserMessage (runtime: FallbackRuntimeState) (sessionID: string) (rawEvent: obj) : unit =
    if ompEventTranslator.IsNewUserMessage rawEvent then
        runtime.ClearConsumed sessionID

let private bindAgentAndModel (runtime: FallbackRuntimeState) (rawEvent: obj) : unit =
    let eventObj = Dyn.get rawEvent "event"
    let eventType = Dyn.str eventObj "type"

    if eventType = "session.busy" || eventType = "session.updated" then
        let info = Dyn.get eventObj "info"

        if not (Dyn.isNullish info) then
            let sid = ompEventTranslator.ExtractSessionID rawEvent

            if sid <> "" then
                let agent = Dyn.str info "agent"

                if agent <> "" then
                    runtime.SetAgentName sid agent

                let modelObj = Dyn.get info "model"

                match Wanxiangshu.Shell.FallbackMessageCodec.decodeModelFromObj modelObj with
                | Some m -> runtime.SetModel sid m
                | None -> ()

let createOmpFallbackHandler
    (runtime: FallbackRuntimeState)
    (configLookup: ConfigLookup)
    (sessionApi: obj)
    : (obj -> JS.Promise<FallbackHookResult>) =
    let baseHandler =
        createHandler ompEventTranslator runtime configLookup (ompActionExecutor runtime sessionApi)

    fun (rawEvent: obj) ->
        promise {
            let sessionID = ompEventTranslator.ExtractSessionID rawEvent
            bindAgentAndModel runtime rawEvent
            let! result = baseHandler rawEvent
            setConsumedFromResult runtime sessionID result
            clearConsumedOnNewUserMessage runtime sessionID rawEvent
            return result
        }
