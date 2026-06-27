module Wanxiangshu.Opencode.FallbackHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodecCommon
open Wanxiangshu.Shell.ChildAgentRegistry
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
    { ErrorName   = Dyn.str errorObj "name"
      Message     = Dyn.str errorObj "message"
      StatusCode  =
          let sc = Dyn.str errorObj "statusCode"
          if sc <> "" then Some (int sc) else None
      IsRetryable =
          let ir = Dyn.str errorObj "isRetryable"
          if ir <> "" then Some (ir = "true") else None }

let private invokeClient (client: obj) (method_: string) (arg: obj) : JS.Promise<obj> =
    if Dyn.isNullish client then
        Promise.lift (unbox null)
    else
        match getSessionApiFromClient client with
        | Error _ -> Promise.lift (unbox null)
        | Ok session ->
            let api : obj = session?(method_)
            if Dyn.isNullish api then Promise.lift (unbox null)
            else unbox<JS.Promise<obj>> (api?(arg))

let opencodeEventTranslator : IEventTranslator =
    { new IEventTranslator with
        member _.TranslateError rawEvent =
            if getEventType rawEvent <> "session.error" then None
            else
                let errorObj = Dyn.get (getProps rawEvent) "error"
                if Dyn.isNullish errorObj then None
                else Some (FallbackEvent.SessionError (opencodeErrorInput errorObj))

        member _.ExtractSessionID rawEvent =
            getSessionID (getEventType rawEvent) (getProps rawEvent)

        member _.IsSessionError rawEvent =
            getEventType rawEvent = "session.error"

        member _.IsSessionIdle rawEvent =
            let eventType = getEventType rawEvent
            if eventType = "session.idle" then true
            elif eventType = "session.status" then
                Dyn.str (Dyn.get (getProps rawEvent) "status") "type" = "idle"
            else false

        member _.IsSessionBusy rawEvent =
            let eventType = getEventType rawEvent
            if eventType = "session.status" then
                Dyn.str (Dyn.get (getProps rawEvent) "status") "type" = "busy"
            else false

        member _.IsNewUserMessage rawEvent =
            getEventType rawEvent = "message.updated"
            && Dyn.str (Dyn.get (getProps rawEvent) "info") "role" = "user" }

let opencodeActionExecutor (client: obj) : IActionExecutor =
    { new IActionExecutor with
        member _.SendContinue (sessionID, model) =
            promise {
                let modelStr =
                    match model.Variant with
                    | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                    | None -> sprintf "%s/%s" model.ProviderID model.ModelID
                let body = createPromptBodyWithModel None (Some modelStr) "continue"
                let arg = box {| path = box {| id = sessionID |}; body = body |}
                do! invokeClient client "prompt" arg |> Promise.map ignore
            }

        member _.AbortSession sessionID =
            promise {
                let arg = box {| path = box {| id = sessionID |} |}
                do! invokeClient client "abort" arg |> Promise.map ignore
            }

        member _.FetchMessages sessionID =
            promise {
                let arg = box {| path = box {| id = sessionID |} |}
                let! resp = invokeClient client "messages" arg
                let data = Dyn.get resp "data"
                if Dyn.isArray data then return (data :?> obj array)
                else return [||]
            }

        member _.PropagateFailure _sessionID = Promise.lift () }

let private setConsumedFromResult (runtime: FallbackRuntimeState) (sessionID: string) (result: FallbackHookResult) : unit =
    runtime.SetConsumed sessionID result.Consumed

let private clearConsumedOnLifecycle (runtime: FallbackRuntimeState) (sessionID: string) (rawEvent: obj) : unit =
    let eventType = getEventType rawEvent
    if eventType = "session.busy" || eventType = "session.idle" || eventType = "message.updated" then
        runtime.ClearConsumed sessionID

let createOpencodeFallbackHandler
    (client: obj)
    (runtime: FallbackRuntimeState)
    (configLookup: ConfigLookup)
    (_registry: ChildAgentRegistry)
    : (obj -> JS.Promise<FallbackHookResult>) =
    let baseHandler = createHandler opencodeEventTranslator runtime configLookup (opencodeActionExecutor client)
    fun rawEvent ->
        promise {
            let sessionID = opencodeEventTranslator.ExtractSessionID rawEvent
            let! result = baseHandler rawEvent
            setConsumedFromResult runtime sessionID result
            clearConsumedOnLifecycle runtime sessionID rawEvent
            return result
        }

let createOpencodeFallbackHandlerLegacy
    (client: obj)
    (runtime: FallbackRuntimeState)
    (configLookup: ConfigLookup)
    : (obj -> JS.Promise<FallbackHookResult>) =
    createHandler opencodeEventTranslator runtime configLookup (opencodeActionExecutor client)

let createOpencodeFallbackHandlerWithRegistry
    (client: obj)
    (runtime: FallbackRuntimeState)
    (configLookup: ConfigLookup)
    (registry: ChildAgentRegistry)
    : (obj -> JS.Promise<FallbackHookResult>) =
    createOpencodeFallbackHandler client runtime configLookup registry

let trackConsumedFromResult (runtime: FallbackRuntimeState) (sessionID: string) (result: FallbackHookResult) : unit =
    setConsumedFromResult runtime sessionID result

let clearConsumedOnLifecycleEvent (runtime: FallbackRuntimeState) (sessionID: string) (rawEvent: obj) : unit =
    clearConsumedOnLifecycle runtime sessionID rawEvent
