module Wanxiangshu.Runtime.MuxNudgeEventParse

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Runtime.NudgeRuntimeEvent
open Wanxiangshu.Runtime.NudgeModelResolver
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Kernel.HostTools

let messageTexts (message: obj) : string list =
    let parts = Dyn.get message "parts"

    if not (Dyn.isArray parts) then
        []
    else
        (parts :?> obj array)
        |> Array.toList
        |> List.choose (fun part ->
            match Dyn.str part "type" with
            | "text" ->
                let t = Dyn.get part "text"
                if Dyn.isNullish t then None else Some(string t)
            | "tool"
            | "dynamic-tool" ->
                let output =
                    let direct = Dyn.get part "output"

                    if not (Dyn.isNullish direct) then
                        string direct
                    else
                        let state = Dyn.get part "state"

                        if Dyn.isNullish state then
                            ""
                        else
                            string (Dyn.get state "output")

                if output = "" then None else Some output
            | _ -> None)

let parseAssistantMessageInfo (assistantMsg: obj) : string option * string * string option =
    let info = Dyn.get assistantMsg "info"
    let agentVal = Dyn.str info "agent"
    let agent = if agentVal = "" then None else Some agentVal
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
            let variant = Dyn.str modelVal "variant"
            let suffix = if variant <> "" then ":" + variant else ""

            if providerID = "" || modelID = "" then
                None
            else
                Some(sprintf "%s/%s%s" providerID modelID suffix)
    agent, tid, model

let tryGetLastAssistantDetails
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    (fallbackRuntime: FallbackRuntimeStore)
    (workspaceId: string)
    (lastMsgFromEvent: string)
    : JS.Promise<string * string option * string * string option> =
    promise {
        match getChatHistory with
        | None -> return lastMsgFromEvent, None, "", None
        | Some getHistory ->
            try
                let! messages = getHistory workspaceId

                let lastAssistantIdx =
                    messages
                    |> Array.tryFindIndexBack (fun m ->
                        let role = Dyn.str m "role"
                        role = "assistant" && not (isSyntheticAssistantAgent (Dyn.str m "agent")))

                match lastAssistantIdx with
                | None -> return lastMsgFromEvent, None, "", None
                | Some idx ->
                    let assistantMsg = messages.[idx]
                    let text = messageTexts assistantMsg |> String.concat "\n"
                    let agent, tid, model = parseAssistantMessageInfo assistantMsg
                    let resolvedModel = resolveNudgeModel messages fallbackRuntime workspaceId model
                    let finalText = if text = "" then lastMsgFromEvent else text
                    return finalText, agent, tid, resolvedModel
            with _ ->
                return lastMsgFromEvent, None, "", None
    }

let parseEvent (input: obj) : NudgeRuntimeEvent =
    match decodeHostEventEnvelope input with
    | None -> Ignore
    | Some envelope ->
        match envelope.EventType with
        | "stream-end" ->
            let props = envelope.Props
            let wsId = getSessionID envelope.EventType props
            let stopReason = Dyn.str props "stopReason"

            let lastMsg =
                let parts = Dyn.get props "parts"

                if Dyn.isArray parts then
                    (parts :?> obj array)
                    |> Array.choose (fun p ->
                        if Dyn.str p "type" = "text" then
                            Some(string (Dyn.get p "text"))
                        else
                            None)
                    |> String.concat "\n"
                else
                    ""

            StreamEnd(wsId, stopReason, lastMsg)
        | "stream-abort" -> StreamAbort(getSessionID envelope.EventType envelope.Props)
        | "session.idle" -> StreamEnd(getSessionID envelope.EventType envelope.Props, "", "")
        | "session.next.step.failed" ->
            let props = envelope.Props
            let errorObj = Dyn.get props "error"

            let errorType =
                if Dyn.isNullish errorObj then
                    ""
                else
                    Dyn.str errorObj "type"

            if errorType = "aborted" then
                AbortedError(getSessionID envelope.EventType props)
            elif errorType = "unknown" then
                StepFailed(getSessionID envelope.EventType props)
            else
                Ignore
        | "session.next.prompted" -> Prompted(getSessionID envelope.EventType envelope.Props)
        | _ -> Ignore
