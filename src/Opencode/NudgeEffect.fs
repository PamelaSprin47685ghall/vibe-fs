module Wanxiangshu.Opencode.NudgeEffect

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.Nudge.SubmitReviewHooks
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.EventLog.Fold
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodecCommon
open Wanxiangshu.Shell.OpencodeSessionEventCodec
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Shell.NudgeRuntime

let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> =
    unbox (target?(method)(arg))

let private getPartsText (parts: obj) : string =
    if not (Dyn.isArray parts) then ""
    else
        (parts :?> obj array)
        |> Array.choose (fun part ->
            if Dyn.str part "type" = "text" then
                let text = Dyn.get part "text"
                if Dyn.isNullish text then None else Some (string text)
            else None)
        |> String.concat "\n"

let private messageHasSubmitReviewWipProgress (message: obj) : bool =
    let parts = Dyn.get message "parts"
    if not (Dyn.isArray parts) then false
    else
        (parts :?> obj array)
        |> Array.exists (fun part ->
            let partType = Dyn.str part "type"
            let tool =
                if partType = "tool" then Dyn.str part "tool"
                elif partType = "dynamic-tool" then Dyn.str part "toolName"
                else ""
            (partType = "tool" || partType = "dynamic-tool")
            && isSubmitReviewToolName tool
            && (let direct = Dyn.get part "output"
                if not (Dyn.isNullish direct) then string direct
                else
                    let state = Dyn.get part "state"
                    if Dyn.isNullish state || Dyn.typeIs state "string" then ""
                    else string (Dyn.get state "output"))
               |> isSubmitReviewWipProgressOutput)

let private messageIsUserNudgePrompt (message: obj) : bool =
    let info = Dyn.get message "info"
    let role =
        let r = Dyn.str info "role"
        if r <> "" then r else Dyn.str message "role"
    role = "user" && isNudgePrompt (getPartsText (Dyn.get message "parts"))

let private collectSnapshot (client: obj) (pluginCtx: obj) (sessionID: SessionId)
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
                if not (Dyn.isArray messagesData) then return None
                else
                    let messagesArr = messagesData :?> obj array
                    let openTodos =
                        if not (List.isEmpty openTodosFromApi) then openTodosFromApi
                        else recoverOpenTodosFromMessages messagesData
                    let lastAssistantIdx =
                        messagesArr
                        |> Array.tryFindIndexBack (fun msg ->
                            let info = Dyn.get msg "info"
                            isCompletedAssistantMessage info
                            && not (isSyntheticAssistantAgent (Dyn.str info "agent")))
                    let lastAssistantText, turnId, agentFromMessage =
                        match lastAssistantIdx with
                        | None -> "", "", None
                        | Some idx ->
                            let msg = messagesArr.[idx]
                            let info = Dyn.get msg "info"
                            let agentVal = Dyn.get info "agent"
                            let agent = if Dyn.isNullish agentVal then None else Some (string agentVal)
                            let text = getPartsText (Dyn.get msg "parts")
                            let time = Dyn.get info "time"
                            let completed = Dyn.str time "completed"
                            let tid = if completed <> "" then completed else Dyn.str info "id"
                            text, tid, agent
                    let directory = pluginDirectoryFromCtx pluginCtx
                    let key = nudgeAnchorKey turnId lastAssistantText
                    let! isLoopActive = isLoopActiveFromEventLog directory sessionIDStr
                    let! blocked = nudgeBlockedForTurn directory sessionIDStr key
                    return Some (deriveSnapshot {
                        openTodos = openTodos
                        lastAssistantText = lastAssistantText
                        agentFromMessage = agentFromMessage
                        isLoopActive = isLoopActive
                        lastAssistantIsCompaction = false
                        hasActiveRunner = false
                        nudgeBlockedForTurn = blocked
                        turnId = turnId
                    })
        with _ -> return None
    }

let private sendNudge (client: obj) (sessionID: SessionId) (agentOpt: string option) (promptText: string) : JS.Promise<unit> =
    promise {
        let body = createPromptBody agentOpt promptText
        let promptArg = box {| path = box {| id = Id.sessionIdValue sessionID |}; body = body |}
        match getSessionApiFromClient client with
        | Error _ -> ()
        | Ok session -> do! invoke1 promptArg "prompt" session |> Promise.map ignore
    }

let private sendNudgeOutcome (client: obj) (sessionID: SessionId) (promptText: string) (agentOpt: string option) : JS.Promise<SendOutcome> =
    promise {
        let! caught = sendNudge client sessionID agentOpt promptText |> Promise.result
        return
            match caught with
            | Ok () -> Delivered
            | Error error ->
                match translateJsError error with
                | MessageAborted -> Aborted
                | SessionBusy -> Busy
                | _ -> Failed
    }

let startNudgeFlow (runtimeState: NudgeRuntimeState) (client: obj) (pluginCtx: obj) (sessionID: SessionId)
    : JS.Promise<NudgeRuntimeState> =
    let sid = Id.sessionIdValue sessionID
    let root = pluginDirectoryFromCtx pluginCtx
    runNudgeFlowCore
        root
        runtimeState
        sid
        (fun () -> collectSnapshot client pluginCtx sessionID)
        (fun promptText agentOpt -> sendNudgeOutcome client sessionID promptText agentOpt)

let dispatchPostStopFromHistory (client: obj) (pluginCtx: obj) (sessionID: SessionId) : JS.Promise<unit> =
    promise {
        let! _ = startNudgeFlow emptyRuntimeState client pluginCtx sessionID
        return ()
    }
