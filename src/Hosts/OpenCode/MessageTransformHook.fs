module Wanxiangshu.Hosts.Opencode.MessageTransformHook

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

open Wanxiangshu.Runtime.JsArrayMutate
open Wanxiangshu.Hosts.Opencode.SembleInjection
open Wanxiangshu.Hosts.Opencode.CompactionHook

let private maxInputTokensCache =
    System.Collections.Generic.Dictionary<string, int>()

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
    match maxInputTokensCache.TryGetValue(sessionID) with
    | true, limit -> Promise.lift limit
    | _ ->
        promise {
            let! limit =
                Wanxiangshu.Runtime.ContextBudgetUsageCodec.resolveMaxInputTokens [ client ] sessionID directory

            maxInputTokensCache.[sessionID] <- limit
            return limit
        }

// ARCHITECTURE_EXEMPT: split this 64-line function later
let private runHostMessagesTransformExecution
    (registry: ChildAgentRegistry)
    (directory: string)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (backlogSession: BacklogSession)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (sessionID: string)
    (agent: string)
    (isSub: bool)
    (messagesList: Message<obj> list)
    (messagesArr: obj array)
    (sembleInjectEnabled: bool)
    (maxInputTokens: int)
    (client: obj)
    (capsEpoch: string)
    (plan: MessageTransformPlan)
    : JS.Promise<unit> =
    promise {
        let backlogOps =
            backlogSessionOpsFrom backlogSession.Host (fun sid msgs -> backlogSession.GetOrRebuildBacklog(sid, msgs))

        let injectFn policy encoded =
            match policy with
            | BacklogProjectionPolicy.Exclude -> Promise.lift encoded
            | BacklogProjectionPolicy.Include -> injectSembleIntoEncoded directory agent sessionID encoded

        let loadCaps () =
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

        let buildCaps encoded capsFiles prelude =
            buildCapsMessages
                Wanxiangshu.Runtime.FileSys.sha256HexTruncated
                capsEpoch
                encoded
                directory
                capsFiles
                prelude

        let! final =
            runHostMessagesTransform
                reviewStore
                sessionID
                plan
                backlogOps
                MessagingCodec.encodeMessages
                injectFn
                loadCaps
                buildCaps

        replaceArrayInPlace messagesArr final
    }

// ARCHITECTURE_EXEMPT: split this 81-line function later
let messagesTransform
    (registry: ChildAgentRegistry)
    (directory: string)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (backlogSession: BacklogSession)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (client: obj)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        runtimeScope.TriggerInit(directory)
        do! runtimeScope.WaitInit()

        match tryGetMessagesArrayFromOutput output with
        | None -> ()
        | Some messagesArr ->
            let messagesList = MessagingCodec.decodeMessages messagesArr
            let agent = resolveMessagesTransformAgent registry input messagesList "build"
            let sessionID = resolveSessionID input messagesList
            let capsEpoch = resolveCapsEpoch messagesList sessionID runtimeScope
            let isSub = registry.ResolveSubsessionParentID(Some sessionID) |> Option.isSome

            let p =
                Wanxiangshu.Kernel.MessageTransformPolicy.getBacklogProjectionPolicy agent isSub

            let caps =
                Wanxiangshu.Kernel.MessageTransformPolicy.getCapsInjectionPolicy agent isSub

            let par =
                Wanxiangshu.Kernel.MessageTransformPolicy.getParallelHintPolicy agent isSub

            let budget =
                Wanxiangshu.Kernel.MessageTransformPolicy.getContextBudgetPolicy agent isSub

            let! maxInputTokens = resolveMaxInputTokens sessionID client directory

            let plan =
                { SessionID = sessionID
                  Agent = agent
                  Directory = directory
                  ProjectionPolicy =
                    (if p = BacklogProjectionPolicy.Include then
                         ProjectionPolicy.IncludeProjection
                     else
                         ProjectionPolicy.ExcludeProjection)
                  BacklogProjectionPolicy = p
                  CapsInjectionPolicy = caps
                  ParallelHintPolicy = par
                  ContextBudgetPolicy = budget
                  IsSubagentSession = isSub
                  Cleaned = messagesList
                  RawArray = Some messagesArr
                  SembleInjectEnabled =
                    (p = BacklogProjectionPolicy.Include
                     && (agent = "investigator" || agent = "reviewer"))
                  Scope = runtimeScope
                  MaxInputTokens = maxInputTokens
                  GetContextUsage =
                    (fun encoded ->
                        Wanxiangshu.Runtime.OpencodeContextBudgetObservation.tryCurrentUsage client sessionID encoded) }

            do!
                runHostMessagesTransformExecution
                    registry
                    directory
                    runtimeScope
                    backlogSession
                    reviewStore
                    sessionID
                    agent
                    isSub
                    messagesList
                    messagesArr
                    (p = BacklogProjectionPolicy.Include
                     && (agent = "investigator" || agent = "reviewer"))
                    maxInputTokens
                    client
                    capsEpoch
                    plan
    }
