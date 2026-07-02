module Wanxiangshu.Opencode.NudgeEffect

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.Nudge.SubmitReviewHooks
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.ReviewReplayPolicy
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

let private collectSnapshot (client: obj) (sessionID: SessionId)
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
                    let lastAssistantText, agentFromMessage, tailTexts =
                        match lastAssistantIdx with
                        | None -> "", None, []
                        | Some idx ->
                            let msg = messagesArr.[idx]
                            let info = Dyn.get msg "info"
                            let agentVal = Dyn.get info "agent"
                            let agent = if Dyn.isNullish agentVal then None else Some (string agentVal)
                            let text = getPartsText (Dyn.get msg "parts")
                            let tail =
                                messagesArr.[idx + 1 ..]
                                |> Array.toList
                                |> List.map (fun message ->
                                    let parts = Dyn.get message "parts"
                                    if not (Dyn.isArray parts) then ""
                                    else
                                        (parts :?> obj array)
                                        |> Array.choose (fun part ->
                                            match Dyn.str part "type" with
                                            | "text" ->
                                                let t = Dyn.get part "text"
                                                if Dyn.isNullish t then None else Some (string t)
                                            | "tool" | "dynamic-tool" ->
                                                let output =
                                                    let direct = Dyn.get part "output"
                                                    if not (Dyn.isNullish direct) then string direct
                                                    else
                                                        let state = Dyn.get part "state"
                                                        if Dyn.isNullish state then "" else string (Dyn.get state "output")
                                                if output = "" then None else Some output
                                            | _ -> None)
                                        |> String.concat "\n")
                                |> List.filter (fun s -> s <> "")
                            text, agent, tail
                    let historyTexts =
                        Wanxiangshu.Opencode.MessagingCodec.decodeMessages messagesArr
                        |> Wanxiangshu.Kernel.Messaging.flatten
                        |> textsFromFlatParts
                        |> Seq.toList
                    let isLoopActive = historyTexts |> reviewTaskFromTexts |> Option.isSome
                    return Some (deriveSnapshot {
                        tailTexts = tailTexts
                        openTodos = openTodos
                        lastAssistantText = lastAssistantText
                        agentFromMessage = agentFromMessage
                        isLoopActive = isLoopActive
                        lastAssistantIsCompaction = false
                        hasActiveRunner = false
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

let startNudgeFlow (runtimeState: NudgeRuntimeState) (client: obj) (sessionID: SessionId)
    : JS.Promise<NudgeRuntimeState> =
    let sid = Id.sessionIdValue sessionID
    runNudgeFlowCore
        runtimeState
        sid
        (fun () -> collectSnapshot client sessionID)
        (fun promptText agentOpt -> sendNudgeOutcome client sessionID promptText agentOpt)

let dispatchPostStopFromHistory (client: obj) (sessionID: SessionId) : JS.Promise<unit> =
    promise {
        let! _ = startNudgeFlow emptyRuntimeState client sessionID
        return ()
    }
