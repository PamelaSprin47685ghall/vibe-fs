module Wanxiangshu.Opencode.NudgeEffect

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.EventLog.Fold
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodec
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.NudgeRuntime
open Wanxiangshu.Shell.FallbackRuntimeState

let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = unbox (target?(method) (arg))

let private getPartsText (parts: obj) : string =
    if not (Dyn.isArray parts) then
        ""
    else
        (parts :?> obj array)
        |> Array.choose (fun part ->
            if Dyn.str part "type" = "text" then
                let text = Dyn.get part "text"
                if Dyn.isNullish text then None else Some(string text)
            else
                None)
        |> String.concat "\n"

let private collectSnapshot
    (fallbackRuntime: FallbackRuntimeState)
    (client: obj)
    (pluginCtx: obj)
    (sessionID: SessionId)
    : JS.Promise<SessionSnapshot option> =
    promise {
        try
            let sessionIDStr = Id.sessionIdValue sessionID

            match getSessionApiFromClient client with
            | Error _ -> return None
            | Ok session ->
                let! todoResp = invoke1 (box {| path = {| id = sessionIDStr |} |}) "todo" session
                let openTodosFromApi = decodeTodos (Dyn.get todoResp "data")
                let! messagesResp = invoke1 (box {| path = {| id = sessionIDStr |} |}) "messages" session
                let messagesData = Dyn.get messagesResp "data"

                if not (Dyn.isArray messagesData) then
                    return None
                else
                    let messagesArr = messagesData :?> obj array

                    let openTodos =
                        if not (List.isEmpty openTodosFromApi) then
                            openTodosFromApi
                        else
                            recoverOpenTodosFromMessages messagesData

                    let shouldSkip = shouldSkipNudge messagesData

                    if shouldSkip then
                        return None
                    else
                        let lastAssistantIdx =
                            messagesArr
                            |> Array.tryFindIndexBack (fun msg ->
                                let info = Dyn.get msg "info"

                                isCompletedAssistantMessage info
                                && not (isSyntheticAssistantAgent (Dyn.str info "agent")))

                        let lastAssistantText, turnId, agentFromMessage, modelFromMessage =
                            match lastAssistantIdx with
                            | None -> "", "", None, None
                            | Some idx ->
                                let msg = messagesArr.[idx]
                                let info = Dyn.get msg "info"
                                let agentVal = Dyn.get info "agent"

                                let agent =
                                    if Dyn.isNullish agentVal then
                                        None
                                    else
                                        Some(string agentVal)

                                let text = getPartsText (Dyn.get msg "parts")
                                let time = Dyn.get info "time"
                                let completed = Dyn.str time "completed"
                                let tid = if completed <> "" then completed else Dyn.str info "id"
                                let modelVal = Dyn.get info "model"

                                let model =
                                    if Dyn.isNullish modelVal then
                                        None
                                    else
                                        let providerID = Dyn.str modelVal "providerID"
                                        let modelID = Dyn.str modelVal "modelID"

                                        if providerID = "" || modelID = "" then
                                            None
                                        else
                                            Some(sprintf "%s/%s" providerID modelID)

                                let resolvedModel =
                                    Wanxiangshu.Shell.NudgeRuntimeTypes.resolveNudgeModel
                                        messagesArr
                                        fallbackRuntime
                                        sessionIDStr
                                        model

                                text, tid, agent, resolvedModel

                        let directory = pluginDirectoryFromCtx pluginCtx

                        do!
                            appendAssistantCompletedOrFail
                                directory
                                sessionIDStr
                                lastAssistantText
                                agentFromMessage
                                modelFromMessage
                                turnId
                                openTodos

                        let! snap = getNudgeSnapshotFromEventLog directory sessionIDStr
                        let key = nudgeAnchorKey snap.turnId snap.lastAssistantText
                        let blocked = Set.contains (key.Trim()) snap.dispatchedAnchors

                        return
                            Some
                                { todos = snap.openTodos
                                  lastAssistantMessage = snap.lastAssistantText
                                  isLoopActive = snap.isLoopActive
                                  nudgeBlockedForTurn = blocked
                                  nudgeAnchorKey = key
                                  agentFromMessage = snap.agentFromMessage
                                  modelFromMessage = snap.modelFromMessage
                                  hasActiveRunner = false }
        with _ ->
            return None
    }

let private sendNudge
    (client: obj)
    (sessionID: SessionId)
    (agentOpt: string option)
    (modelOpt: string option)
    (promptText: string)
    : JS.Promise<unit> =
    promise {
        let body = createPromptBodyWithModel agentOpt modelOpt promptText

        let promptArg =
            box
                {| path = box {| id = Id.sessionIdValue sessionID |}
                   body = body |}

        match getSessionApiFromClient client with
        | Error _ -> ()
        | Ok session -> do! invoke1 promptArg "prompt" session |> Promise.map ignore
    }

let private sendNudgeOutcome
    (client: obj)
    (sessionID: SessionId)
    (promptText: string)
    (agentOpt: string option)
    (modelOpt: string option)
    : JS.Promise<SendOutcome> =
    promise {
        let! caught = sendNudge client sessionID agentOpt modelOpt promptText |> Promise.result

        return
            match caught with
            | Ok() -> Delivered
            | Error error ->
                match translateJsError error with
                | MessageAborted -> Aborted
                | SessionBusy -> Busy
                | _ -> Failed
    }

let startNudgeFlow
    (host: Host)
    (fallbackRuntime: FallbackRuntimeState)
    (runtimeState: NudgeRuntimeState)
    (client: obj)
    (pluginCtx: obj)
    (sessionID: SessionId)
    : JS.Promise<NudgeRuntimeState> =
    let sid = Id.sessionIdValue sessionID
    let root = pluginDirectoryFromCtx pluginCtx

    runNudgeFlowCore
        host
        root
        runtimeState
        sid
        (fun () -> collectSnapshot fallbackRuntime client pluginCtx sessionID)
        (fun promptText agentOpt modelOpt -> sendNudgeOutcome client sessionID promptText agentOpt modelOpt)

let dispatchPostStopFromHistory
    (host: Host)
    (fallbackRuntime: FallbackRuntimeState)
    (client: obj)
    (pluginCtx: obj)
    (sessionID: SessionId)
    : JS.Promise<unit> =
    promise {
        let! _ = startNudgeFlow host fallbackRuntime emptyRuntimeState client pluginCtx sessionID
        return ()
    }
