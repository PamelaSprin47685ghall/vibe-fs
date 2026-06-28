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
open Wanxiangshu.Shell.NudgeSnapshot
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.ErrorClassify

type NudgeRuntimeEvent =
    | Ignore
    | StreamEnd of workspaceId: string * stopReason: string * lastAssistantMessage: string
    | StreamAbort of workspaceId: string
    | AbortedError of workspaceId: string

let runNudgeFlowCore
    (holder: StateHolder<NudgeShellState>)
    (isReviewActive: string -> bool)
    (lookupChildAgent: string -> string option)
    (sessionKey: string)
    (takeSnapshot: unit -> JS.Promise<SessionSnapshot option>)
    (sendNudge: string -> string option -> JS.Promise<SendOutcome>)
    : JS.Promise<unit> =
    promise {
        try
            let! snapshotOpt = takeSnapshot ()
            match snapshotOpt with
            | None -> holder.Mutate(fun state -> clearSession state sessionKey, ())
            | Some snapshot ->
                match holder.Mutate(fun state -> decideNudge isReviewActive lookupChildAgent state sessionKey snapshot) with
                | StandDown -> ()
                | Send(promptText, agentOpt) ->
                    let! outcome = sendNudge promptText agentOpt
                    holder.Mutate(fun state ->
                        match tryRecordSend state sessionKey outcome with
                        | Some nextState -> nextState, ()
                        | None -> state, ())
        with ex ->
            match translateJsError ex with
            | MessageAborted | SessionBusy | TaskWaitBackgrounded -> ()
            | _ -> holder.Mutate(fun state -> clearSession state sessionKey, ())
    }

let private runNudgeFlow
    (holder: StateHolder<NudgeShellState>)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    (helpers: obj)
    (workspaceId: string)
    (lastAssistantMessage: string)
    : JS.Promise<unit> =
    runNudgeFlowCore
        holder
        reviewStore.isReviewActive
        (fun _ -> None)
        workspaceId
        (fun () -> collectSnapshot getChatHistory helpers workspaceId lastAssistantMessage |> Promise.map Some)
        (fun promptText _ ->
            promise {
                try
                    let nudgeFn = Dyn.get helpers "nudge"
                    let! delivered = unbox<JS.Promise<bool>> (Dyn.call2 nudgeFn workspaceId promptText)
                    return if delivered then Delivered else Busy
                with _ ->
                    return Busy
            })

let private startNudgeFlow
    (holder: StateHolder<NudgeShellState>)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (getChatHistory: (string -> JS.Promise<obj array>) option)
    (helpers: obj)
    (workspaceId: string)
    (lastAssistantMessage: string)
    : unit =
    runNudgeFlow holder reviewStore getChatHistory helpers workspaceId lastAssistantMessage |> Promise.start

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
                    startNudgeFlow holder reviewStore getChatHistory helpers workspaceId lastAssistantMessage
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
