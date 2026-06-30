module Wanxiangshu.Mux.FallbackHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackEventBridge
open Wanxiangshu.Shell.FallbackRuntimeState

let private muxErrorInput (props: obj) : ErrorInput =
    { ErrorName   = Dyn.str props "errorType"
      Message     = Dyn.str props "errorMessage"
      StatusCode  =
          let sc = Dyn.str props "statusCode"
          if sc <> "" then Some (int sc) else None
      IsRetryable =
          let ir = Dyn.str props "isRetryable"
          if ir <> "" then Some (ir = "true") else None }

let muxEventTranslator : IEventTranslator =
    { new IEventTranslator with
        member _.TranslateError (rawEvent: obj) : FallbackEvent option =
            let eventType = Dyn.str rawEvent "type"
            if eventType = "error" then
                let props = Dyn.get rawEvent "properties"
                let errType = Dyn.str props "errorType"
                if errType = "aborted" then
                    Some (SessionError { ErrorName = "MessageAbortedError"
                                         Message = "aborted"; StatusCode = None; IsRetryable = None })
                else
                    Some (SessionError (muxErrorInput props))
            else None

        member _.ExtractSessionID (rawEvent: obj) : string =
            Dyn.str rawEvent "workspaceId"

        member _.IsSessionError (rawEvent: obj) : bool =
            Dyn.str rawEvent "type" = "error"

        member _.IsSessionIdle (rawEvent: obj) : bool =
            Dyn.str rawEvent "type" = "stream-end"

        member _.IsSessionBusy (rawEvent: obj) : bool = false

        member _.IsNewUserMessage (_rawEvent: obj) : bool = false }

let muxActionExecutor (helpers: obj) : IActionExecutor =
    let invokeNudge (workspaceId: string) (text: string) : JS.Promise<unit> =
        if Dyn.isNullish helpers then Promise.lift ()
        else
            let nudge = Dyn.get helpers "nudge"
            if Dyn.isNullish nudge then Promise.lift ()
            else unbox<JS.Promise<unit>> (nudge?(workspaceId, text))

    let getChatHistory (workspaceId: string) : JS.Promise<obj array> =
        if Dyn.isNullish helpers then Promise.lift [||]
        else
            let getter = Dyn.get helpers "getChatHistory"
            if Dyn.isNullish getter then Promise.lift [||]
            else unbox<JS.Promise<obj array>> (getter?(workspaceId))

    { new IActionExecutor with
        member _.SendContinue (sessionID, model) =
            let modelStr =
                match model.Variant with
                | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                | None -> sprintf "%s/%s" model.ProviderID model.ModelID
            invokeNudge sessionID ("continue " + modelStr)

        member _.AbortSession (sessionID) =
            if Dyn.isNullish helpers then Promise.lift ()
            else
                let abort = Dyn.get helpers "abort"
                if Dyn.isNullish abort then Promise.lift ()
                else unbox<JS.Promise<unit>> (abort?(sessionID))

        member _.FetchMessages sessionID = getChatHistory sessionID

        member _.PropagateFailure (_sessionID: string) = Promise.lift ()

        member _.CaptureCurrentModel (_sessionID: string) = Promise.lift None }

let createMuxFallbackHandler
    (runtime       : FallbackRuntimeState)
    (configLookup  : ConfigLookup)
    (helpers       : obj)
    : (obj -> JS.Promise<FallbackHookResult>) =
    createHandler muxEventTranslator runtime configLookup (muxActionExecutor helpers)
