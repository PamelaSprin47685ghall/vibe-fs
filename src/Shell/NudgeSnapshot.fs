module Wanxiangshu.Shell.NudgeSnapshot

open Fable.Core
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge.SubmitReviewHooks
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.ReviewReplayPolicy

let private tryGetTodos (helpers: obj) (workspaceId: string) : JS.Promise<string list> =
    promise {
        try
            let getTodosFn = Dyn.get helpers "getTodos"
            let! result = unbox<JS.Promise<obj>> (Dyn.call1 getTodosFn workspaceId)
            if Dyn.isArray result then
                return (result :?> obj array) |> Array.map string |> List.ofArray
            else
                return []
        with ex ->
            return []
    }

let private tryGetChatHistory
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    (workspaceId: string)
    : JS.Promise<obj array> =
    promise {
        match getChatHistory with
        | None -> return [||]
        | Some getHistory ->
            try
                return! getHistory workspaceId
            with ex ->
                return [||]
    }

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
            Dyn.str part "type" = "dynamic-tool"
            && isSubmitReviewToolName (Dyn.str part "toolName")
            && (let direct = Dyn.get part "output"
                if not (Dyn.isNullish direct) then string direct
                else
                    let state = Dyn.get part "state"
                    if Dyn.isNullish state || Dyn.typeIs state "string" then ""
                    else string (Dyn.get state "output"))
               |> isSubmitReviewWipProgressOutput)

let private messageIsUserNudgePrompt (message: obj) : bool =
    Dyn.str message "role" = "user" && isNudgePrompt (getPartsText (Dyn.get message "parts"))

let private historyText (message: obj) : string list =
    let parts = Dyn.get message "parts"
    if not (Dyn.isArray parts) then []
    else
        (parts :?> obj array)
        |> Array.choose (fun part ->
            match Dyn.str part "type" with
            | "text" ->
                let text = Dyn.get part "text"
                if Dyn.isNullish text then None else Some (string text)
            | "tool"
            | "dynamic-tool" ->
                let output =
                    let direct = Dyn.get part "output"
                    if not (Dyn.isNullish direct) then string direct
                    else
                        let state = Dyn.get part "state"
                        if Dyn.isNullish state then "" else string (Dyn.get state "output")
                if output = "" then None else Some output
            | _ -> None)
        |> Array.toList

let private alreadyNudgedAfterIndex (messages: obj array) (index: int) : bool =
    messages.[index + 1 ..]
    |> Array.fold
        (fun nudged message ->
            if messageHasSubmitReviewWipProgress message then false
            elif messageIsUserNudgePrompt message then true
            else nudged)
        false

let private decodeLastAssistant (messages: obj array) : string * bool * string option =
    let lastAssistantIndex =
        messages
        |> Array.tryFindIndexBack (fun message ->
            Dyn.str message "role" = "assistant"
            && not (isSyntheticAssistantAgent (Dyn.str message "agent")))

    match lastAssistantIndex with
    | None -> "", false, None
    | Some index ->
        let text = getPartsText (Dyn.get messages.[index] "parts")
        let alreadyNudged = alreadyNudgedAfterIndex messages index
        let info = Dyn.get messages.[index] "info"
        let agentVal = Dyn.str info "agent"
        let agent = if agentVal = "" then None else Some agentVal
        text, alreadyNudged, agent

let collectSnapshot
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    (helpers: obj)
    (workspaceId: string)
    (eventLastAssistantMessage: string)
    : JS.Promise<SessionSnapshot> =
    promise {
        let! todos = tryGetTodos helpers workspaceId
        let! history = tryGetChatHistory getChatHistory workspaceId
        let historyLastAssistantMessage, historyAlreadyNudged, historyAgentFromMessage =
            decodeLastAssistant history
        let historyLoopActive =
            history
            |> Array.toList
            |> List.collect historyText
            |> reviewTaskFromTexts
            |> Option.isSome
        let effectiveLastAssistantMessage, alreadyNudged =
            if historyLastAssistantMessage = "" then
                eventLastAssistantMessage, false
            elif eventLastAssistantMessage <> "" && eventLastAssistantMessage <> historyLastAssistantMessage then
                eventLastAssistantMessage, false
            else
                historyLastAssistantMessage, historyAlreadyNudged

        return
            { todos = todos
              lastAssistantMessage = effectiveLastAssistantMessage
              isLoopActive = historyLoopActive
              alreadyNudged = alreadyNudged
              agentFromMessage = historyAgentFromMessage
              lastAssistantIsCompaction = false
              anchorPromptIssued = false }
    }
