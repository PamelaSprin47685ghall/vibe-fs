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
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn

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

let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = unbox (target?(method) (arg))

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

let private collectSnapshot
    (fallbackRuntime: FallbackRuntimeStore)
    (client: obj)
    (pluginCtx: obj)
    (sessionID: SessionId)
    (isForceStopped: string -> bool)
    : JS.Promise<SessionSnapshot option> =
    promise {
        try
            let sessionIDStr = Id.sessionIdValue sessionID

            if isForceStopped sessionIDStr then
                return None
            else
                match getSessionApiFromClient client with
                | Error _ -> return None
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
        with _ ->
            return None
    }

let private sendNudge
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

        let body =
            createPromptBodyWithModelAndNonce agentOpt modelOpt promptText (Some nonce)

        let promptArg =
            box
                {| path = box {| id = sidStr |}
                   body = body |}

        match getSessionApiFromClient client with
        | Error _ -> ()
        | Ok session ->
            try
                do! invoke1 promptArg "prompt" session |> Promise.map ignore
            finally
                fallbackRuntime.ClearActiveNudgeNonce sidStr
    }

let private sendNudgeOutcome
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
                | SessionBusy -> Busy
                | _ -> Failed
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
