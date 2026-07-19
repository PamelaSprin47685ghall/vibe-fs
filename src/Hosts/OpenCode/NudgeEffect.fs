module Wanxiangshu.Hosts.Opencode.NudgeEffect

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.NudgeRuntime
open Wanxiangshu.Runtime.NudgeRuntimeState
open Wanxiangshu.Runtime.OpencodeSessionPromptBuilder
open Wanxiangshu.Runtime.NudgeFlow
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Hosts.Opencode.NudgeEffectSnapshot

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

/// Send a nudge through the unified `DispatchRegistry` so two nudges on
/// the same physical session cannot race. Calling `client.session.prompt`
/// directly and resolving `Promise.result` as "delivered" could silently
/// succeed when the session API is missing and did not always clear the
/// active nudge nonce.
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

        fallbackRuntime.UpdateSession(sidStr, armNudgeNonce nonce)

        try
            match getSessionApiFromClient client with
            | Error _ ->
                // Surface the typed failure AND clear the
                // active nudge nonce / owner on the early-exit path so the
                // next attempt can dispatch.
                let _ = fallbackRuntime.UpdateSessionReturning(sidStr, tryConsumeNudgeNonce nonce)

                if (fallbackRuntime.GetSession sidStr).Owner = SessionOwner.Nudge then
                    fallbackRuntime.UpdateSession(sidStr, transferOwnership SessionOwner.NoOwner)

                return raise (System.Exception("opencode_session_api_missing"))
            | Ok session ->
                let body =
                    createPromptBodyWithModelAndNonce agentOpt modelOpt promptText (Some nonce)

                let promptArg =
                    box
                        {| path = box {| id = sidStr |}
                           body = body |}

                do! invoke1 promptArg "prompt" session |> Promise.map ignore
        with ex ->
            // Any failure before terminal resolution must clear the active
            // nonce and release ownership so the session is not wedged.
            let _ = fallbackRuntime.UpdateSessionReturning(sidStr, tryConsumeNudgeNonce nonce)

            if (fallbackRuntime.GetSession sidStr).Owner = SessionOwner.Nudge then
                fallbackRuntime.UpdateSession(sidStr, transferOwnership SessionOwner.NoOwner)

            JS.console.error (
                box
                    {| feature = "nudge"
                       session = sidStr
                       dispatchId = nonce
                       hostVariant = "opencode"
                       error = ex.Message |}
            )

            return raise ex
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
                let msg = string error

                if msg = "opencode_session_api_missing" then
                    TransportUnavailable msg
                else
                    match translateJsError error with
                    | MessageAborted -> Aborted
                    | Wanxiangshu.Kernel.Errors.DomainError.SessionBusy -> Busy
                    | _ -> Wanxiangshu.Kernel.Nudge.Types.Failed msg
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
