module Wanxiangshu.Omp.FallbackHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackEventBridge
open Wanxiangshu.Shell.FallbackRuntimeState

let ompErrorInput (errorObj: obj) : ErrorInput =
    { ErrorName   = Dyn.str errorObj "name"
      Message     = Dyn.str errorObj "message"
      StatusCode  =
          let sc = Dyn.str errorObj "statusCode"
          if sc <> "" then Some (int sc) else None
      IsRetryable =
          let ir = Dyn.str errorObj "isRetryable"
          if ir <> "" then Some (ir = "true") else None }

let ompEventTranslator : IEventTranslator =
    { new IEventTranslator with
        member _.TranslateError (rawEvent: obj) : FallbackEvent option =
            let eventObj = Dyn.get rawEvent "event"
            let eventType = Dyn.str eventObj "type"
            if eventType = "session.error" then
                let errorObj = Dyn.get eventObj "error"
                if Dyn.isNullish errorObj then None
                else Some (SessionError (ompErrorInput errorObj))
            else None

        member _.ExtractSessionID (rawEvent: obj) : string =
            Dyn.str (Dyn.get rawEvent "props") "sessionID"

        member _.IsSessionError (rawEvent: obj) : bool =
            Dyn.str (Dyn.get rawEvent "event") "type" = "session.error"

        member _.IsSessionIdle (rawEvent: obj) : bool =
            Dyn.str (Dyn.get rawEvent "event") "type" = "session.idle"

        member _.IsSessionBusy (rawEvent: obj) : bool =
            Dyn.str (Dyn.get rawEvent "event") "type" = "session.busy"

        member _.IsNewUserMessage (rawEvent: obj) : bool =
            let eventObj = Dyn.get rawEvent "event"
            Dyn.str eventObj "type" = "message.updated"
            && Dyn.str (Dyn.get eventObj "info") "role" = "user" }

let ompActionExecutor (sessionApi: obj) : IActionExecutor =
    let invoke (method_: string) (arg: obj) : JS.Promise<obj> =
        if Dyn.isNullish sessionApi then Promise.lift (unbox null)
        else unbox<JS.Promise<obj>> (sessionApi?(method_)(arg))

    { new IActionExecutor with
        member _.SendContinue (sessionID, model) =
            promise {
                let modelStr =
                    match model.Variant with
                    | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                    | None -> sprintf "%s/%s" model.ProviderID model.ModelID
                let body = box {| prompt = box {| text = "continue"; model = modelStr |} |}
                let arg = box {| sessionId = sessionID; body = body |}
                do! invoke "sessionPrompt" arg |> Promise.map ignore
            }

        member _.AbortSession sessionID =
            promise {
                let arg = box {| sessionId = sessionID |}
                do! invoke "sessionAbort" arg |> Promise.map ignore
            }

        member _.FetchMessages sessionID =
            promise {
                let arg = box {| sessionId = sessionID |}
                let! resp = invoke "sessionMessages" arg
                let data = Dyn.get resp "data"
                if Dyn.isArray data then return (data :?> obj array)
                else return [||]
            }

        member _.PropagateFailure (_sessionID: string) = Promise.lift () }

let private setConsumedFromResult (runtime: FallbackRuntimeState) (sessionID: string) (result: FallbackHookResult) : unit =
    runtime.SetConsumed sessionID result.Consumed

let private clearConsumedOnLifecycle (runtime: FallbackRuntimeState) (sessionID: string) (rawEvent: obj) : unit =
    let eventObj = Dyn.get rawEvent "event"
    let eventType = Dyn.str eventObj "type"
    if eventType = "session.busy" || eventType = "session.idle" || eventType = "message.updated" then
        runtime.ClearConsumed sessionID

let createOmpFallbackHandler
    (runtime       : FallbackRuntimeState)
    (configLookup  : ConfigLookup)
    (sessionApi    : obj)
    : (obj -> JS.Promise<FallbackHookResult>) =
    let baseHandler = createHandler ompEventTranslator runtime configLookup (ompActionExecutor sessionApi)
    fun (rawEvent: obj) ->
        promise {
            let sessionID = ompEventTranslator.ExtractSessionID rawEvent
            let! result = baseHandler rawEvent
            setConsumedFromResult runtime sessionID result
            clearConsumedOnLifecycle runtime sessionID rawEvent
            return result
        }

let createOmpFallbackHandlerLegacy
    (runtime       : FallbackRuntimeState)
    (configLookup  : ConfigLookup)
    (sessionApi    : obj)
    : (obj -> JS.Promise<FallbackHookResult>) =
    createHandler ompEventTranslator runtime configLookup (ompActionExecutor sessionApi)
