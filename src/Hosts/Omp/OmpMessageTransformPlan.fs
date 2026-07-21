module Wanxiangshu.Hosts.Omp.OmpMessageTransformPlan

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.CapsCodec
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.OmpCaps
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.FileSys
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn

let resolveAgent (ctx: obj) : string =
    let sm = Dyn.get ctx "sessionManager"

    if Dyn.isNullish sm then
        "manager"
    else
        let name = Dyn.str sm "agentName"
        if name <> "" then name else "manager"

let resolveMaxInputTokens (_sessionId: string) (_cwd: string) (_ctx: obj) : JS.Promise<int> = Promise.lift 8192

let createMessageTransformPlan
    (sessionId: string)
    (agent: string)
    (cwd: string)
    (capsPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy)
    (parallelHintPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy)
    (isChild: bool)
    (messagesList: Message<obj> list)
    (entriesArr: obj array)
    (maxInputTokens: int)
    : MessageTransformPlan =
    { SessionID = sessionId
      Agent = agent
      Directory = cwd
      ProjectionPolicy = projectionPolicyForAgent agent isChild
      CapsInjectionPolicy = capsPolicy
      ParallelHintPolicy = parallelHintPolicy
      IsSubagentSession = isChild
      Cleaned = messagesList
      RawArray = Some entriesArr
      SembleInjectEnabled = false
      Scope = ExecutorTools.ompScope
      MaxInputTokens = maxInputTokens
      ObserveLatestUsage = fun () -> Promise.lift ()
      ModelKey = "omp:host-unknown"
      LimitSource = "omp:no-model-client" }

let buildLoadCapsFn (plan: MessageTransformPlan) (cwd: string) : unit -> JS.Promise<CapsFile list> =
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

let buildCapsFn (plan: MessageTransformPlan) (sessionId: string) (cwd: string) =
    fun encoded (capsFiles: CapsFile list) prelude ->
        let ompCaps =
            capsFiles
            |> List.map (fun f ->
                ({ filePath = f.filePath
                   label = f.label
                   content = f.content }
                : OmpCapsFile))

        buildCapsEntries sha256HexTruncated sessionId encoded cwd ompCaps prelude
