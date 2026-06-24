module VibeFs.Opencode.NudgeEventCodec

open Fable.Core
open VibeFs.Shell
open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.NudgeState
open VibeFs.Kernel.PromptFragments
open VibeFs.Shell.Dyn
open VibeFs.Shell.ErrorClassify

let private sessionEventTypes =
    Set.ofList [
        "session.created"
        "session.updated"
        "session.deleted"
        "session.delete"
        "session.close"
        "session.remove"
    ]

let getSessionID (eventType: string) (props: obj) : string =
    let part = Dyn.get props "part"
    let info = Dyn.get props "info"
    let candidates =
        [ Dyn.str props "sessionID"
          Dyn.str part "sessionID"
          Dyn.str info "sessionID"
          if Set.contains eventType sessionEventTypes then
              Dyn.str info "id"
          else "" ]
    candidates |> List.tryFind (fun s -> s <> "") |> Option.defaultValue ""

let getPartsText (parts: obj) : string =
    if not (Dyn.isArray parts) then ""
    else
        (parts :?> obj array)
        |> Array.choose (fun part ->
            if Dyn.str part "type" = "text" then
                let text = Dyn.get part "text"
                if Dyn.isNullish text then None else Some (string text)
            else None)
        |> String.concat "\n"

let isCompletedAssistantMessage (info: obj) : bool =
    if Dyn.isNullish info then false
    else
        let isAssistant = Dyn.str info "role" = "assistant" || Dyn.str info "type" = "assistant"
        let hasError = not (Dyn.isNullish (Dyn.get info "error"))
        if not isAssistant || hasError then false
        else
            let finishVal = Dyn.get info "finish"
            if not (Dyn.isNullish finishVal) && Dyn.typeIs finishVal "string" then
                isTerminalAssistantFinish (string finishVal)
            else
                let timeCompleted = Dyn.get (Dyn.get info "time") "completed"
                not (Dyn.isNullish timeCompleted) && Dyn.typeIs timeCompleted "number"

let decodeTodos (todosData: obj) : string list =
    if Dyn.isArray todosData then
        (todosData :?> obj array)
        |> Array.choose (fun todo ->
            let content = Dyn.str todo "content"
            let status = Dyn.str todo "status"
            if content = "" || status = "" then None
            else
                match todoStatusOfString status with
                | Some s when isTerminal s -> None
                | _ -> Some content)
        |> Array.toList
    else []

let decodeLastAssistant (messagesData: obj) : string * string option * bool =
    if Dyn.isArray messagesData then
        let messagesArr = messagesData :?> obj array
        let lastAssistantIdx =
            messagesArr
            |> Array.tryFindIndexBack (fun msg ->
                let info = Dyn.get msg "info"
                isCompletedAssistantMessage info
                && not (isSyntheticAssistantAgent (Dyn.str info "agent")))
        match lastAssistantIdx with
        | Some idx ->
            let msg = messagesArr.[idx]
            let info = Dyn.get msg "info"
            let agentVal = Dyn.get info "agent"
            let agent = if Dyn.isNullish agentVal then None else Some (string agentVal)
            let text = getPartsText (Dyn.get msg "parts")
            let alreadyNudged =
                messagesArr.[idx + 1 ..]
                |> Array.exists (fun m -> isNudgePrompt (getPartsText (Dyn.get m "parts")))
            text, agent, alreadyNudged
        | None -> "", None, false
    else "", None, false

let createPromptBody (agent: string option) (text: string) : obj =
    match agent with
    | Some a -> box {| agent = a; parts = [| box {| ``type`` = "text"; text = text |} |] |}
    | None -> box {| parts = [| box {| ``type`` = "text"; text = text |} |] |}

let private pureMap =
    Map [
        "stream-abort", StreamAbort
        "session.delete", SessionDeleted
        "session.close", SessionDeleted
        "session.remove", SessionDeleted
        "session.deleted", SessionDeleted
        "session.next.retried", SessionNextRetried
        "session.idle", SessionIdle
    ]

let private extractorMap : Map<string, obj -> NudgeHostEvent option> =
    Map [
        "session.next.prompted", fun props ->
            let prompt = Dyn.get props "prompt"
            let promptText = Dyn.str prompt "text"
            let text =
                if promptText <> "" then promptText
                else
                    let partsText = getPartsText (Dyn.get props "parts")
                    if partsText <> "" then partsText else Dyn.str props "text"
            Some (SessionNextPrompted text)

        "message.updated", fun props ->
            let info = Dyn.get props "info"
            let outcome =
                if isAbortDomainError (Dyn.get info "error") then UpdateAborted
                elif isCompletedAssistantMessage info then UpdateCompletedAssistant
                else UpdateNoChange
            Some (MessageUpdated outcome)

        "message.part.updated", fun props ->
            let part = Dyn.get props "part"
            let partType = Dyn.str part "type"
            let outcome =
                if partType = "retry" then PartRetry
                elif isAbortDomainError (Dyn.get part "error") || isAbortDomainError (Dyn.get part "state") then PartAborted
                elif isRetryProgressPart partType then PartRetryProgress
                else PartOther
            Some (MessagePartUpdated outcome)

        "session.next.step.failed", fun props ->
            Some (SessionNextStepFailed (if isAbortDomainError (Dyn.get props "error") then StepFailAbort else StepFailOther))

        "session.next.tool.failed", fun props ->
            Some (SessionNextToolFailed (if isAbortDomainError (Dyn.get props "error") then ToolFailAbort else ToolFailOther))

        "session.next.step.ended", fun props ->
            let direct = Dyn.str props "finish"
            let finish = if direct <> "" then direct else Dyn.str (Dyn.get props "info") "finish"
            Some (SessionNextStepEnded finish)

        "session.error", fun props ->
            Some (SessionError (if isAbortDomainError (Dyn.get props "error") then SessionErrorAbort else SessionErrorOther))

        "session.status", fun props ->
            let ev =
                match Dyn.str (Dyn.get props "status") "type" with
                | "idle" -> SessionStatusIdle
                | "busy" -> SessionStatusBusy
                | "retry" -> SessionStatusRetry
                | _ -> Other
            Some ev
    ]

let decodeNudgeHostEvent (eventType: string) (props: obj) : NudgeHostEvent =
    match Map.tryFind eventType pureMap with
    | Some ev -> ev
    | None ->
        match Map.tryFind eventType extractorMap with
        | Some extract -> extract props |> Option.defaultValue Other
        | None -> if isRetryProgressEvent eventType then RetryProgress else Other
