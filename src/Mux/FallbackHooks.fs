module Wanxiangshu.Mux.FallbackHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackEventBridge
open Wanxiangshu.Shell.FallbackRuntimeState

let private muxErrorInput (props: obj) : ErrorInput =
    let errorName = Dyn.str props "errorType"
    let message = Dyn.str props "errorMessage"

    { ErrorName = errorName
      DomainError = Some(classifyErrorLeaf errorName "" message)
      Message = message
      StatusCode =
        let sc = Dyn.str props "statusCode"
        if sc <> "" then Some(int sc) else None
      IsRetryable =
        let ir = Dyn.str props "isRetryable"
        if ir <> "" then Some(ir = "true") else None }

let muxEventTranslator: IEventTranslator =
    { new IEventTranslator with
        member _.TranslateError(rawEvent: obj) : FallbackEvent option =
            let eventType = Dyn.str rawEvent "type"
            let props = Dyn.get rawEvent "properties"
            let props = if Dyn.isNullish props then rawEvent else props

            if eventType = "error" || eventType = "session.error" then
                let errType = Dyn.str props "errorType"

                if errType = "aborted" then
                    Some(
                        SessionError
                            { ErrorName = "MessageAbortedError"
                              DomainError = Some MessageAborted
                              Message = "aborted"
                              StatusCode = None
                              IsRetryable = None }
                    )
                else
                    Some(SessionError(muxErrorInput props))
            else
                None

        member _.ExtractSessionID(rawEvent: obj) : string =
            let ws = Dyn.str rawEvent "workspaceId"

            if ws <> "" then
                ws
            else
                let props = Dyn.get rawEvent "properties"
                let props = if Dyn.isNullish props then rawEvent else props
                Dyn.str props "sessionID"

        member _.IsSessionError(rawEvent: obj) : bool =
            let t = Dyn.str rawEvent "type"
            t = "error" || t = "session.error"

        member _.IsSessionIdle(rawEvent: obj) : bool = Dyn.str rawEvent "type" = "stream-end"

        member _.IsSessionBusy(rawEvent: obj) : bool = false

        member _.IsNewUserMessage(_sessionID, _rawEvent) : bool = false }

let muxActionExecutor (helpers: obj) : IActionExecutor =
    let invokeNudge (workspaceId: string) (text: string) : JS.Promise<unit> =
        if Dyn.isNullish helpers then
            Promise.lift ()
        else
            let nudge = Dyn.get helpers "nudge"

            if Dyn.isNullish nudge then
                Promise.lift ()
            else
                unbox<JS.Promise<unit>> (Dyn.call2 nudge workspaceId text)

    let getChatHistory (workspaceId: string) : JS.Promise<obj array> =
        if Dyn.isNullish helpers then
            Promise.lift [||]
        else
            let getter = Dyn.get helpers "getChatHistory"

            if Dyn.isNullish getter then
                Promise.lift [||]
            else
                unbox<JS.Promise<obj array>> (Dyn.call1 getter workspaceId)

    { new IActionExecutor with
        member _.SendContinue(sessionID, model) =
            let modelStr =
                match model.Variant with
                | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                | None -> sprintf "%s/%s" model.ProviderID model.ModelID

            invokeNudge sessionID ("continue " + modelStr)

        member _.RecoverWithPrompt(sessionID, model, promptText) =
            let modelStr =
                match model.Variant with
                | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                | None -> sprintf "%s/%s" model.ProviderID model.ModelID

            invokeNudge sessionID (promptText + " " + modelStr)

        member _.FetchMessages sessionID = getChatHistory sessionID

        member _.PropagateFailure(_sessionID: string) = Promise.lift ()

        member _.CaptureCurrentModel(_sessionID: string) = Promise.lift None

    }

let createMuxFallbackHandler
    (runtime: FallbackRuntimeState)
    (configLookup: ConfigLookup)
    (helpers: obj)
    (workspaceRoot: string)
    : (obj -> JS.Promise<FallbackHookResult>) =
    createHandler muxEventTranslator runtime configLookup (muxActionExecutor helpers) workspaceRoot None
