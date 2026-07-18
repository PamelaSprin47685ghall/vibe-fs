module Wanxiangshu.Hosts.Omp.OmpMessageTransformPlan

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Runtime.BacklogProjection
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.OmpCaps
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Runtime.ContextBudgetUsageCodec
open Wanxiangshu.Runtime.MessageTransform.HostEntry
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn

let defaultBacklogSession = BacklogSession omp

let configureBacklogSession (cwd: string) : unit =
    defaultBacklogSession.WorkspaceRoot <- cwd

let private resolveAgent (ctx: obj) : string =
    let sm = Dyn.get ctx "sessionManager"

    if Dyn.isNullish sm then
        "manager"
    else
        let name = Dyn.str sm "agentName"
        if name <> "" then name else "manager"

let private resolveMaxInputTokens (sessionId: string) (cwd: string) (ctx: obj) : JS.Promise<int> =
    Wanxiangshu.Runtime.ContextBudgetUsageCodec.resolveMaxInputTokens [ ctx ] sessionId cwd

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
    (observeUsage: unit -> JS.Promise<UsageObservation option>)
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
      ObserveLatestUsage = observeUsage
      ModelKey = "omp:host-unknown"
      LimitSource = "omp:no-model-client" }

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
