module Wanxiangshu.Mux.FallbackHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.PartTypeClassify
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

        member _.IsNewUserMessage(_sessionID, _rawEvent) : bool = false

        member _.ExtractNewUserMessageId(_rawEvent) = None

        member _.ExtractRoutingContext(_rawEvent) = None, None

        member _.IsAssistantMessage(rawEvent: obj) = false
        member _.ExtractAssistantMessageId(rawEvent: obj) = None
        member _.ExtractAssistantParentId(rawEvent: obj) = None

        member _.ExtractContinuationIdentity(rawEvent: obj) =
            let props = Dyn.get rawEvent "properties"
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

        member _.ExtractHostRunId(rawEvent: obj) =
            let props = Dyn.get rawEvent "properties"
            let props = if Dyn.isNullish props then rawEvent else props
            let tid = Dyn.str props "turnId"
            let tid = if tid <> "" then tid else Dyn.str props "turnID"
            let tid = if tid <> "" then tid else Dyn.str props "runId"
            let tid = if tid <> "" then tid else Dyn.str props "runID"
            if tid <> "" then Some tid else None

        member _.ExtractTurnObservation(rawEvent: obj) : TurnObservation option =
            let eventType = Dyn.str rawEvent "type"
            if eventType = "stream-end" then
                let properties = Dyn.get rawEvent "properties"
                let properties = if Dyn.isNullish properties then rawEvent else properties
                let parts = Dyn.get properties "parts"
                let text =
                    if Dyn.isNullish parts || not (Dyn.isArray parts) then
                        ""
                    else
                        (parts :?> obj array)
                        |> Array.filter (fun p -> Dyn.str p "type" = "text")
                        |> Array.map (fun p -> Dyn.str p "text")
                        |> String.concat "\n"
                let hasToolCall =
                    if Dyn.isNullish parts || not (Dyn.isArray parts) then
                        false
                    else
                        (parts :?> obj array)
                        |> Array.exists (fun p -> isToolCallPartType (Dyn.str p "type"))
                let finish = if hasToolCall then ToolFinish else NormalFinish
                Some { TurnId = TurnId.create ""
                       Evidence = { CurrentTurnEvidence.empty with Assistant = AssistantContent(text, Some finish) } }
            else
                None }

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
        member _.SendContinue(sessionID, model, continuationID) =
            let modelStr =
                match model.Variant with
                | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                | None -> sprintf "%s/%s" model.ProviderID model.ModelID

            invokeNudge sessionID ("continue " + modelStr)

        member _.RecoverWithPrompt(sessionID, model, promptText, continuationID) =
            let modelStr =
                match model.Variant with
                | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                | None -> sprintf "%s/%s" model.ProviderID model.ModelID

            invokeNudge sessionID (promptText + " " + modelStr)

        member _.FetchMessages sessionID = getChatHistory sessionID

        member _.PropagateFailure(_sessionID: string) = Promise.lift ()

        member _.CaptureCurrentModel(_sessionID: string) = Promise.lift None

        member _.AbortRun(_sessionID: string) = Promise.lift ()

    }

let createMuxFallbackHandler
    (runtime: FallbackRuntimeState)
    (configLookup: ConfigLookup)
    (helpers: obj)
    (workspaceRoot: string)
    : (obj -> JS.Promise<FallbackHookResult>) =
    createHandler muxEventTranslator runtime configLookup (muxActionExecutor helpers) workspaceRoot None
