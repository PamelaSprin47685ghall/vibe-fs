module Wanxiangshu.Hosts.Opencode.Fallback.ActionExecutor

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.FallbackBridgePorts
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection

/// Zero-width space character used as the fallback `SendContinue` prompt
/// text. It is invisible in any UI but still parsed by LLMs as a non-empty
/// text, so it triggers the protocol-driven "continue" loop without leaving
/// a "continue" footprint in the LLM history that the consumer side could
/// mistake for a real user message.
let private zwsChar = "​"

let private resolveModelAndAgent
    (runtime: FallbackRuntimeStore)
    (liveAgentOpt: string option)
    (fallbackModel: FallbackModel)
    (sessionID: string)
    (infoOpt: obj option)
    =
    let finalModel = fallbackModel

    let modelStr =
        match finalModel.Variant with
        | Some v -> sprintf "%s/%s:%s" finalModel.ProviderID finalModel.ModelID v
        | None -> sprintf "%s/%s" finalModel.ProviderID finalModel.ModelID

    let agent =
        match liveAgentOpt with
        | Some sa -> Some sa
        | None ->
            let fromMsg =
                infoOpt
                |> Option.map (fun info -> Dyn.str info "agent")
                |> Option.filter (fun value -> value <> "")

            match fromMsg with
            | Some a -> Some a
            | None ->
                let fromRuntime = runtime.GetAgentName sessionID
                if fromRuntime <> "" then Some fromRuntime else None

    modelStr, agent

let private fetchMessagesImpl (client: obj) (sessionID: string) : JS.Promise<obj array> =
    promise {
        let arg = box {| path = box {| id = sessionID |} |}
        let! resp = invokeClient client "messages" arg
        let data = Dyn.get resp "data"

        if Dyn.isArray data then
            return (data :?> obj array)
        else
            return [||]
    }

let private sendContinueImpl
    (runtime: FallbackRuntimeStore)
    (client: obj)
    (sessionID: string)
    (model: FallbackModel)
    (continuationID: string)
    : JS.Promise<unit> =
    promise {
        let! _, liveAgentOpt = tryGetSessionModelAndAgentAsync client sessionID
        let! infoOpt = tryReadLatestMessageInfo client sessionID

        let modelStr, agent =
            resolveModelAndAgent runtime liveAgentOpt model sessionID infoOpt

        let body =
            createPromptBodyWithModelAndNonce agent (Some modelStr) zwsChar (Some continuationID)

        let arg =
            box
                {| path = box {| id = sessionID |}
                   body = body |}

        do! invokeClient client "prompt" arg |> Promise.map ignore
    }

let private captureCurrentModelImpl
    (runtime: FallbackRuntimeStore)
    (client: obj)
    (sessionID: string)
    : JS.Promise<FallbackModel option> =
    promise {
        let! msgs = fetchMessagesImpl client sessionID

        match Wanxiangshu.Runtime.Fallback.FallbackMessageCodec.tryGetLatestUserModel msgs with
        | Some m -> return Some m
        | None ->
            match runtime.GetModel sessionID with
            | Some m -> return Some m
            | None -> return! tryReadCurrentModel client sessionID
    }

let private recoverWithPromptImpl
    (runtime: FallbackRuntimeStore)
    (client: obj)
    (sessionID: string)
    (model: FallbackModel)
    (promptText: string)
    (continuationID: string)
    : JS.Promise<unit> =
    promise {
        let! _, liveAgentOpt = tryGetSessionModelAndAgentAsync client sessionID
        let! infoOpt = tryReadLatestMessageInfo client sessionID

        let modelStr, agent =
            resolveModelAndAgent runtime liveAgentOpt model sessionID infoOpt

        let body =
            createPromptBodyWithModelAndNonce agent (Some modelStr) promptText (Some continuationID)

        let arg =
            box
                {| path = box {| id = sessionID |}
                   body = body |}

        do! invokeClient client "prompt" arg |> Promise.map ignore
    }

let private abortRunImpl (client: obj) (sessionID: string) : JS.Promise<unit> =
    promise {
        let arg = box {| path = box {| id = sessionID |} |}
        do! invokeClient client "abort" arg |> Promise.map ignore
    }

let opencodeActionExecutor (runtime: FallbackRuntimeStore) (client: obj) : IActionExecutor =
    { new IActionExecutor with
        member _.SendContinue(sessionID, model, continuationID) =
            sendContinueImpl runtime client sessionID model continuationID

        member _.FetchMessages sessionID = fetchMessagesImpl client sessionID
        member _.PropagateFailure _sessionID = Promise.lift ()

        member _.CaptureCurrentModel sessionID =
            captureCurrentModelImpl runtime client sessionID

        member _.RecoverWithPrompt(sessionID, model, promptText, continuationID) =
            recoverWithPromptImpl runtime client sessionID model promptText continuationID

        member _.AbortRun sessionID = abortRunImpl client sessionID }
