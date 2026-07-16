module Wanxiangshu.Tests.ContextBudgetNoReinjectTests

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
              GetContextUsage = (fun _ -> Promise.lift (Some 120000)) }

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = (fun _ _ -> []) }

        let state = beginPhase 30000L 100L 0L

        ContextBudgetStore.update scope "sess-nudge-reinject" (fun entry ->
            { entry with
                State = Some state
                NudgeTrack = EmergencySignaled })

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

        let! res = applyContextBudget plan backlogOps messages [||] (fun _ -> [||])
        equal "must reinject after prior round stripped synthetic nudge" 2 res.Length
        equal "nudge source" (Synthetic "context-budget-nudge-") (List.last res).source
    }

let run () : JS.Promise<unit> =
    promise { do! spec_applyContextBudget_reinjectWhenBudgetStillHot () }
