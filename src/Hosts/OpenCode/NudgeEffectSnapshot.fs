module Wanxiangshu.Hosts.Opencode.NudgeEffectSnapshot

open Fable.Core
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge.NudgeSnapshotSource
open Wanxiangshu.Runtime.Nudge.NudgeDerivation
open Wanxiangshu.Runtime.NudgeModelResolver
open Wanxiangshu.Runtime.SessionEventWriter
open Wanxiangshu.Runtime.EventLogRuntimeNudge
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.Messaging.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore

module Dyn = Wanxiangshu.Runtime.Dyn

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

let private tryAssembleSnapshot
    (messagesArr: obj array)
    (idx: int)
    (openTodos: string list)
    (fallbackRuntime: FallbackRuntimeStore)
    (pluginCtx: obj)
    (sessionIDStr: string)
    (isForceStopped: string -> bool)
    : JS.Promise<SessionSnapshot option> =
    promise {
        if isForceStopped sessionIDStr then
            return None
        else
            let! result =
                assembleMessageInfo messagesArr idx fallbackRuntime sessionIDStr pluginCtx openTodos isForceStopped

            return result
    }

let private processSessionMessages
    (messagesData: obj)
    (openTodosFromApi: string list)
    (fallbackRuntime: FallbackRuntimeStore)
    (pluginCtx: obj)
    (sessionIDStr: string)
    (isForceStopped: string -> bool)
    : JS.Promise<SessionSnapshot option> =
    promise {
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
                | Some idx ->
                    return!
                        tryAssembleSnapshot
                            messagesArr
                            idx
                            openTodos
                            fallbackRuntime
                            pluginCtx
                            sessionIDStr
                            isForceStopped
    }

/// Distinguish a real error from "not needed". The snapshot layer is the
/// single place that can say "not needed" (None) vs "transport / event-store
/// failure" (typed exception).
///
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
            | Error e -> return raise (System.Exception("opencode_session_api_missing"))
            | Ok session when not (Dyn.has session "todo") || not (Dyn.has session "messages") -> return None
            | Ok session ->
                let! todoResp = invoke1 (box {| path = {| id = sessionIDStr |} |}) "todo" session
                let openTodosFromApi = decodeTodos (Dyn.get todoResp "data")
                let! messagesResp = invoke1 (box {| path = {| id = sessionIDStr |} |}) "messages" session
                let messagesData = Dyn.get messagesResp "data"

                return!
                    processSessionMessages
                        messagesData
                        openTodosFromApi
                        fallbackRuntime
                        pluginCtx
                        sessionIDStr
                        isForceStopped
    }
