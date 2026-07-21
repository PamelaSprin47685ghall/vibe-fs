module Wanxiangshu.Hosts.Opencode.MessageTransformPipelineHelper

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.HostHooks
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Hosts.Opencode.OpenCodeModelResolution
open Wanxiangshu.Hosts.Opencode.ModelKeyResolver
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Hosts.Opencode.SembleInjection
open Wanxiangshu.Hosts.Opencode.CompactionHook
open Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Hosts.Opencode.CapsCodec

let resolveSessionID (input: obj) (messagesList: Message<obj> list) : string =
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

/// Build the injection function: takes a policy and encoded messages, either
/// passes messages through or injects Semble annotations.
let buildInjectionFn
    (directory: string)
    (agent: string)
    (sessionID: string)
    (runtimeScope: RuntimeScope)
    : (BacklogProjectionPolicy -> obj array -> JS.Promise<obj array>) =
    fun policy encoded ->
        match policy with
        | BacklogProjectionPolicy.Exclude -> Promise.lift encoded
        | BacklogProjectionPolicy.Include -> injectSembleIntoEncoded runtimeScope directory agent sessionID encoded

/// Build the load-caps async function for the transform pipeline.
let buildLoadCaps
    (registry: ChildAgentRegistry)
    (runtimeScope: RuntimeScope)
    (sessionID: string)
    (plan: MessageTransformPlan)
    : unit -> JS.Promise<CapsFile list> =
    fun () ->
        promise {
            let parentSessionID =
                registry.ResolveSubsessionParentID(Some sessionID)
                |> Option.defaultValue sessionID

            let! caps =
                loadCapsForScope
                    runtimeScope
                    AllowEmptyDirectory
                    { plan with
                        SessionID = parentSessionID }

            return caps |> List.sortBy (fun cf -> cf.label, cf.filePath)
        }

/// Build the build-caps callback passed to runHostMessagesTransform.
let buildCapsFn (capsEpoch: string) (directory: string) : (obj array -> CapsFile list -> string option -> obj array) =
    fun encoded capsFiles preludeOpt ->
        buildCapsMessages
            Wanxiangshu.Runtime.FileSys.sha256HexTruncated
            capsEpoch
            encoded
            directory
            capsFiles
            preludeOpt

/// Build the MessageTransformPlan from resolved session parameters.
let buildTransformPlan
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
    (runtimeScope: RuntimeScope)
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
    (runtimeScope: RuntimeScope)
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
        let par = getParallelHintPolicy agent
        let budget = getContextBudgetPolicy agent isSub

        let! maxInputTokens = resolveMaxInputTokens sessionID client directory
        let! modelKey = resolveModelKey client sessionID
        let limitSource = resolveLimitSource ()

        let sembleInjectEnabled =
            p = BacklogProjectionPolicy.Include
            && (agent = "inspector" || agent = "reviewer")

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
