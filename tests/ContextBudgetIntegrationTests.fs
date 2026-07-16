module Wanxiangshu.Tests.ContextBudgetIntegrationTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.Pipeline
open Wanxiangshu.Runtime.ContextBudgetUsageCodec
open Wanxiangshu.Tests.ContextBudgetRealApiSpecs

let spec_runMessageTransformPipeline_nudge () =
    promise {
        let scope = RuntimeScope.create ()

        let plan =
            { SessionID = "sess-pipeline-nudge"
              Agent = "main"
              Directory = ""
              ProjectionPolicy = ProjectionPolicy.IncludeProjection
              BacklogProjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include
              CapsInjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Include
              ParallelHintPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Include
              ContextBudgetPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Include
              IsSubagentSession = false
              Cleaned = []
              RawArray = Some [||]
              SembleInjectEnabled = false
              Scope = scope
              MaxInputTokens = 200000
              GetContextUsage = (fun _ -> Promise.lift (Some 120000)) }

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = (fun _ _ -> []) }

        let state = beginPhase 30000L 100L 0L
        ContextBudgetStore.update scope "sess-pipeline-nudge" (fun entry -> { entry with State = Some state })

        let msgInfo: MessageInfo<obj> =
            { id = "user-1"
              sessionID = "sess-pipeline-nudge"
              role = User
              agent = ""
              isError = false
              toolName = ""
              details = null
              time = null }

        let messages =
            [ { info = msgInfo
                parts = []
                source = Native
                raw = null } ]

        let planWithMessages = { plan with Cleaned = messages }

        let encodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray
        let injectFn _ (arr: obj array) = promise { return arr }
        let loadCaps () = promise { return [] }
        let buildCaps (arr: obj array) _ _ = arr

        let! res = runMessageTransformPipeline planWithMessages backlogOps encodeMessages injectFn loadCaps buildCaps
        equal "nudge should be injected in pipeline" 2 res.Length
        let lastMsg = res.[1]
        let idVal = Dyn.get lastMsg "info" |> fun i -> Dyn.str i "id"
        check "nudge prefix" (idVal.StartsWith("context-budget-nudge-"))
    }

/// RED: tryExtractMaxInputTokens must read model.limit.context/input
let spec_tryExtractMaxInputTokens_realSchema () =
    let mkLimit ctx inp outOpt =
        let fields =
            [ "context", box ctx; "output", box outOpt ]
            @ (match inp with
               | Some i -> [ "input", box i ]
               | None -> [])

        createObj fields

    let t1 =
        createObj [ "session", createObj [ "model", createObj [ "limit", mkLimit 128000 (Some 200000) 8000 ] ] ]

    equal "extract limit.input" (Some 200000) (tryExtractMaxInputTokens t1)

    let t2 =
        createObj [ "session", createObj [ "model", createObj [ "limit", mkLimit 128000 None 8000 ] ] ]

    equal "extract limit.context (no input)" (Some 128000) (tryExtractMaxInputTokens t2)

    let t3 = createObj []
    equal "empty obj → None" None (tryExtractMaxInputTokens t3)

    let t4 =
        createObj
            [ "client", createObj [ "session", createObj [ "model", createObj [ "limit", mkLimit 100000 None 4000 ] ] ] ]

    equal "extract client.session.model.limit.context" (Some 100000) (tryExtractMaxInputTokens t4)

let run () : JS.Promise<unit> =
    promise {
        do! spec_runMessageTransformPipeline_nudge ()
        spec_tryExtractMaxInputTokens_realSchema ()
        do! ContextBudgetRealApiSpecs.run ()
    }
