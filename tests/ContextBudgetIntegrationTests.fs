module Wanxiangshu.Tests.ContextBudgetIntegrationTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Shell
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Shell.MessageTransformPipeline

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

let run () : JS.Promise<unit> =
    promise {
        do! spec_runMessageTransformPipeline_nudge ()
    }
