module Wanxiangshu.Hosts.Opencode.NudgeEffect

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Runtime.Nudge.NudgeDerivation
open Wanxiangshu.Kernel.Nudge.NudgeSnapshotSource
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.Review.ReviewLoopFold
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.NudgeRuntime
open Wanxiangshu.Runtime.NudgeRuntimeState
open Wanxiangshu.Hosts.Opencode.NudgeEffectPrompt
open Wanxiangshu.Runtime.NudgeFlow
open Wanxiangshu.Runtime.NudgeModelResolver
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Hosts.Opencode.NudgeEffectHelpers

module Dyn = Wanxiangshu.Runtime.Dyn

let private invokeClient (client: obj) (method_: string) (arg: obj) : JS.Promise<obj> =
    if Dyn.isNullish client then
        Promise.lift (unbox null)
    else
        match getSessionApiFromClient client with
        | Error _ -> Promise.lift (unbox null)
        | Ok session ->
            let api: obj = Dyn.get session method_

            if Dyn.isNullish api then
                Promise.lift (unbox null)
            else
                 unbox<JS.Promise<obj>> (Dyn.callMethod1 session method_ arg)

let buildSnapshotResult (snap: Wanxiangshu.Kernel.Nudge.NudgeProjection.NudgeSnapshotState) : SessionSnapshot =
    let anchor =
        Wanxiangshu.Kernel.Nudge.NudgeProjection.nudgeAnchorKey snap.turnId snap.lastAssistantText

    let blockStatus =
        if
            Wanxiangshu.Kernel.Nudge.NudgeProjection.isBlocked
                { PendingNudge = snap.pendingNudge
                  LastDispatchedAnchor = snap.lastDispatchedAnchor }
                anchor
        then
            NudgeBlockStatus.Blocked
        else
            NudgeBlockStatus.Allowed

    sessionSnapshotFromFold snap RunnerPresence.Absent blockStatus

let assembleMessageInfo
    (messagesArr: obj array)
    (idx: int)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionIDStr: string)
    (pluginCtx: obj)
    (openTodos: string list)
    (isForceStopped: string -> bool)
    : JS.Promise<SessionSnapshot option> =
    let msg = messagesArr.[idx]
    let info = Dyn.get msg "info"
    let agentVal = Dyn.get info "agent"

    let agent =
        if Dyn.isNullish agentVal then
            None
        else
            Some(string agentVal)

    let text = getPartsText (Dyn.get msg "parts")
    let modelVal = Dyn.get info "model"
    let model = parseModelVal modelVal
    let resolvedModel = resolveNudgeModel messagesArr fallbackRuntime sessionIDStr model
    let directory = pluginDirectoryFromCtx pluginCtx

    appendAssistantCompletedOrFail directory sessionIDStr text agent resolvedModel (getTurnId info idx) openTodos
    |> Promise.bind (fun () -> getNudgeSnapshotFromEventLog directory sessionIDStr)
    |> Promise.map (fun snap ->
        if isForceStopped sessionIDStr then
            None
        else
            Some(buildSnapshotResult snap))

/// Distinguish a real error from "not needed". The previous implementation
/// used a blanket `try ... with _ -> None` that hid event-store failures
/// as "no nudge needed" (N-04). The new contract is:
///   NotNeeded = no problem; the flow treats `None` as a normal no-op
///   SnapshotUnavailable = a real failure; surface it as a typed
///     exception so the runNudgeFlowCore caller can mark the nudge as
///     TransportFailure rather than silently skipping it
///
/// The function keeps the `try ... with` form so a synchronous bug
/// in the host API path is converted to a typed exception, but the
/// happy path is unaffected.
let collectSnapshot
    (fallbackRuntime: FallbackRuntimeStore)
    (client: obj)
    (pluginCtx: obj)
    (sessionID: SessionId)
    (isForceStopped: string -> bool)
    : JS.Promise<SessionSnapshot option> =
    promise {
        let sessionIDStr = Id.sessionIdValue sessionID

        if isForceStopped sessionIDStr then
            return None
        else
            match getSessionApiFromClient client with
            | Error _ -> return raise (System.Exception("opencode_session_api_missing"))
            | Ok session when not (Dyn.has session "todo") || not (Dyn.has session "messages") -> return None
            | Ok session ->
                let! todoResp = invoke1 (box {| path = {| id = sessionIDStr |} |}) "todo" session
                let openTodosFromApi = decodeTodos (Dyn.get todoResp "data")
                let! messagesResp = invoke1 (box {| path = {| id = sessionIDStr |} |}) "messages" session
                let messagesData = Dyn.get messagesResp "data"

                if not (Dyn.isArray messagesData) then
                    return None
                else
                    let messagesArr = messagesData :?> obj array

                    let openTodos =
                        if not (List.isEmpty openTodosFromApi) then
                            openTodosFromApi
                        else
                            recoverOpenTodosFromMessages messagesData

                    let shouldSkip = shouldSkipNudge messagesData

                    if shouldSkip then
                        return None
                    else
                        let lastAssistantIdx = tryFindLastAssistantIdx messagesArr

                        match lastAssistantIdx with
                        | None -> return None
                        | Some idx when isForceStopped sessionIDStr -> return None
                        | Some idx ->
                            let! result =
                                assembleMessageInfo
                                    messagesArr
                                    idx
                                    fallbackRuntime
                                    sessionIDStr
                                    pluginCtx
                                    openTodos
                                    isForceStopped

                            return result
    }

