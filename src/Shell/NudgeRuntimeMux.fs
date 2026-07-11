module Wanxiangshu.Shell.NudgeRuntimeMux

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.Nudge.NudgeSnapshotSource
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodecCommon
open Wanxiangshu.Kernel.EventLog.Fold
open Wanxiangshu.Kernel.EventLog.ReviewLoopFold
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.NudgeRuntimeTypes

[<Global("process")>]
let private nodeProcess: obj = jsNative

let tryGetTodos (helpers: obj) (workspaceId: string) : JS.Promise<string list> =
    promise {
        try
            let getTodosFn = Dyn.get helpers "getTodos"

            if Dyn.typeIs getTodosFn "function" then
                let! result = unbox<JS.Promise<obj>> (Dyn.call1 getTodosFn workspaceId)

                if Dyn.isArray result then
                    return (result :?> obj array) |> Array.map string |> List.ofArray
                else
                    return []
            else
                return []
        with _ ->
            return []
    }

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

let collectSnapshotMux
    (fallbackRuntime: FallbackRuntimeState)
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    (workspaceDirectory: string)
    (helpers: obj)
    (workspaceId: string)
    (lastMsgFromEvent: string)
    ()
    : JS.Promise<SessionSnapshot option> =
    promise {
        let! todos = tryGetTodos helpers workspaceId

        let root =
            if workspaceDirectory <> "" then
                workspaceDirectory
            else
                unbox<string> (nodeProcess?cwd ())

        let! lastAssistantText, agent, turnId, model =
            match getChatHistory with
            | None -> promise { return lastMsgFromEvent, None, "", None }
            | Some getHistory ->
                promise {
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

                                    if providerID = "" || modelID = "" then
                                        None
                                    else
                                        Some(sprintf "%s/%s" providerID modelID)

                            let resolvedModel =
                                Wanxiangshu.Shell.NudgeRuntimeTypes.resolveNudgeModel
                                    messages
                                    fallbackRuntime
                                    workspaceId
                                    model

                            let finalText = if text = "" then lastMsgFromEvent else text
                            return finalText, agent, tid, resolvedModel
                    with _ ->
                        return lastMsgFromEvent, None, "", None
                }

        do! appendAssistantCompletedOrFail root workspaceId lastAssistantText agent model turnId todos

        let! snapshot = getNudgeSnapshotFromEventLog root workspaceId

        let currentAnchor = nudgeAnchorKey snapshot.turnId snapshot.lastAssistantText

        let blockStatus =
            if isNudgeBlockedForAnchor { DispatchedAnchors = snapshot.dispatchedAnchors } currentAnchor then
                NudgeBlockStatus.Blocked
            else
                NudgeBlockStatus.Allowed

        return Some(sessionSnapshotFromFold snapshot RunnerPresence.Absent blockStatus)
    }

let sendNudgeMux
    (fallbackRuntime: FallbackRuntimeState)
    (helpers: obj)
    (workspaceId: string)
    (promptText: string)
    (agentOpt: string option)
    (modelOpt: string option)
    : JS.Promise<SendOutcome> =
    promise {
        try
            let nudgeFn = Dyn.get helpers "nudge"

            let agentVal =
                match agentOpt with
                | Some a -> box a
                | None -> null

            let modelVal =
                match modelOpt with
                | Some m -> box m
                | None -> null

            fallbackRuntime.SetAwaitingBusy workspaceId true
            let! delivered = unbox<JS.Promise<bool>> (Dyn.call4 nudgeFn workspaceId promptText modelVal agentVal)
            return if delivered then Delivered else Busy
        with _ ->
            return Busy
    }

let runNudgeFlowWithRetryCheck
    (workspaceDirectory: string)
    (runtimeState: NudgeRuntimeState)
    (sessionKey: string)
    (takeSnapshot: unit -> JS.Promise<SessionSnapshot option>)
    (sendNudge: string -> string option -> string option -> JS.Promise<SendOutcome>)
    : JS.Promise<NudgeRuntimeState> =
    if
        Set.contains sessionKey runtimeState.retryPendingSessions
        || Set.contains sessionKey runtimeState.forceStoppedSessions
    then
        Promise.lift runtimeState
    else
        let root =
            if workspaceDirectory <> "" then
                workspaceDirectory
            else
                unbox<string> (nodeProcess?cwd ())

        runNudgeFlowCore Mux root runtimeState sessionKey takeSnapshot sendNudge

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
