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
open Wanxiangshu.Runtime.MuxNudgeEventParse
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.Review.ReviewLoopFold
open Wanxiangshu.Runtime.SessionEventWriter
open Wanxiangshu.Runtime.EventLogRuntimeNudge
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.NudgeRuntimeState
open Wanxiangshu.Runtime.NudgeRuntimeEvent
open Wanxiangshu.Runtime.NudgeFlow
open Wanxiangshu.Runtime.NudgeModelResolver
open Wanxiangshu.Runtime.NudgeRuntimeMuxHelper

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

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
            if Dyn.isNullish helpers then
                return SendOutcome.Failed "helpers missing"
            else
                let nudgeFn = Dyn.get helpers "nudge"

                if Dyn.isNullish nudgeFn then
                    return SendOutcome.Failed "helpers.nudge missing"
                elif not (Dyn.typeIs nudgeFn "function") then
                    return SendOutcome.Failed "helpers.nudge is not a function"
                else
                    let agentVal =
                        match agentOpt with
                        | Some a -> box a
                        | None -> null

                    let modelVal =
                        match modelOpt with
                        | Some m -> box m
                        | None -> null

                    fallbackRuntime.Update(workspaceId, setMainContinuationAwaitingStart true)

                    let! result =
                        (nudgeFn $ (workspaceId, promptText, modelVal, agentVal, nudgeId, nonce))
                        |> unbox<JS.Promise<obj>>

                    let validation = validateMuxReceipt result workspaceId nonce nudgeId

                    return
                        match validation with
                        | ValidReceipt _ -> SendOutcome.Delivered
                        | SimpleSuccess ->
                            SendOutcome.AcceptanceUnknown "nudge resolved true, cannot verify delivery without receipt"
                        | SimpleFailure -> SendOutcome.Busy
                        | InvalidReceipt err -> SendOutcome.Failed err
        with ex ->
            JS.console.error (
                box
                    {| feature = "nudge"
                       session = workspaceId
                       hostVariant = "mux"
                       error = ex.Message |}
            )

            return SendOutcome.Failed ex.Message
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

        let abortRun (_: string) =
            Promise.reject (
                System.Exception("AbortUnavailable: Mux host adapter does not expose a session-level abort API")
            )

        runNudgeFlowCore Mux root fallbackRuntime runtimeState sessionKey takeSnapshot sendNudge abortRun
