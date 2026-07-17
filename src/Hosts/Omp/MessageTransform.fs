module Wanxiangshu.Hosts.Omp.MessageTransform

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Hosts.Omp
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Runtime.BacklogProjection
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Hosts.Omp.CapsCodec
open Wanxiangshu.Hosts.Omp.ChildSession
open Wanxiangshu.Hosts.Omp.MagicTodo
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Hosts.Omp.ToolResultEvent
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.FileSys
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.HostEntry
open Wanxiangshu.Runtime.MessageTransform.Pipeline
open Wanxiangshu.Runtime.OmpCaps
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.TreeSitterShell
open Wanxiangshu.Runtime.RuntimeScope

module Dyn = Wanxiangshu.Runtime.Dyn

let private defaultBacklogSession = BacklogSession omp

let private maxInputTokensCache =
    System.Collections.Generic.Dictionary<string, int>()

let private resolveAgent (ctx: obj) : string =
    let sm = Dyn.get ctx "sessionManager"

    if Dyn.isNullish sm then
        "manager"
    else
        let name = Dyn.str sm "agentName"
        if name <> "" then name else "manager"

let private resolveMaxInputTokens (sessionId: string) (cwd: string) (ctx: obj) : JS.Promise<int> =
    match maxInputTokensCache.TryGetValue(sessionId) with
    | true, limit -> Promise.lift limit
    | _ ->
        promise {
            let! limit = Wanxiangshu.Runtime.ContextBudgetUsageCodec.resolveMaxInputTokens [ ctx ] sessionId cwd

            maxInputTokensCache.[sessionId] <- limit
            return limit
        }

let private createMessageTransformPlan
    (sessionId: string)
    (agent: string)
    (cwd: string)
    (backlogPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy)
    (capsPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy)
    (parallelHintPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy)
    (contextBudgetPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy)
    (isChild: bool)
    (messagesList: Message<obj> list)
    (entriesArr: obj array)
    (maxInputTokens: int)
    (getContextUsage: obj array -> JS.Promise<int option>)
    : MessageTransformPlan =
    { SessionID = sessionId
      Agent = agent
      Directory = cwd
      ProjectionPolicy =
        (if backlogPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include then
             ProjectionPolicy.IncludeProjection
         else
             ProjectionPolicy.ExcludeProjection)
      BacklogProjectionPolicy = backlogPolicy
      CapsInjectionPolicy = capsPolicy
      ParallelHintPolicy = parallelHintPolicy
      ContextBudgetPolicy = contextBudgetPolicy
      IsSubagentSession = isChild
      Cleaned = messagesList
      RawArray = Some entriesArr
      SembleInjectEnabled = false
      Scope = ExecutorTools.ompScope
      MaxInputTokens = maxInputTokens
      GetContextUsage = getContextUsage }

let private buildLoadCapsFn (plan: MessageTransformPlan) (cwd: string) : unit -> JS.Promise<CapsFile list> =
    fun () ->
        promise {
            let isExcluded =
                match plan.CapsInjectionPolicy with
                | Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Exclude -> true
                | Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Include -> false

            if isExcluded || cwd = "" then
                return ([]: CapsFile list)
            else
                let! ompFiles = findOmpCapsFiles cwd

                let baseFiles =
                    ompFiles
                    |> List.map (fun f ->
                        ({ filePath = f.filePath
                           label = f.label
                           content = f.content }
                        : CapsFile))

                let! injected =
                    Wanxiangshu.Runtime.MessageTransform.HostHooks.injectSubagentFilesIfAny
                        ExecutorTools.ompScope
                        plan
                        baseFiles

                return injected |> List.sortBy (fun cf -> cf.label, cf.filePath)
        }

let private buildCapsFn (plan: MessageTransformPlan) (sessionId: string) (cwd: string) =
    fun encoded (capsFiles: CapsFile list) prelude ->
        let ompCaps =
            capsFiles
            |> List.map (fun f ->
                { filePath = f.filePath
                  label = f.label
                  content = f.content })

        buildCapsEntries sha256HexTruncated sessionId encoded cwd ompCaps prelude

