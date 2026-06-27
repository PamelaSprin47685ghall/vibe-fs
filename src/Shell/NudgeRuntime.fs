module Wanxiangshu.Shell.NudgeRuntime

open Fable.Core
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.Nudge.SubmitReviewHooks
open Wanxiangshu.Kernel.NudgeState
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Shell.SerialStateHolder

type NudgeRuntimeEvent =
    | Ignore
    | StreamEnd of workspaceId: string * stopReason: string * lastAssistantMessage: string
    | StreamAbort of workspaceId: string
    | AbortedError of workspaceId: string

let private runNudgeFlow
    (holder: StateHolder<NudgeShellState>)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    (helpers: obj)
    (workspaceId: string)
    (lastAssistantMessage: string)
    : JS.Promise<unit> =
    let getSnapshot () : JS.Promise<SessionSnapshot option> =
        promise {
            let! todos =
                promise {
                    try
                        let getTodosFn = Dyn.get helpers "getTodos"
                        let! result = unbox<JS.Promise<obj>> (Dyn.call1 getTodosFn workspaceId)
                        if Dyn.isArray result then
                            return (result :?> obj array) |> Array.map string |> List.ofArray
                        else
                            return []
                    with _ ->
                        return []
                }
            let! history =
                match getChatHistory with
                | None -> return [||]
                | Some getHistory ->
                    promise {
                        try
                            return! getHistory workspaceId
                        with _ ->
                            return [||]
                    }
            let lastAssistantIndex =
                history
                |> Array.tryFindIndexBack (fun message ->
                    Dyn.str message "role" = "assistant"
                    && not (isSyntheticAssistantAgent (Dyn.str message "agent")))
            let historyLastAssistantMessage, historyAlreadyNudged =
                match lastAssistantIndex with
                | None -> "", false
                | Some idx ->
                    let parts = Dyn.get history.[idx] "parts"
                    let text =
                        if not (Dyn.isArray parts) then ""
                        else
                            (parts :?> obj array)
                            |> Array.choose (fun part ->
                                if Dyn.str part "type" = "text" then
                                    let t = Dyn.get part "text"
                                    if Dyn.isNullish t then None else Some (string t)
                                else None)
                            |> String.concat "\n"
                    let already =
                        history.[idx + 1 ..]
                        |> Array.fold (fun nudged msg ->
                            if Dyn.str msg "role" = "user" && isNudgePrompt text then true
                            else nudged) false
                    text, already
            let effectiveMsg, alreadyNudged =
                if historyLastAssistantMessage = "" then lastAssistantMessage, false
                elif lastAssistantMessage <> "" && lastAssistantMessage <> historyLastAssistantMessage then lastAssistantMessage, false
                else historyLastAssistantMessage, historyAlreadyNudged
            return Some { todos = todos; lastAssistantMessage = effectiveMsg; alreadyNudged = alreadyNudged; agentFromMessage = None }
        }
    let attemptSend (promptText: string) (_agentOpt: string option) : JS.Promise<SendOutcome> =
        promise {
            try
                let nudgeFn = Dyn.get helpers "nudge"
                let! delivered = unbox<JS.Promise<bool>> (Dyn.call2 nudgeFn workspaceId promptText)
                return if delivered then Delivered else Busy
            with _ ->
                return Busy
        }
    runNudgeFlowCore holder reviewStore (fun _ -> None) getSnapshot attemptSend workspaceId

let runNudgeFlowCore
    (holder: StateHolder<NudgeShellState>)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (lookupChildAgent: string -> string option)
    (getSnapshot: unit -> JS.Promise<SessionSnapshot option>)
    (attemptSend: string -> string option -> JS.Promise<SendOutcome>)
    (workspaceId: string)
    : JS.Promise<unit> =
    promise {
        try
            let! snapshotOpt = getSnapshot ()
            match snapshotOpt with
            | None -> holder.Mutate(fun state -> clearSession state workspaceId, ())
            | Some snapshot ->
                match holder.Mutate(fun state -> decideNudge reviewStore.isReviewActive lookupChildAgent state workspaceId snapshot) with
                | StandDown -> ()
                | Send(promptText, agentOpt) ->
                    let! outcome = attemptSend promptText agentOpt
                    holder.Mutate(fun state ->
                        match tryRecordSend state workspaceId outcome with
                        | Some nextState -> nextState, ()
                        | None -> state, ())
        with _ ->
            holder.Mutate(fun state -> clearSession state workspaceId, ())
    }

type NudgeRuntime
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore,
      getChatHistory: (string -> JS.Promise<obj array>) option) =

    let holder = StateHolder<NudgeShellState>(emptyState)

    member _.HandleEvent(parsed: NudgeRuntimeEvent, helpers: obj) : JS.Promise<unit> =
        promise {
            match parsed with
            | Ignore -> return ()
            | StreamEnd(workspaceId, stopReason, lastAssistantMessage) ->
                if Dyn.isNullish helpers || stopReason = "queued-message" then
                    return ()
                else
                    runNudgeFlow holder reviewStore getChatHistory helpers workspaceId lastAssistantMessage |> ignore
                    return ()
            | StreamAbort workspaceId
            | AbortedError workspaceId ->
                holder.Mutate(fun state -> clearSession state workspaceId, ())
                return ()
        }

let createNudgeRuntime
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    : NudgeRuntime =
    NudgeRuntime(reviewStore, getChatHistory)


