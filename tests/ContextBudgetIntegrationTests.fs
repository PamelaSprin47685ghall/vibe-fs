module Wanxiangshu.Tests.ContextBudgetIntegrationTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Shell
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Shell.MessageTransformPipeline
open Wanxiangshu.Shell.ContextBudgetUsageCodec

let spec_runMessageTransformPipeline_nudge () =
    promise {
        let scope = RuntimeScope.create()
        let plan =
            { SessionID = "sess-pipeline-nudge"
              Agent = "main"
              Directory = ""
              Excluded = false
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
        ContextBudgetStore.update scope "sess-pipeline-nudge" (fun entry ->
            { entry with State = Some state })

        let msgInfo : MessageInfo<obj> =
            { id = "user-1"
              sessionID = "sess-pipeline-nudge"
              role = User
              agent = ""
              isError = false
              toolName = ""
              details = null
              time = null }

        let messages = [ { info = msgInfo; parts = []; source = Native; raw = null } ]
        let planWithMessages = { plan with Cleaned = messages }

        let encodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray
        let injectFn _ (arr: obj array) = promise { return arr }
        let loadCaps () = promise { return [] }
        let buildCaps (arr: obj array) _ _ = arr

        let! res = runMessageTransformPipeline planWithMessages backlogOps encodeMessages injectFn loadCaps buildCaps
        equal "nudge should be injected in pipeline" 2 res.Length
        let lastMsg = res.[1]
        let idVal = Dyn.get lastMsg "info" |> fun info -> Dyn.str info "id"
        check "last msg id starts with nudge prefix" (idVal.StartsWith("context-budget-nudge-"))
    }

let spec_tryExtractMaxInputTokens () =
    let t1 = Fable.Core.JsInterop.createObj [ "maxInputTokens", box 50000 ]
    equal "extract t1" (Some 50000) (tryExtractMaxInputTokens t1)
    
    let t2 = Fable.Core.JsInterop.createObj [ "session", Fable.Core.JsInterop.createObj [ "maxInputTokens", box 100000 ] ]
    equal "extract t2" (Some 100000) (tryExtractMaxInputTokens t2)

    let t3 = Fable.Core.JsInterop.createObj [ "client", Fable.Core.JsInterop.createObj [ "session", Fable.Core.JsInterop.createObj [ "maxInputTokens", box 120000 ] ] ]
    equal "extract t3" (Some 120000) (tryExtractMaxInputTokens t3)

    let t4 = Fable.Core.JsInterop.createObj [ "session", Fable.Core.JsInterop.createObj [ "model", Fable.Core.JsInterop.createObj [ "maxInputTokens", box 150000 ] ] ]
    equal "extract t4" (Some 150000) (tryExtractMaxInputTokens t4)

    let t5 = Fable.Core.JsInterop.createObj [ "session", Fable.Core.JsInterop.createObj [ "model", Fable.Core.JsInterop.createObj [ "contextWindow", box 180000 ] ] ]
    equal "extract t5" (Some 180000) (tryExtractMaxInputTokens t5)

    let t6 = Fable.Core.JsInterop.createObj []
    equal "extract t6" None (tryExtractMaxInputTokens t6)

let spec_tryGetMaxInputTokensAsync () =
    promise {
        let mockGet (arg: obj) =
            promise {
                let res = Fable.Core.JsInterop.createObj [ "data", Fable.Core.JsInterop.createObj [ "model", Fable.Core.JsInterop.createObj [ "maxInputTokens", box 80000 ] ] ]
                return res
            }
        let t = Fable.Core.JsInterop.createObj [ "session", Fable.Core.JsInterop.createObj [ "get", box mockGet ] ]
        let! limit = tryGetMaxInputTokensAsync t "sess-id"
        equal "async extract" (Some 80000) limit

        let t2 = Fable.Core.JsInterop.createObj []
        let! limit2 = tryGetMaxInputTokensAsync t2 "sess-id"
        equal "async extract fallback" None limit2
    }

let spec_resolveMaxInputTokens () =
    promise {
        let t1 = Fable.Core.JsInterop.createObj []
        let! res1 = resolveMaxInputTokens [ t1 ] "sess-id"
        equal "resolve fallback" 200000 res1

        let t2 = Fable.Core.JsInterop.createObj [ "maxInputTokens", box 60000 ]
        let! res2 = resolveMaxInputTokens [ t1; t2 ] "sess-id"
        equal "resolve priority" 60000 res2
    }

let run () : JS.Promise<unit> =
    promise {
        do! spec_runMessageTransformPipeline_nudge ()
        spec_tryExtractMaxInputTokens ()
        do! spec_tryGetMaxInputTokensAsync ()
        do! spec_resolveMaxInputTokens ()
    }
