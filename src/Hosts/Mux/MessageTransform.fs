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
open Wanxiangshu.Runtime.PromptFrontMatter
open Wanxiangshu.Hosts.Mux.MessagingCodec
open Wanxiangshu.Hosts.Mux.BacklogSession
open Wanxiangshu.Hosts.Mux.CapsCodec
open Wanxiangshu.Runtime.JsArrayMutate
open Wanxiangshu.Runtime.Fallback.HumanTurnTransitions
open Wanxiangshu.Runtime.Fallback.OrdinalTransitions
open Wanxiangshu.Runtime.Fallback.CompactionTransitions
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.HostMessageCodec
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.MuxHookInputCodec
open Wanxiangshu.Runtime.MuxWorkspaceCodec
open Wanxiangshu.Runtime.ChatTransformOutputCodec
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.ContextBudgetUsageCodec
open Wanxiangshu.Hosts.Mux.MessageTransformCompaction

let private maxInputTokensCache =
    System.Collections.Generic.Dictionary<string, int>()

let resolvePolicies (agent: string) (isChild: bool) =
    let bp =
        Wanxiangshu.Kernel.MessageTransformPolicy.getBacklogProjectionPolicy agent isChild

    let cp =
        Wanxiangshu.Kernel.MessageTransformPolicy.getCapsInjectionPolicy agent isChild

    let pp =
        Wanxiangshu.Kernel.MessageTransformPolicy.getParallelHintPolicy agent isChild

    let cb =
        Wanxiangshu.Kernel.MessageTransformPolicy.getContextBudgetPolicy agent isChild

    bp, cp, pp, cb

let fetchMaxInputTokens (sessionID: string) (deps: obj) (input: obj) (directory: string) =
    promise {
        match maxInputTokensCache.TryGetValue(sessionID) with
        | true, limit -> return limit
        | _ ->
            let! limit = resolveMaxInputTokens [ deps; input ] sessionID directory
            maxInputTokensCache.[sessionID] <- limit
            return limit
    }

let resolveContextUsage (deps: obj) (sessionID: string) (directory: string) =
    match ContextBudgetUsageCodec.tryGetRealContextUsage deps sessionID directory with
    | Some f -> f
    | None -> fun (_: obj array) -> Promise.lift None

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
    (backlogPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy)
    (capsPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy)
    (parallelHintPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy)
    (contextBudgetPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy)
    (cleanedMessages: Message<obj> list)
    (backlogOps: BacklogSessionOps)
    (maxInputTokens: int)
    (getContextUsage: obj array -> JS.Promise<int option>)
    : MessageTransformPlan =
    { SessionID = sessionID
      Agent = agent
      Directory = directory
      ProjectionPolicy =
        if backlogPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include then
            ProjectionPolicy.IncludeProjection
        else
            ProjectionPolicy.ExcludeProjection
      BacklogProjectionPolicy = backlogPolicy
      CapsInjectionPolicy = capsPolicy
      ParallelHintPolicy = parallelHintPolicy
      ContextBudgetPolicy = contextBudgetPolicy
      IsSubagentSession = isChild
      Cleaned = cleanedMessages
      RawArray = Some messagesArr
      SembleInjectEnabled = false
      Scope = runtimeScope
      MaxInputTokens = maxInputTokens
      GetContextUsage = getContextUsage }

let applyTransformPipeline
    (deps: obj)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (backlogSession: BacklogSession)
    (reviewStore: ReviewStore)
    (input: obj)
    (decoded: Wanxiangshu.Runtime.MuxHookInputCodec.MuxMessagesTransformInput)
    (messagesArr: obj[])
    (sessionID: string)
    : JS.Promise<unit> =
    promise {
        let directory = decoded.Directory
        let agent = decoded.Agent
        let isChild = isChildWorkspace deps sessionID

        let backlogPolicy, capsPolicy, parallelHintPolicy, contextBudgetPolicy =
            resolvePolicies agent isChild

        let cleanedMessages = sanitizeMuxMessages sessionID messagesArr

        let backlogOps =
            backlogSessionOpsFrom backlogSession.Host (fun sid msgs -> backlogSession.GetOrRebuildBacklog(sid, msgs))

        let! maxInputTokens = fetchMaxInputTokens sessionID deps input directory
        let getContextUsage = resolveContextUsage deps sessionID directory

        let plan =
            buildPlan
                sessionID
                agent
                directory
                isChild
                runtimeScope
                messagesArr
                backlogPolicy
                capsPolicy
                parallelHintPolicy
                contextBudgetPolicy
                cleanedMessages
                backlogOps
                maxInputTokens
                getContextUsage

        let buildCaps encoded capsFiles prelude =
            buildCapsMessages sessionID encoded capsFiles prelude

        let! final =
            runHostMessagesTransform
                reviewStore
                sessionID
                plan
                backlogOps
                encodeMessages
                (fun _policy encoded -> Promise.lift encoded)
                (fun () -> loadCapsForSession runtimeScope deps sessionID plan)
                buildCaps

        if not cleanedMessages.IsEmpty then
            replaceArrayInPlace messagesArr final
    }

let messagesTransform
    (deps: obj)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (backlogSession: BacklogSession)
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
            do! applyTransformPipeline deps runtimeScope backlogSession reviewStore input decoded messagesArr sessionID
    }

let buildGuid = MessageTransformCompaction.buildGuid
let readCompactionMetadata = MessageTransformCompaction.readCompactionMetadata
let compactMessageBatch = MessageTransformCompaction.compactMessageBatch
let buildCompactedResult = MessageTransformCompaction.buildCompactedResult

let buildCompactedResultOutput =
    MessageTransformCompaction.buildCompactedResultOutput

let compactingTransform
    (deps: obj)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (backlogSession: BacklogSession)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        let decoded = decodeMuxMessagesTransformInput input deps
        let directory = decoded.Directory
        runtimeScope.TriggerInit(directory)
        do! runtimeScope.WaitInit()
        let messagesArr = compactMessageBatch input output

        if messagesArr.Length > 0 then
            let sessionID = decoded.SessionID
            let cleaned = sanitizeMuxMessages sessionID messagesArr
            let backlog = backlogSession.GetOrRebuildBacklog(sessionID, cleaned)
            let! result = buildCompactedResult deps runtimeScope directory sessionID cleaned backlog
            buildCompactedResultOutput result output
    }