/// Send a nudge through the unified `DispatchRegistry` so two nudges on
/// the same physical session cannot race. The previous implementation
/// called `client.session.prompt` directly and resolved
/// `Promise.result` as "delivered", which violated N-01
/// (silent success when session API is missing) and N-02
/// (active-nudge-nonce not always cleared).
let sendNudge
    (fallbackRuntime: FallbackRuntimeStore)
    (client: obj)
    (sessionID: SessionId)
    (agentOpt: string option)
    (modelOpt: string option)
    (promptText: string)
    (nudgeId: string)
    (nonce: string)
    : JS.Promise<unit> =
    promise {
        let sidStr = Id.sessionIdValue sessionID

        fallbackRuntime.SetActiveNudgeNonce sidStr nonce

        match getSessionApiFromClient client with
        | Error _ ->
            // N-01 + N-02: surface the typed failure AND clear the
            // active nudge nonce / owner on the early-exit path so the
            // next attempt can dispatch.
            let _ = fallbackRuntime.TryConsumeActiveNudgeNonce(sidStr, nonce)

            if fallbackRuntime.GetSessionOwner sidStr = SessionOwner.Nudge then
                fallbackRuntime.SetSessionOwner sidStr SessionOwner.NoOwner

            return raise (System.Exception("opencode_session_api_missing"))
        | Ok session ->
            let body =
                createPromptBodyWithModelAndNonce agentOpt modelOpt promptText (Some nonce)

            let promptArg =
                box
                    {| path = box {| id = sidStr |}
                       body = body |}

            do! invoke1 promptArg "prompt" session |> Promise.map ignore
    }

let sendNudgeOutcome
    (fallbackRuntime: FallbackRuntimeStore)
    (client: obj)
    (sessionID: SessionId)
    (promptText: string)
    (agentOpt: string option)
    (modelOpt: string option)
    (nudgeId: string)
    (nonce: string)
    : JS.Promise<SendOutcome> =
    promise {
        let! caught =
            sendNudge fallbackRuntime client sessionID agentOpt modelOpt promptText nudgeId nonce
            |> Promise.result

        return
            match caught with
            | Ok() -> Delivered
            | Error error ->
                match translateJsError error with
                | MessageAborted -> Aborted
                | Wanxiangshu.Kernel.Errors.DomainError.SessionBusy -> Busy
                | _ -> Wanxiangshu.Kernel.Nudge.Types.Failed
    }

let startNudgeFlow
    (host: Host)
    (fallbackRuntime: FallbackRuntimeStore)
    (runtimeState: NudgeRuntimeState)
    (client: obj)
    (pluginCtx: obj)
    (sessionID: SessionId)
    (isForceStopped: string -> bool)
    : JS.Promise<NudgeRuntimeState> =
    let sid = Id.sessionIdValue sessionID
    let root = pluginDirectoryFromCtx pluginCtx

    let abortRun sessionIDStr =
        promise {
            let arg = box {| path = box {| id = sessionIDStr |} |}
            do! invokeClient client "abort" arg |> Promise.map ignore
        }

    runNudgeFlowCore
        host
        root
        fallbackRuntime
        runtimeState
        sid
        (fun () -> collectSnapshot fallbackRuntime client pluginCtx sessionID isForceStopped)
        (fun promptText agentOpt modelOpt nudgeId nonce ->
            sendNudgeOutcome fallbackRuntime client sessionID promptText agentOpt modelOpt nudgeId nonce)
        abortRun

let dispatchPostStopFromHistory
    (host: Host)
    (fallbackRuntime: FallbackRuntimeStore)
    (client: obj)
    (pluginCtx: obj)
    (sessionID: SessionId)
    (isForceStopped: string -> bool)
    : JS.Promise<unit> =
    promise {
        let! _ = startNudgeFlow host fallbackRuntime emptyRuntimeState client pluginCtx sessionID isForceStopped
        return ()
    }
