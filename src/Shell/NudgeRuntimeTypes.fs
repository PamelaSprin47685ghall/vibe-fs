module Wanxiangshu.Shell.NudgeRuntimeTypes

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Nudge.TodoStatus

type NudgeRuntimeState =
    { retryPendingSessions: Set<string>
      forceStoppedSessions: Set<string> }

let emptyRuntimeState =
    { retryPendingSessions = Set.empty
      forceStoppedSessions = Set.empty }

let isNudgePromptText (text: string) : bool =
    let t = text.Trim()
    t.Contains("There are still incomplete todos")
    || t.Contains("You are in loop mode. You must call the submit_review")
    || t.Contains("A background runner task is still active")
    || t.Contains("the system context is about to be suspended")
    || t.Contains("You must immediately force an emergency stop")

let isContinuePromptText (text: string) : bool =
    let t = text.Trim()
    t = "continue"

let private messageTexts (message: obj) : string list =
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
                        if Dyn.isNullish state then "" else string (Dyn.get state "output")
                if output = "" then None else Some output
            | _ -> None)

let classifyUserMessage (msg: obj) : string =
    let text = messageTexts msg |> String.concat "\n"
    if isContinuePromptText text then
        "continue"
    elif isNudgePromptText text then
        "nudge"
    else
        "user"

let tryGetModelStringFromMessage (msg: obj) : string option =
    let info = Dyn.get msg "info"
    if isNull info || Dyn.isNullish info then
        None
    else
        let modelVal = Dyn.get info "model"
        if isNull modelVal || Dyn.isNullish modelVal then
            None
        else
            if Dyn.typeIs modelVal "string" then
                let s = string modelVal
                if s = "" then None else Some s
            else
                let providerID = Dyn.str modelVal "providerID"
                let modelID = Dyn.str modelVal "modelID"
                if providerID = "" || modelID = "" then
                    let idVal = Dyn.str modelVal "id"
                    if idVal <> "" then Some idVal else None
                else
                    Some(sprintf "%s/%s" providerID modelID)

let resolveNudgeModel
    (msgs: obj array)
    (fallbackRuntime: FallbackRuntimeState)
    (sessionID: string)
    (lastAssistantModel: string option)
    : string option =
    if isNull msgs || msgs.Length = 0 then
        lastAssistantModel
    else
        let lastAssistantModelFromMsg =
            msgs
            |> Array.tryFindBack (fun msg ->
                let role = Dyn.str msg "role"
                let info = Dyn.get msg "info"
                let msgRole = if not (Dyn.isNullish info) then Dyn.str info "role" else ""
                (role = "assistant" || msgRole = "assistant")
                && not (isSyntheticAssistantAgent (Dyn.str info "agent")))
            |> Option.bind tryGetModelStringFromMessage

        let defaultModel = lastAssistantModelFromMsg |> Option.orElse lastAssistantModel

        let lastUserMsgOpt =
            msgs
            |> Array.tryFindBack (fun msg ->
                let role = Dyn.str msg "role"
                let info = Dyn.get msg "info"
                let msgRole = if not (Dyn.isNullish info) then Dyn.str info "role" else ""
                role = "user" || msgRole = "user")

        match lastUserMsgOpt with
        | None -> defaultModel
        | Some lastUserMsg ->
            match classifyUserMessage lastUserMsg with
            | "user" ->
                match tryGetModelStringFromMessage lastUserMsg with
                | Some m -> Some m
                | None -> defaultModel
            | "nudge" ->
                match tryGetModelStringFromMessage lastUserMsg with
                | Some m -> Some m
                | None -> defaultModel
            | "continue" ->
                match fallbackRuntime.GetModel(sessionID) with
                | Some m -> Some(sprintf "%s/%s" m.ProviderID m.ModelID)
                | None -> None
            | _ -> defaultModel

let runNudgeFlowCore
    (host: Host)
    (workspaceRoot: string)
    (runtimeState: NudgeRuntimeState)
    (sessionKey: string)
    (takeSnapshot: unit -> JS.Promise<SessionSnapshot option>)
    (sendNudge: string -> string option -> string option -> JS.Promise<SendOutcome>)
    : JS.Promise<NudgeRuntimeState> =
    promise {
        match! takeSnapshot () with
        | None -> return runtimeState
        | Some snapshot ->
            match deriveAction snapshot with
            | NudgeNone -> return runtimeState
            | action ->
                match selectNudgePrompt host action snapshot with
                | None -> return runtimeState
                | Some promptText ->
                    let! claimed =
                        promise {
                            try
                                return! tryClaimNudgeDispatch workspaceRoot sessionKey action snapshot.nudgeAnchorKey
                            with _ ->
                                return false
                        }

                    if not claimed then
                        return runtimeState
                    else
                        let! _ = sendNudge promptText snapshot.agentFromMessage snapshot.modelFromMessage
                        return runtimeState
    }

type NudgeRuntimeEvent =
    | Ignore
    | StreamEnd of workspaceId: string * stopReason: string * lastAssistantMessage: string
    | StreamAbort of workspaceId: string
    | AbortedError of workspaceId: string
    | StepFailed of workspaceId: string
    | Prompted of workspaceId: string
