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

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

type private MuxReceiptValidationResult =
    | ValidReceipt of messageId: string
    | InvalidReceipt of error: string
    | SimpleSuccess
    | SimpleFailure

let private validateMuxReceipt
    (result: obj)
    (expectedSessionId: string)
    (expectedDispatchId1: string)
    (expectedDispatchId2: string)
    : MuxReceiptValidationResult =
    if Dyn.isNullish result then
        InvalidReceipt "nudge returned nullish value"
    elif Dyn.typeIs result "boolean" then
        if unbox<bool> result then SimpleSuccess else SimpleFailure
    else
        let msgId = Dyn.str result "messageId"
        let msgId = if msgId <> "" then msgId else Dyn.str result "receiptId"
        let sessId = Dyn.str result "sessionId"

        let sessId =
            if sessId <> "" then
                sessId
            else
                Dyn.str result "workspaceId"

        let dispId = Dyn.str result "dispatchId"
        let dispId = if dispId <> "" then dispId else Dyn.str result "nonce"

        let dispId =
            if dispId <> "" then
                dispId
            else
                Dyn.str result "continuationId"

        let dispId =
            if dispId <> "" then
                dispId
            else
                Dyn.str result "continuationID"

        if sessId <> expectedSessionId then
            InvalidReceipt $"Receipt sessionId mismatch: expected {expectedSessionId}, got {sessId}"
        elif dispId <> expectedDispatchId1 && dispId <> expectedDispatchId2 then
            InvalidReceipt
                $"Receipt dispatchId mismatch: expected {expectedDispatchId1} or {expectedDispatchId2}, got {dispId}"
        elif msgId = "" then
            InvalidReceipt "Receipt messageId is empty"
        else
            ValidReceipt msgId

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
