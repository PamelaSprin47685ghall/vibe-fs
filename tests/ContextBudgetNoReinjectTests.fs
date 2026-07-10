module Wanxiangshu.Tests.ContextBudgetNoReinjectTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Shell
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Shell.MessageTransformPipeline

let spec_applyContextBudget_noReinject () =
    promise {
        let scope = RuntimeScope.create()
        let plan =
            { SessionID = "sess-nudge-no-reinject"
              Agent = "main"
              Directory = ""
              Excluded = false
              IsSubagentSession = false
              Cleaned = []
              RawArray = None
              SembleInjectEnabled = false
              Scope = scope
              MaxInputTokens = 200000
              GetContextUsage = (fun _ -> Promise.lift (Some 120000)) }

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = (fun _ _ -> []) }

        let state = beginPhase 30000L 100L 0L
        ContextBudgetStore.update scope "sess-nudge-no-reinject" (fun entry ->
            { entry with State = Some state; NudgeInjected = true })

        let msgInfo : MessageInfo<obj> =
            { id = "user-1"
              sessionID = "sess-nudge-no-reinject"
              role = User
              agent = ""
              isError = false
              toolName = ""
              details = null
              time = null }

        let messages = [ { info = msgInfo; parts = []; source = Native; raw = null } ]
        let! res = applyContextBudget plan backlogOps messages [||] (fun _ -> [||])
        equal "should not reinject nudge" 1 res.Length
    }

let run () : JS.Promise<unit> =
    promise {
        do! spec_applyContextBudget_noReinject ()
    }
