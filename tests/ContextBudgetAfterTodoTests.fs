module Wanxiangshu.Tests.ContextBudgetAfterTodoTests

open Wanxiangshu.Runtime.MessageTransform.ContextBudgetEngine
open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Kernel.Backlog.BacklogTypes
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.Pipeline

let spec_applyContextBudget_afterTodoResets () =
    promise {
        let scope = RuntimeScope.create ()

        let plan =
            { SessionID = "sess-after-todo"
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
                            { AssistantMessageID = "test-20000"
                              InputTokens = 20000L }
                    ) }

        let backlogRef = ref ([]: BacklogEntry list)

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = (fun _ _ -> backlogRef.Value) }

        let state = beginCycle 30000L 0 3

        ContextBudgetStore.update scope "sess-after-todo" (fun entry ->
            { entry with
                State = Some state
                LastBacklog = []
                NudgeTrack = EmergencySignaled 0 })

        let newTodoEntry =
            { ahaMoments = "aha"
              changesAndReasons = "changes"
              gotchas = "gotchas"
              lessonsAndConventions = "lessons"
              plan = "plan" }

        backlogRef.Value <- [ newTodoEntry ]

        let msgInfo: MessageInfo<obj> =
            { id = "user-1"
              sessionID = "sess-after-todo"
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

        let! _res = applyContextBudget plan backlogOps messages [||]
        let updatedStore = ContextBudgetStore.get scope "sess-after-todo"
        equal "last todo count updated" 1 updatedStore.LastBacklog.Length
        equal "nudge track preserved after backlog change" (EmergencySignaled 0) updatedStore.NudgeTrack
    }

let spec_applyContextBudget_fiveConsecutiveTodos () =
    promise {
        let scope = RuntimeScope.create ()
        let maxTokens = 200000L
        let sessionID = "sess-five-todos"

        let plan =
            { SessionID = sessionID
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
              MaxInputTokens = int maxTokens
              ModelKey = "openai/gpt-4o:default"
              LimitSource = "openai-session-model"
              ObserveLatestUsage =
                fun () ->
                    Promise.lift (
                        Some
                            { AssistantMessageID = "test"
                              InputTokens = 120000L }
                    ) }

        let backlogRef = ref ([]: BacklogEntry list)

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = (fun _ _ -> backlogRef.Value) }

        let mutable phaseBase = 30000L
        let state = beginCycle phaseBase 0 3

        ContextBudgetStore.update scope sessionID (fun entry ->
            { entry with
                State = Some state
                LastBacklog = []
                NudgeTrack = Idle })

        let bEff = effectiveMaxInputTokens (int maxTokens)
        let N = 3

        for i in 1..5 do
            let boundary = (bEff + int64 N * phaseBase) / int64 (N + 1)
            let belowTokens = boundary - 5000L

            let planBelow =
                { plan with
                    ObserveLatestUsage =
                        fun () ->
                            Promise.lift (
                                Some
                                    { AssistantMessageID = "test"
                                      InputTokens = belowTokens }
                            ) }

            let messages =
                [ { info =
                      { id = "msg-below"
                        sessionID = sessionID
                        role = User
                        agent = ""
                        isError = false
                        toolName = ""
                        details = null
                        time = null }
                    parts = []
                    source = Native
                    raw = null } ]

            let! resBelow = applyContextBudget planBelow backlogOps messages [||]
            equal "no nudge below threshold" 1 resBelow.Length

            let triggerTokens = boundary + 5000L
            check "trigger point a < bEff" (triggerTokens < bEff)

            let planTrigger =
                { plan with
                    ObserveLatestUsage =
                        fun () ->
                            Promise.lift (
                                Some
                                    { AssistantMessageID = "test"
                                      InputTokens = triggerTokens }
                            ) }

            let! resTrigger = applyContextBudget planTrigger backlogOps messages [||]
            equal "nudge injected at threshold" 2 resTrigger.Length

            let newTodoEntry =
                { ahaMoments = sprintf "aha-%d" i
                  changesAndReasons = "changes"
                  gotchas = "gotchas"
                  lessonsAndConventions = "lessons"
                  plan = "plan" }

            backlogRef.Value <- backlogRef.Value @ [ newTodoEntry ]

            let lowPlan =
                { plan with
                    ObserveLatestUsage =
                        fun () ->
                            Promise.lift (
                                Some
                                    { AssistantMessageID = "test"
                                      InputTokens = belowTokens }
                            ) }

            let! _resTodo = applyContextBudget lowPlan backlogOps messages [||]

            let updatedStore = ContextBudgetStore.get scope sessionID
            equal "LastTodoCount updated" i updatedStore.LastBacklog.Length
            equal "NudgeTrack preserved after todo" (EmergencySignaled 0) updatedStore.NudgeTrack
            phaseBase <- updatedStore.State.Value.BaselineTokens
    }

let run () : JS.Promise<unit> =
    promise {
        do! spec_applyContextBudget_afterTodoResets ()
        do! spec_applyContextBudget_fiveConsecutiveTodos ()
    }
