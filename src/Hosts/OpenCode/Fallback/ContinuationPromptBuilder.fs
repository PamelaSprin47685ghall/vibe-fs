module Wanxiangshu.Hosts.Opencode.Fallback.ContinuationPromptBuilder

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Fallback.Continuation
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection
open Wanxiangshu.Runtime.OpencodeSessionPromptBuilder
open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Dispatch.Protocol

let resolveModelAndAgent
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
                let fromRuntime = (runtime.GetSession sessionID).AgentName
                if fromRuntime <> "" then Some fromRuntime else None

    modelStr, agent

let createContinuationPromptBody
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (model: FallbackModel)
    (agent: string option)
    (payload: string)
    (mode: ContinuationMode)
    (continuationID: string)
    : obj =
    let state = runtime.GetSession sessionID

    match state.PendingLease with
    | Some lease when lease.ContinuationID = continuationID ->
        let request: ContinuationRequest =
            { ContinuationId = continuationID
              ContinuationOrdinal = lease.ContinuationOrdinal
              Attempt = 1
              SessionId = sessionID
              HumanTurnId = lease.HumanTurnID
              SourceHumanMessageId =
                if state.LastHumanMessageId = "" then
                    None
                else
                    Some state.LastHumanMessageId
              ContextGeneration = lease.SessionGeneration
              CancelGeneration = lease.CancelGeneration
              Model = model
              Agent = agent |> Option.defaultValue ""
              Mode = mode }

        createFallbackContinuationPromptBody agent payload request
    | _ -> invalidOp "fallback_continuation_lease_missing"

let buildContinuationPrompt
    (runtime: FallbackRuntimeStore)
    (client: obj)
    (sessionID: string)
    (model: FallbackModel)
    (promptText: string)
    (mode: ContinuationMode)
    (continuationID: string) =
    fun (_: DispatchIdentity) ->
        promise {
            let! _, liveAgentOpt = tryGetSessionModelAndAgentAsync client sessionID
            let! infoOpt = tryReadLatestMessageInfo client sessionID

            let _, agent = resolveModelAndAgent runtime liveAgentOpt model sessionID infoOpt

            let body =
                createContinuationPromptBody
                    runtime
                    sessionID
                    model
                    agent
                    promptText
                    mode
                    continuationID

            let arg =
                box
                    {| path = box {| id = sessionID |}
                       body = body |}

            do! invokeClient client "prompt" arg |> Promise.map ignore
            return OpaqueAccepted continuationID
        }
