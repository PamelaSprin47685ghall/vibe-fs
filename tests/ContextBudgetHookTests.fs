module Wanxiangshu.Tests.ContextBudgetHookTests

open Wanxiangshu.Runtime.MessageTransform.ContextBudgetEngine
open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.Pipeline

let spec_applyContextBudget_injectsNudge () =
    promise {
        let scope = RuntimeScope.create ()

        let plan =
            { SessionID = "sess-nudge"
              Agent = "main"
              Directory = ""
              ProjectionPolicy = ProjectionPolicy.IncludeProjection
              BacklogProjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include
              CapsInjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Include
              ParallelHintPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Include
              ContextBudgetPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Include
              IsSubagentSession = false
              Cleaned = []
              RawArray = None
              SembleInjectEnabled = false
              Scope = scope
              MaxInputTokens = 200000
              ModelKey = "openai/gpt-4o:default"
              LimitSource = "openai-session-model"
              ObserveLatestUsage =
                fun () ->
                    Promise.lift (
                        Some
                            { AssistantMessageID = "test"
                              InputTokens = 120000L }
                    ) }

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = (fun _ _ -> []) }

        let state = beginPhase 30000L 100L 0L
        ContextBudgetStore.update scope "sess-nudge" (fun entry -> { entry with State = Some state })

        let msgInfo: MessageInfo<obj> =
            { id = "user-1"
              sessionID = "sess-nudge"
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

        let! res = applyContextBudget plan backlogOps messages [||]
        equal "should inject nudge" 2 res.Length
        let lastMsg = List.last res
        equal "last msg source" (Synthetic "context-budget-nudge-") lastMsg.source
        equal "last msg role" User lastMsg.info.role
        check "last msg contains nudge text" ((partTextStr lastMsg.parts.Head).Contains("suspended"))
    }

let run () : JS.Promise<unit> =
    promise { do! spec_applyContextBudget_injectsNudge () }
