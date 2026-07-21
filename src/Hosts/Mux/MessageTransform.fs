module Wanxiangshu.Hosts.Mux.MessageTransform

open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.HostEntry
open Wanxiangshu.Runtime.MessageTransform.HostHooks
open Wanxiangshu.Runtime.MessageTransform.Pipeline
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Hosts.Mux.MessagingCodec
open Wanxiangshu.Hosts.Mux.CapsCodec
open Wanxiangshu.Runtime.JsArrayMutate
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.HostMessageCodec
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.MuxHookInputCodec
open Wanxiangshu.Runtime.MuxWorkspaceCodec
open Wanxiangshu.Runtime.ChatTransformOutputCodec
open Wanxiangshu.Runtime.Dyn

let resolvePolicies (agent: string) (isChild: bool) =
    let cp =
        Wanxiangshu.Kernel.MessageTransformPolicy.getCapsInjectionPolicy agent isChild

    let pp =
        Wanxiangshu.Kernel.MessageTransformPolicy.getParallelHintPolicy agent isChild

    cp, pp

let sanitizeMuxMessages (sessionID: string) (messagesArr: obj[]) = decodeMessages sessionID messagesArr

let loadCapsForSession
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (deps: obj)
    (sessionID: string)
    (plan: MessageTransformPlan)
    =
    promise {
        let parentSessionID =
            match tryGetParentWorkspaceId deps sessionID with
            | Some pid -> pid
            | None -> sessionID

        let planWithParent =
            { plan with
                SessionID = parentSessionID }

        let! caps = loadCapsForScope runtimeScope RequireDirectory planWithParent
        return caps |> List.sortBy (fun cf -> cf.label, cf.filePath)
    }

let private buildPlan
    (sessionID: string)
    (agent: string)
    (directory: string)
    (isChild: bool)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (messagesArr: obj[])
    (capsPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy)
    (parallelHintPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy)
    (cleanedMessages: Message<obj> list)
    (maxInputTokens: int)
    : MessageTransformPlan =
    { SessionID = sessionID
      Agent = agent
      Directory = directory
      ProjectionPolicy = projectionPolicyForAgent agent isChild
      CapsInjectionPolicy = capsPolicy
      ParallelHintPolicy = parallelHintPolicy
      IsSubagentSession = isChild
      Cleaned = cleanedMessages
      RawArray = Some messagesArr
      SembleInjectEnabled = false
      Scope = runtimeScope
      MaxInputTokens = maxInputTokens
      ModelKey = "mux:host-unknown"
      LimitSource = "mux:no-model-client"
      ObserveLatestUsage = fun () -> Promise.lift () }

let applyTransformPipeline
    (deps: obj)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (reviewStore: ReviewStore)
    (_input: obj)
    (decoded: Wanxiangshu.Runtime.MuxHookInputCodec.MuxMessagesTransformInput)
    (messagesArr: obj[])
    (sessionID: string)
    : JS.Promise<unit> =
    promise {
        let directory = decoded.Directory
        let agent = decoded.Agent
        let isChild = isChildWorkspace deps sessionID

        let capsPolicy, parallelHintPolicy = resolvePolicies agent isChild

        let cleanedMessages = sanitizeMuxMessages sessionID messagesArr

        let maxInputTokens = 8192

        let plan =
            buildPlan
                sessionID
                agent
                directory
                isChild
                runtimeScope
                messagesArr
                capsPolicy
                parallelHintPolicy
                cleanedMessages
                maxInputTokens

        let buildCaps encoded capsFiles prelude =
            buildCapsMessages sessionID encoded capsFiles prelude

        let! final =
            runHostMessagesTransform
                reviewStore
                sessionID
                plan
                encodeMessages
                (fun _ encoded -> Promise.lift encoded)
                (fun () -> loadCapsForSession runtimeScope deps sessionID plan)
                buildCaps

        if not cleanedMessages.IsEmpty then
            replaceArrayInPlace messagesArr final
    }

let messagesTransform
    (deps: obj)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (reviewStore: ReviewStore)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        let decoded = decodeMuxMessagesTransformInput input deps
        let directory = decoded.Directory
        runtimeScope.TriggerInit(directory)
        do! runtimeScope.WaitInit()

        match tryGetMessagesArrayFromOutput output with
        | None -> ()
        | Some messagesArr ->
            let sessionID = decoded.SessionID
            do! applyTransformPipeline deps runtimeScope reviewStore input decoded messagesArr sessionID
    }