let private buildAndRunTransform
    (reviewStore: ReviewStore)
    (cwd: string)
    (sessionId: string)
    (entriesArr: obj array)
    (agent: string)
    (getContextUsage: obj array -> JS.Promise<int option>)
    (ctx: obj)
    : JS.Promise<obj array> =
    promise {
        let messagesList = decodeEntries sessionId entriesArr
        let isChild = isChildSession ExecutorTools.ompScope sessionId

        let backlogPolicy = getBacklogProjectionPolicy agent isChild
        let capsPolicy = getCapsInjectionPolicy agent isChild
        let parallelHintPolicy = getParallelHintPolicy agent isChild
        let contextBudgetPolicy = getContextBudgetPolicy agent isChild

        defaultBacklogSession.WorkspaceRoot <- cwd

        let backlogOps =
            backlogSessionOpsFrom defaultBacklogSession.Host (fun sid msgs ->
                defaultBacklogSession.GetOrRebuildBacklog(sid, msgs))

        let! maxInputTokens = resolveMaxInputTokens sessionId cwd ctx

        let plan =
            createMessageTransformPlan
                sessionId
                agent
                cwd
                backlogPolicy
                capsPolicy
                parallelHintPolicy
                contextBudgetPolicy
                isChild
                messagesList
                entriesArr
                maxInputTokens
                getContextUsage

        let injectFn _ encoded = Promise.lift encoded
        let loadCaps = buildLoadCapsFn plan cwd
        let buildCaps = buildCapsFn plan sessionId cwd

        return!
            runHostMessagesTransform reviewStore sessionId plan backlogOps encodeMessages injectFn loadCaps buildCaps
    }

let transformEntriesAsyncWithAgent
    (reviewStore: ReviewStore)
    (cwd: string)
    (sessionId: string)
    (entriesObj: obj)
    (agent: string)
    (getContextUsage: obj array -> JS.Promise<int option>)
    (ctx: obj)
    : JS.Promise<obj array> =
    promise {
        if Dyn.isNullish entriesObj || not (Dyn.isArray entriesObj) then
            return unbox<obj array> entriesObj
        else
            let entriesArr = unbox<obj array> entriesObj

            if entriesArr.Length = 0 then
                return entriesArr
            else
                return! buildAndRunTransform reviewStore cwd sessionId entriesArr agent getContextUsage ctx
    }


let transformEntriesAsync
    (reviewStore: ReviewStore)
    (cwd: string)
    (sessionId: string)
    (entriesObj: obj)
    : JS.Promise<obj array> =
    transformEntriesAsyncWithAgent
        reviewStore
        cwd
        sessionId
        entriesObj
        "manager"
        (fun _ -> Promise.lift None)
        (box null)

let beforeAgentStart (_cwd: string) (systemPrompt: obj) : JS.Promise<obj> =
    promise {
        let stripped = stripHostAgentsPrompt systemPrompt
        return createObj [ "systemPrompt", box stripped ]
    }

let appendToolResultSyntax (cwd: string) (event: obj) : JS.Promise<unit> =
    promise {
        let toolName = Dyn.str event "toolName"

        if toolName <> "read" && toolName <> "write" && toolName <> "edit" then
            return ()

        let args = getToolInput event
        let content = getToolResultText event
        let paths = extractFilePaths args

        match paths |> List.tryHead with
        | None -> ()
        | Some path ->
            let! extra = appendSyntaxDiagnostics path content false

            match extra with
            | None -> ()
            | Some diag -> setToolResultText event (content + "\n" + diag)
    }

let registerContextTransform (pi: obj) (reviewStore: ReviewStore) : unit =
    let run (event: obj) (ctx: obj) =
        promise {
            let cwd = Dyn.str ctx "cwd"
            let sessionId = getSessionIdFromContext ctx |> Option.defaultValue ""
            let agent = resolveAgent ctx
            let entries = Dyn.get event "entries"

            if Dyn.isNullish entries then
                return event
            else
                let getContextUsage =
                    match Wanxiangshu.Runtime.ContextBudgetUsageCodec.tryGetRealContextUsage ctx sessionId cwd with
                    | Some f -> f
                    | None -> fun _ -> Promise.lift None

                let! transformed =
                    transformEntriesAsyncWithAgent reviewStore cwd sessionId entries agent getContextUsage ctx

                event?entries <- transformed
                return event
        }

    pi?on ("context", box run)
    pi?on ("before_context", box run)
