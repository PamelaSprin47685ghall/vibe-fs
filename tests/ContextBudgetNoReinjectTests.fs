module Wanxiangshu.Tests.ContextBudgetNoReinjectTests

open Wanxiangshu.Runtime.MessageTransform.ContextBudgetEngine
open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.Pipeline

let spec_applyContextBudget_reinjectWhenBudgetStillHot () =
    promise {
        let scope = RuntimeScope.create ()

        let plan =
            { SessionID = "sess-nudge-reinject"
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

        let state = beginCycle 30000L 0 3

        ContextBudgetStore.update scope "sess-nudge-reinject" (fun entry ->
            { entry with
                State = Some state
                NudgeTrack = EmergencySignaled 0
                PendingOutbound = Some { Fingerprint = "seed"; Bytes = 2000 } })

        let msgInfo: MessageInfo<obj> =
            { id = "user-1"
              sessionID = "sess-nudge-reinject"
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

        let bigPayload = String.replicate 60000 "a"
        let encoded = [| box bigPayload |]
        let! res = applyContextBudget plan backlogOps messages encoded
        equal "must reinject after prior round stripped synthetic nudge" 2 res.Length
        equal "nudge source" (Synthetic "context-budget-nudge-") (List.last res).source
    }

let run () : JS.Promise<unit> =
    promise { do! spec_applyContextBudget_reinjectWhenBudgetStillHot () }
