module Wanxiangshu.Hosts.Omp.MessageTransform

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
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Hosts.Omp.OmpMessageTransformPlan

module Dyn = Wanxiangshu.Runtime.Dyn

let private buildAndRunTransform
    (reviewStore: ReviewStore)
    (cwd: string)
    (sessionId: string)
    (entriesArr: obj array)
    (agent: string)
    (observeUsage: unit -> JS.Promise<UsageObservation option>)
    (ctx: obj)
    : JS.Promise<obj array> =
    promise {
        let messagesList = decodeEntries sessionId entriesArr
        let isChild = isChildSession ExecutorTools.ompScope sessionId

        let backlogPolicy = getBacklogProjectionPolicy agent isChild
        let capsPolicy = getCapsInjectionPolicy agent isChild
        let parallelHintPolicy = getParallelHintPolicy agent isChild
        let contextBudgetPolicy = getContextBudgetPolicy agent isChild

        configureBacklogSession cwd

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
                observeUsage

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
    (observeUsage: unit -> JS.Promise<UsageObservation option>)
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
                return! buildAndRunTransform reviewStore cwd sessionId entriesArr agent observeUsage ctx
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
        (fun () -> Promise.lift None)
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
                let observeUsage =
                    match Wanxiangshu.Runtime.ContextBudgetUsageCodec.tryGetRealContextUsage ctx sessionId cwd with
                    | Some f -> f
                    | None -> fun () -> Promise.lift None

                let! transformed =
                    transformEntriesAsyncWithAgent reviewStore cwd sessionId entries agent observeUsage ctx

                event?entries <- transformed
                return event
        }

    pi?on ("context", box run)
    pi?on ("before_context", box run)
