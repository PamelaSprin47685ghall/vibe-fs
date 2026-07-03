module Wanxiangshu.Shell.NudgeRuntime

open Fable.Core
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.Nudge.SubmitReviewHooks
open Wanxiangshu.Kernel.NudgeState
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Shell.SerialStateHolder
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodecCommon
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.EventLog.Fold
open Wanxiangshu.Shell.EventLogRuntime

type NudgeRuntimeState =
    { retryPendingSessions: Set<string>
      forceStoppedSessions: Set<string> }

let emptyRuntimeState =
    { retryPendingSessions = Set.empty
      forceStoppedSessions = Set.empty }

let runNudgeFlowCore
    (workspaceRoot: string)
    (runtimeState: NudgeRuntimeState)
    (sessionKey: string)
    (takeSnapshot: unit -> JS.Promise<SessionSnapshot option>)
    (sendNudge: string -> string option -> JS.Promise<SendOutcome>)
    : JS.Promise<NudgeRuntimeState> =
    promise {
        match! takeSnapshot () with
        | None -> return runtimeState
        | Some snapshot ->
            match deriveAction snapshot with
            | NudgeNone -> return runtimeState
            | action ->
                match selectNudgePrompt action snapshot with
                | None -> return runtimeState
                | Some promptText ->
                    let! claimed =
                        promise {
                            try
                                return! tryClaimNudgeDispatch workspaceRoot sessionKey action snapshot.nudgeAnchorKey
                            with _ -> return false
                        }
                    if not claimed then return runtimeState
                    else
                        let! _ = sendNudge promptText snapshot.agentFromMessage
                        return runtimeState
    }

type NudgeRuntimeEvent =
    | Ignore
    | StreamEnd of workspaceId: string * stopReason: string * lastAssistantMessage: string
    | StreamAbort of workspaceId: string
    | AbortedError of workspaceId: string
    | StepFailed of workspaceId: string
    | Prompted of workspaceId: string

[<Global("process")>]
let private nodeProcess : obj = jsNative

