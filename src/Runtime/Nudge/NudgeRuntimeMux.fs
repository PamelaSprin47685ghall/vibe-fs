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
open Wanxiangshu.Runtime.Nudge.MuxNudgeEventParse
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

let getRootDirectory (workspaceDirectory: string) : string =
    if workspaceDirectory <> "" then
        workspaceDirectory
    else
        unbox<string> (nodeProcess?cwd ())

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
