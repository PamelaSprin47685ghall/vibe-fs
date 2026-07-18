module Wanxiangshu.Hosts.Opencode.MessageTransformPipeline

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.HostEntry
open Wanxiangshu.Runtime.MessageTransform.HostHooks
open Wanxiangshu.Runtime.MessageTransform.Pipeline
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Runtime.PromptFrontMatter
open Wanxiangshu.Hosts.Opencode.AgentConfig
open Wanxiangshu.Hosts.Opencode.BacklogSession
open Wanxiangshu.Hosts.Opencode.MessagingCodec
open Wanxiangshu.Hosts.Opencode.CapsCodec
open Wanxiangshu.Runtime.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ChatTransformOutputCodec
open Wanxiangshu.Runtime.HostMessageCodec
open Wanxiangshu.Kernel.FallbackKernel.Types

open Wanxiangshu.Hosts.Opencode.ModelKeyResolver
open Wanxiangshu.Runtime.JsArrayMutate
open Wanxiangshu.Hosts.Opencode.SembleInjection
open Wanxiangshu.Hosts.Opencode.CompactionHook

let private resolveSessionID (input: obj) (messagesList: Message<obj> list) : string =
    let sid1 = Dyn.str input "sessionID"

    if sid1 <> "" then
        sid1
    else
        let sid2 = Dyn.str input "sessionId"

        if sid2 <> "" then
            sid2
        else
            let sid3 = Dyn.str input "session_id"
            if sid3 <> "" then sid3 else extractSessionID messagesList

let private resolveMaxInputTokens (sessionID: string) (client: obj) (directory: string) : JS.Promise<int> =
    Wanxiangshu.Runtime.ContextBudgetUsageCodec.resolveMaxInputTokens [ client ] sessionID directory

let runHostMessagesTransformExecution
    (registry: ChildAgentRegistry)
    (directory: string)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (backlogSession: BacklogSession)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (sessionID: string)
    (agent: string)
    (messagesArr: obj array)
    (sembleInjectEnabled: bool)
    (capsEpoch: string)
    (plan: MessageTransformPlan)
    : JS.Promise<unit> =
    promise {
        let backlogOps =
            backlogSessionOpsFrom backlogSession.Host (fun sid msgs -> backlogSession.GetOrRebuildBacklog(sid, msgs))

        let injectFn =
            buildInjectionFn directory agent sessionID

        let loadCaps =
            buildLoadCaps registry runtimeScope sessionID plan

        let buildCaps =
            buildCapsFn capsEpoch directory

        let! final =
            runHostMessagesTransform
                reviewStore sessionID plan backlogOps
                MessagingCodec.encodeMessages injectFn loadCaps buildCaps

        replaceArrayInPlace messagesArr final
    }

/// Build the MessageTransformPlan from resolved session parameters.
let private buildTransformPlan
    (sessionID: string)
    (agent: string)
    (directory: string)
    (p: BacklogProjectionPolicy)
    (caps: CapsInjectionPolicy)
    (par: ParallelHintPolicy)
    (budget: ContextBudgetPolicy)
    (isSub: bool)
    (messagesList: Message<obj> list)
    (messagesArr: obj array)
    (sembleInjectEnabled: bool)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (maxInputTokens: int)
    (client: obj)
    (modelKey: string)
    (limitSource: string)
    : MessageTransformPlan =
    let observeUsage () =
        Wanxiangshu.Runtime.OpencodeContextBudgetObservation.tryObserveLatestUsage client sessionID directory
        |> Promise.map (Option.map (fun observation -> observation))

    let proj =
        if p = BacklogProjectionPolicy.Include then
            ProjectionPolicy.IncludeProjection
        else
            ProjectionPolicy.ExcludeProjection

    { SessionID = sessionID
      Agent = agent
      Directory = directory
      ProjectionPolicy = proj
      BacklogProjectionPolicy = p
      CapsInjectionPolicy = caps
      ParallelHintPolicy = par
      ContextBudgetPolicy = budget
      IsSubagentSession = isSub
      Cleaned = messagesList
      RawArray = Some messagesArr
      SembleInjectEnabled = sembleInjectEnabled
      Scope = runtimeScope
      MaxInputTokens = maxInputTokens
      ModelKey = modelKey
      LimitSource = limitSource
      ObserveLatestUsage = observeUsage }

/// Resolve all transform parameters from raw input and return the plan,
/// capsEpoch, and isSub flag.
let resolveTransformParams
    (registry: ChildAgentRegistry)
    (directory: string)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (client: obj)
    (input: obj)
    (messagesArr: obj array)
    : JS.Promise<MessageTransformPlan * string * bool> =
    promise {
        let messagesList = MessagingCodec.decodeMessages messagesArr
        let agent = resolveMessagesTransformAgent registry input messagesList "build"
        let sessionID = resolveSessionID input messagesList
        let capsEpoch = resolveCapsEpoch messagesList sessionID runtimeScope
        let isSub = registry.ResolveSubsessionParentID(Some sessionID) |> Option.isSome

        let p = getBacklogProjectionPolicy agent isSub
        let caps = getCapsInjectionPolicy agent isSub
        let par = getParallelHintPolicy agent isSub
        let budget = getContextBudgetPolicy agent isSub

        let! maxInputTokens = resolveMaxInputTokens sessionID client directory
        let! modelKey = resolveModelKey client sessionID
        let limitSource = resolveLimitSource ()

        let sembleInjectEnabled =
            p = BacklogProjectionPolicy.Include
            && (agent = "investigator" || agent = "reviewer")

        let plan =
            buildTransformPlan
                sessionID
                agent
                directory
                p
                caps
                par
                budget
                isSub
                messagesList
                messagesArr
                sembleInjectEnabled
                runtimeScope
                maxInputTokens
                client
                modelKey
                limitSource

        return (plan, capsEpoch, isSub)
    }
