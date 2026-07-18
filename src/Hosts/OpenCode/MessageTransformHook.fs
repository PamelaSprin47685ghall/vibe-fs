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
open Wanxiangshu.Runtime.Dyn

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

let private tryModelLimitFromClientConfig
    (client: obj)
    (sessionID: string)
    (directory: string)
    : JS.Promise<int option> =
    promise {
        if isNullish client then
            printfn "[OpenCode limit] client is null"
            return None
        else
            let configApi = get client "config"
            let configGet = if isNullish configApi then null else get configApi "get"

            if isNullish configGet || not (typeIs configGet "function") then
                printfn "[OpenCode limit] client.config.get missing or not function"
                return None
            else
                try
                    let configArg =
                        createObj [ "query", box (createObj [ "directory", box directory ]) ]

                    printfn "[OpenCode limit] calling client.config.get with directory %s" directory
                    let! configRes = unbox<JS.Promise<obj>> (configApi?get (configArg))

                    printfn
                        "[OpenCode limit] configRes keys: %A"
                        (if isNullish configRes then
                             "null"
                         else
                             String.concat "," (Dyn.keys configRes))

                    let configData = get configRes "data"
                    printfn "[OpenCode limit] configData has provider? %b" (not (isNullish (get configData "provider")))

                    let sessionApi = get client "session"
                    let sessionGet = if isNullish sessionApi then null else get sessionApi "get"

                    if isNullish sessionGet || not (typeIs sessionGet "function") then
                        printfn "[OpenCode limit] client.session.get missing or not function"
                        return None
                    else
                        let sessionArg =
                            createObj
                                [ "path", box (createObj [ "id", box sessionID ])
                                  "query", box (createObj [ "directory", box directory ]) ]

                        printfn "[OpenCode limit] calling client.session.get for %s" sessionID
                        let! sessionRes = unbox<JS.Promise<obj>> (sessionApi?get (sessionArg))
                        printfn "[OpenCode limit] sessionRes ok? %b" (not (isNullish sessionRes))

                        printfn
                            "[OpenCode limit] sessionRes keys: %A"
                            (if isNullish sessionRes then
                                 "null"
                             else
                                 String.concat "," (Dyn.keys sessionRes))

                        let sessionData = get sessionRes "data"

                        printfn
                            "[OpenCode limit] sessionData keys: %A"
                            (if isNullish sessionData then
                                 "null"
                             else
                                 String.concat "," (Dyn.keys sessionData))

                        let modelObj = get sessionData "model"

                        printfn
                            "[OpenCode limit] modelObj keys: %A"
                            (if isNullish modelObj then
                                 "null"
                             else
                                 String.concat "," (Dyn.keys modelObj))

                        let providerID = str modelObj "providerID"
                        let modelID = str modelObj "id"
                        printfn "[OpenCode limit] providerID=%s modelID=%s" providerID modelID

                        let providers = get configData "provider"

                        let providerObj =
                            if isNullish providers then
                                null
                            else
                                get providers providerID

                        let models = get providerObj "models"
                        let modelDef = if isNullish models then null else get models modelID

                        printfn
                            "[OpenCode limit] providerObj null? %b; modelDef null? %b"
                            (isNullish providerObj)
                            (isNullish modelDef)

                        let limitObj = get modelDef "limit"

                        if isNullish limitObj then
                            printfn "[OpenCode limit] modelDef.limit is null"
                            return None
                        else
                            let inputVal = get limitObj "input"
                            let ctxVal = get limitObj "context"
                            let raw = if isNullish inputVal then ctxVal else inputVal

                            if isNullish raw || not (typeIs raw "number") then
                                printfn "[OpenCode limit] raw limit is null or not number: %A" raw
                                return None
                            else
                                let limit = int (unbox<float> raw)
                                printfn "[OpenCode limit] found limit %d" limit
                                return Some limit
                with ex ->
                    printfn "[OpenCode limit] exception: %s" (string ex.Message)
                    return None
    }

let private resolveMaxInputTokens (sessionID: string) (client: obj) (directory: string) : JS.Promise<int> =
    promise {
        let! limitOpt = tryModelLimitFromClientConfig client sessionID directory

        match limitOpt with
        | Some limit ->
            printfn "[OpenCode limit] resolved from config: %d" limit

            let limitTarget =
                createObj [ "model", box (createObj [ "limit", box (createObj [ "input", box limit ]) ]) ]

            return!
                Wanxiangshu.Runtime.ContextBudgetUsageCodec.resolveMaxInputTokens
                    [ limitTarget; client ]
                    sessionID
                    directory
        | None ->
            printfn "[OpenCode limit] no config limit; using fallback"
            return! Wanxiangshu.Runtime.ContextBudgetUsageCodec.resolveMaxInputTokens [ client ] sessionID directory
    }

/// Build the BacklogSessionOps value for a host-messages-transform run.
let private buildBacklogOps (backlogSession: BacklogSession) : BacklogSessionOps =
    backlogSessionOpsFrom backlogSession.Host (fun sid msgs -> backlogSession.GetOrRebuildBacklog(sid, msgs))

/// Build the injection function: takes a policy and encoded messages, either
/// passes messages through or injects Semble annotations.
let private buildInjectionFn
    (directory: string)
    (agent: string)
    (sessionID: string)
    : (BacklogProjectionPolicy -> obj array -> JS.Promise<obj array>) =
    fun policy encoded ->
        match policy with
        | BacklogProjectionPolicy.Exclude -> Promise.lift encoded
        | BacklogProjectionPolicy.Include -> injectSembleIntoEncoded directory agent sessionID encoded

/// Build the load-caps async function for the transform pipeline.
let private buildLoadCaps
    (registry: ChildAgentRegistry)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
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
let private buildCapsFn
    (capsEpoch: string)
    (directory: string)
    : (obj array -> CapsFile list -> string option -> obj array) =
    fun encoded capsFiles preludeOpt ->
        buildCapsMessages
            Wanxiangshu.Runtime.FileSys.sha256HexTruncated
            capsEpoch
            encoded
            directory
            capsFiles
            preludeOpt

/// Run the host-messages transform pipeline and replace the messages array
/// in-place with the final result.
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
        let backlogOps = buildBacklogOps backlogSession
        let injectFn = buildInjectionFn directory agent sessionID
        let loadCaps = buildLoadCaps registry runtimeScope sessionID plan
        let buildCaps = buildCapsFn capsEpoch directory

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

    { SessionID = sessionID
      Agent = agent
      Directory = directory
      ProjectionPolicy =
        if p = BacklogProjectionPolicy.Include then
            ProjectionPolicy.IncludeProjection
        else
            ProjectionPolicy.ExcludeProjection
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
let private resolveTransformParams
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

/// Transform the messages array in-place: resolve session context, build the
/// transform plan, and run the host-messages transform pipeline.
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
            let! (plan, capsEpoch, isSub) =
                resolveTransformParams registry directory runtimeScope client input messagesArr

            let agent = plan.Agent
            let sessionID = plan.SessionID
            let messagesList = plan.Cleaned
            let sembleInjectEnabled = plan.SembleInjectEnabled
            let maxInputTokens = plan.MaxInputTokens

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
                    sembleInjectEnabled
                    maxInputTokens
                    client
                    capsEpoch
                    plan
    }
