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
open Wanxiangshu.Kernel.ReviewReplayPolicy

type NudgeRuntimeState =
    { lastSentActions: Map<string, NudgeAction>
      lastSentMessages: Map<string, string>
      retryPendingSessions: Set<string> }

let emptyRuntimeState = { lastSentActions = Map.empty; lastSentMessages = Map.empty; retryPendingSessions = Set.empty }

let runNudgeFlowCore
    (runtimeState: NudgeRuntimeState)
    (sessionKey: string)
    (takeSnapshot: unit -> JS.Promise<SessionSnapshot option>)
    (sendNudge: string -> string option -> JS.Promise<SendOutcome>)
    : JS.Promise<NudgeRuntimeState> =
    promise {
        match! takeSnapshot () with
        | None -> return runtimeState
        | Some snapshot ->
            let lastSent = Map.tryFind sessionKey runtimeState.lastSentActions
            let lastSentMsg = Map.tryFind sessionKey runtimeState.lastSentMessages
            match deriveAction snapshot lastSent lastSentMsg with
            | NudgeNone -> return runtimeState
            | action ->
                match selectNudgePrompt action with
                | None -> return runtimeState
                | Some promptText ->
                    let! outcome = sendNudge promptText snapshot.agentFromMessage
                    let nextLastSent =
                        match outcome with
                        | Delivered | Aborted -> Some action
                        | Busy | Failed -> lastSent
                    let nextLastMsg =
                        match outcome with
                        | Delivered | Aborted -> Some snapshot.lastAssistantMessage
                        | Busy | Failed -> lastSentMsg
                    return
                        { runtimeState with
                            lastSentActions =
                                Map.add sessionKey (defaultArg nextLastSent NudgeNone) runtimeState.lastSentActions
                            lastSentMessages =
                                Map.add sessionKey (defaultArg nextLastMsg "") runtimeState.lastSentMessages }
    }

type NudgeRuntimeEvent =
    | Ignore
    | StreamEnd of workspaceId: string * stopReason: string * lastAssistantMessage: string
    | StreamAbort of workspaceId: string
    | AbortedError of workspaceId: string
    | StepFailed of workspaceId: string
    | Prompted of workspaceId: string

type NudgeRuntime
    (getChatHistory: (string -> JS.Promise<obj array>) option) =

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
                | _ -> None)

    let collectSnapshotMux (helpers: obj) (workspaceId: string) () : JS.Promise<SessionSnapshot option> =
        promise {
            let! todos = tryGetTodos helpers workspaceId
            match getChatHistory with
            | None ->
                return Some (deriveSnapshot {
                    tailTexts = []
                    openTodos = todos
                    lastAssistantText = ""
                    agentFromMessage = None
                    isLoopActive = false
                    lastAssistantIsCompaction = false
                    hasActiveRunner = false
                })
            | Some getHistory ->
                try
                    let! messages = getHistory workspaceId
                    let lastAssistantIdx =
                        messages
                        |> Array.tryFindIndexBack (fun m ->
                            let role = Dyn.str m "role"
                            role = "assistant" && not (isSyntheticAssistantAgent (Dyn.str m "agent")))
                    let lastAssistantText, tailTexts, agent, isLoopActive =
                        match lastAssistantIdx with
                        | None -> "", [], None, false
                        | Some idx ->
                            let assistantMsg = messages.[idx]
                            let text = messageTexts assistantMsg |> String.concat "\n"
                            let info = Dyn.get assistantMsg "info"
                            let agentVal = Dyn.str info "agent"
                            let agent = if agentVal = "" then None else Some agentVal
                            let afterTexts =
                                messages.[idx + 1 ..]
                                |> Array.toList
                                |> List.collect messageTexts
                            let allTexts =
                                messages
                                |> Array.toList
                                |> List.collect messageTexts
                            let loopActive = allTexts |> reviewTaskFromTexts |> Option.isSome
                            text, afterTexts, agent, loopActive
                    return Some (deriveSnapshot {
                        tailTexts = tailTexts
                        openTodos = todos
                        lastAssistantText = lastAssistantText
                        agentFromMessage = agent
                        isLoopActive = isLoopActive
                        lastAssistantIsCompaction = false
                        hasActiveRunner = false
                    })
                with _ ->
                    return Some (deriveSnapshot {
                        tailTexts = []
                        openTodos = todos
                        lastAssistantText = ""
                        agentFromMessage = None
                        isLoopActive = false
                        lastAssistantIsCompaction = false
                        hasActiveRunner = false
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
        if Set.contains sessionKey runtimeState.retryPendingSessions then
            Promise.lift runtimeState
        else
            runNudgeFlowCore runtimeState sessionKey takeSnapshot sendNudge

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
                if errorType = "unknown" || errorType = "aborted" then
                    StepFailed (getSessionID envelope.EventType props)
                else Ignore
            | "session.next.prompted" ->
                Prompted (getSessionID envelope.EventType envelope.Props)
            | _ -> Ignore

    member _.HandleEvent(parsed: NudgeRuntimeEvent, helpers: obj) : JS.Promise<unit> =
        promise {
            match parsed with
            | Ignore -> return ()
            | StreamEnd(workspaceId, stopReason, _) ->
                if not (Dyn.isNullish helpers) && stopReason <> "queued-message" then
                    let! newState = runNudgeFlowWithRetryCheck runtimeState workspaceId (collectSnapshotMux helpers workspaceId) (sendNudgeMux helpers workspaceId)
                    runtimeState <- newState
                return ()
            | StreamAbort workspaceId
            | AbortedError workspaceId ->
                runtimeState <- { runtimeState with lastSentActions = Map.remove workspaceId runtimeState.lastSentActions; lastSentMessages = Map.remove workspaceId runtimeState.lastSentMessages; retryPendingSessions = Set.remove workspaceId runtimeState.retryPendingSessions }
                return ()
            | StepFailed workspaceId ->
                runtimeState <- { runtimeState with retryPendingSessions = Set.add workspaceId runtimeState.retryPendingSessions }
                return ()
            | Prompted workspaceId ->
                runtimeState <- { runtimeState with retryPendingSessions = Set.remove workspaceId runtimeState.retryPendingSessions }
                return ()
        }

let createNudgeRuntime
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    : NudgeRuntime =
    NudgeRuntime(getChatHistory)
