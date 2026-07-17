module Wanxiangshu.Runtime.NudgeRuntimeMux

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Runtime.Nudge.NudgeDerivation
open Wanxiangshu.Kernel.Nudge.NudgeSnapshotSource
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.OpencodeHostEvent
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.Review.ReviewLoopFold
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.GateFlagTransitions
open Wanxiangshu.Runtime.NudgeRuntimeState
open Wanxiangshu.Runtime.NudgeRuntimeEvent
open Wanxiangshu.Runtime.NudgeFlow
open Wanxiangshu.Runtime.NudgeModelResolver

[<Global("globalThis.process")>]
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

let getRootDirectory (workspaceDirectory: string) : string =
    if workspaceDirectory <> "" then
        workspaceDirectory
    else
        unbox<string> (nodeProcess?cwd ())

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

let private getBlockStatus (snapshot: NudgeSnapshotState) (currentAnchor: string) : NudgeBlockStatus =
    let dedup: NudgeDedupState =
        { PendingNudge = snapshot.pendingNudge
          LastDispatchedAnchor = snapshot.lastDispatchedAnchor }
    if NudgeProjection.isBlocked dedup currentAnchor then
        NudgeBlockStatus.Blocked
    else
        NudgeBlockStatus.Allowed

let collectSnapshotMux
    (fallbackRuntime: FallbackRuntimeStore)
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    (workspaceDirectory: string)
    (helpers: obj)
    (workspaceId: string)
    (lastMsgFromEvent: string)
    ()
    : JS.Promise<SessionSnapshot option> =
    promise {
        let! todos = tryGetTodos helpers workspaceId

        let root = getRootDirectory workspaceDirectory

        let! lastAssistantText, agent, turnId, model =
            tryGetLastAssistantDetails getChatHistory fallbackRuntime workspaceId lastMsgFromEvent

        do! appendAssistantCompletedOrFail root workspaceId lastAssistantText agent model turnId todos

        let! snapshot = getNudgeSnapshotFromEventLog root workspaceId

        let currentAnchor =
            Wanxiangshu.Kernel.Nudge.NudgeProjection.nudgeAnchorKey snapshot.turnId snapshot.lastAssistantText

        let blockStatus = getBlockStatus snapshot currentAnchor

        return Some(sessionSnapshotFromFold snapshot RunnerPresence.Absent blockStatus)
    }

let sendNudgeMux
    (fallbackRuntime: FallbackRuntimeStore)
    (helpers: obj)
    (workspaceId: string)
    (promptText: string)
    (agentOpt: string option)
    (modelOpt: string option)
    (nudgeId: string)
    (nonce: string)
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

            fallbackRuntime.SetMainContinuationAwaitingStart workspaceId true
            let! delivered = unbox<JS.Promise<bool>> (Dyn.call4 nudgeFn workspaceId promptText modelVal agentVal)
            return if delivered then Delivered else Busy
        with _ ->
            return Busy
    }

let runNudgeFlowWithRetryCheck
    (fallbackRuntime: FallbackRuntimeStore)
    (workspaceDirectory: string)
    (runtimeState: NudgeRuntimeState)
    (sessionKey: string)
    (takeSnapshot: unit -> JS.Promise<SessionSnapshot option>)
    (sendNudge: string -> string option -> string option -> string -> string -> JS.Promise<SendOutcome>)
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

        let abortRun (_: string) = Promise.lift ()

        runNudgeFlowCore Mux root fallbackRuntime runtimeState sessionKey takeSnapshot sendNudge abortRun

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