type NudgeRuntime
    (getChatHistory: (string -> JS.Promise<obj array>) option,
     workspaceDirectory: string) =

    let mutable runtimeState = emptyRuntimeState

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
            with _ -> return []
        }

    let messageTexts (message: obj) : string list =
        let parts = Dyn.get message "parts"
        if not (Dyn.isArray parts) then []
        else
            (parts :?> obj array)
            |> Array.toList
            |> List.choose (fun part ->
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

    let collectSnapshotMux (helpers: obj) (workspaceId: string) (lastMsgFromEvent: string) () : JS.Promise<SessionSnapshot option> =
        promise {
            let! todos = tryGetTodos helpers workspaceId
            match getChatHistory with
            | None ->
                let root = if workspaceDirectory <> "" then workspaceDirectory else unbox<string> (nodeProcess?cwd())
                let! isLoopActive = isLoopActiveFromEventLog root workspaceId
                let! blocked = nudgeBlockedForTurn root workspaceId lastMsgFromEvent
                return Some (deriveSnapshot {
                    openTodos = todos
                    lastAssistantText = lastMsgFromEvent
                    agentFromMessage = None
                    isLoopActive = isLoopActive
                    lastAssistantIsCompaction = false
                    hasActiveRunner = false
                    nudgeBlockedForTurn = blocked
                    turnId = ""
                })
            | Some getHistory ->
                try
                    let! messages = getHistory workspaceId
                    let lastAssistantIdx =
                        messages
                        |> Array.tryFindIndexBack (fun m ->
                            let role = Dyn.str m "role"
                            role = "assistant" && not (isSyntheticAssistantAgent (Dyn.str m "agent")))
                    let lastAssistantText, turnId, agent =
                        match lastAssistantIdx with
                        | None -> lastMsgFromEvent, "", None
                        | Some idx ->
                            let assistantMsg = messages.[idx]
                            let text = messageTexts assistantMsg |> String.concat "\n"
                            let info = Dyn.get assistantMsg "info"
                            let agentVal = Dyn.str info "agent"
                            let agent = if agentVal = "" then None else Some agentVal
                            let time = Dyn.get info "time"
                            let completed = Dyn.str time "completed"
                            let tid = if completed <> "" then completed else Dyn.str info "id"
                            let finalText = if text = "" then lastMsgFromEvent else text
                            finalText, tid, agent
                    let root = if workspaceDirectory <> "" then workspaceDirectory else unbox<string> (nodeProcess?cwd())
                    let key = nudgeAnchorKey turnId lastAssistantText
                    let! isLoopActive = isLoopActiveFromEventLog root workspaceId
                    let! blocked = nudgeBlockedForTurn root workspaceId key
                    return Some (deriveSnapshot {
                        openTodos = todos
                        lastAssistantText = lastAssistantText
                        agentFromMessage = agent
                        isLoopActive = isLoopActive
                        lastAssistantIsCompaction = false
                        hasActiveRunner = false
                        nudgeBlockedForTurn = blocked
                        turnId = turnId
                    })
                with _ ->
                    let root = if workspaceDirectory <> "" then workspaceDirectory else unbox<string> (nodeProcess?cwd())
                    let! blocked = nudgeBlockedForTurn root workspaceId lastMsgFromEvent
                    return Some (deriveSnapshot {
                        openTodos = todos
                        lastAssistantText = lastMsgFromEvent
                        agentFromMessage = None
                        isLoopActive = false
                        lastAssistantIsCompaction = false
                        hasActiveRunner = false
                        nudgeBlockedForTurn = blocked
                        turnId = ""
                    })
        }

    let sendNudgeMux (helpers: obj) (workspaceId: string) (promptText: string) (_agentOpt: string option) : JS.Promise<SendOutcome> =
        promise {
            try
                let nudgeFn = Dyn.get helpers "nudge"
                let! delivered = unbox<JS.Promise<bool>> (Dyn.call2 nudgeFn workspaceId promptText)
                return if delivered then Delivered else Busy
            with _ -> return Busy
        }

    let runNudgeFlowWithRetryCheck
        (runtimeState: NudgeRuntimeState)
        (sessionKey: string)
        (takeSnapshot: unit -> JS.Promise<SessionSnapshot option>)
        (sendNudge: string -> string option -> JS.Promise<SendOutcome>)
        : JS.Promise<NudgeRuntimeState> =
        if Set.contains sessionKey runtimeState.retryPendingSessions
           || Set.contains sessionKey runtimeState.forceStoppedSessions then
            Promise.lift runtimeState
        else
            let root = if workspaceDirectory <> "" then workspaceDirectory else unbox<string> (nodeProcess?cwd())
            runNudgeFlowCore root runtimeState sessionKey takeSnapshot sendNudge

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
                            if Dyn.str p "type" = "text" then Some (string (Dyn.get p "text"))
                            else None)
                        |> String.concat "\n"
                    else ""
                StreamEnd(wsId, stopReason, lastMsg)
            | "stream-abort" -> StreamAbort (getSessionID envelope.EventType envelope.Props)
            | "session.idle" -> StreamEnd (getSessionID envelope.EventType envelope.Props, "", "")
            | "session.next.step.failed" ->
                let props = envelope.Props
                let errorObj = Dyn.get props "error"
                let errorType = if Dyn.isNullish errorObj then "" else Dyn.str errorObj "type"
                if errorType = "aborted" then
                    AbortedError (getSessionID envelope.EventType props)
                elif errorType = "unknown" then
                    StepFailed (getSessionID envelope.EventType props)
                else Ignore
            | "session.next.prompted" ->
                Prompted (getSessionID envelope.EventType envelope.Props)
            | _ -> Ignore

    member _.HandleEvent(parsed: NudgeRuntimeEvent, helpers: obj) : JS.Promise<unit> =
        promise {
         match parsed with
             | Ignore -> return ()
             | StreamEnd(workspaceId, stopReason, lastMsg) ->
                 if not (Dyn.isNullish helpers) && stopReason <> "queued-message" then
                     let! newState = runNudgeFlowWithRetryCheck runtimeState workspaceId (collectSnapshotMux helpers workspaceId lastMsg) (sendNudgeMux helpers workspaceId)
                     runtimeState <- newState
                 return ()
             | StreamAbort workspaceId
             | AbortedError workspaceId ->
                 runtimeState <-
                     { runtimeState with
                         forceStoppedSessions = Set.add workspaceId runtimeState.forceStoppedSessions }
                 return ()
             | StepFailed workspaceId ->
                 runtimeState <- { runtimeState with retryPendingSessions = Set.add workspaceId runtimeState.retryPendingSessions }
                 return ()
             | Prompted workspaceId ->
                 runtimeState <-
                     { runtimeState with
                         retryPendingSessions = Set.remove workspaceId runtimeState.retryPendingSessions
                         forceStoppedSessions = Set.remove workspaceId runtimeState.forceStoppedSessions }
                 return ()
        }

let createNudgeRuntime
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    (workspaceDirectory: string)
    : NudgeRuntime =
    NudgeRuntime(getChatHistory, workspaceDirectory)
