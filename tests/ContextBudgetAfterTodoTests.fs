module Wanxiangshu.Tests.ContextBudgetAfterTodoTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Shell
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Shell.MessageTransformPipeline

let spec_applyContextBudget_afterTodoResets () =
    promise {
        let scope = RuntimeScope.create ()

        let plan =
            { SessionID = "sess-after-todo"
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

        let backlogRef = ref ([]: BacklogEntry list)

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = (fun _ _ -> backlogRef.Value) }

        // Start phase with totalTokens = 30000, totalBytes = 100, backlogBytes = 0
        let state = beginPhase 30000L 100L 0L

        ContextBudgetStore.update scope "sess-after-todo" (fun entry ->
            { entry with
                State = Some state
                LastBacklog = []
                NudgeInjected = true })

        // 1. Simulate new todo: add one entry to backlog
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

        // Since LastTodoCount (0) <> backlogRef length (1), it should begin a new phase and reset nudgeInjected
        let! res = applyContextBudget plan backlogOps messages [||] (fun _ -> [||])
        let updatedStore = ContextBudgetStore.get scope "sess-after-todo"
        equal "last todo count updated" 1 updatedStore.LastBacklog.Length
        equal "nudgeInjected reset" false updatedStore.NudgeInjected
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
              Excluded = false
              IsSubagentSession = false
              Cleaned = []
              RawArray = None
              SembleInjectEnabled = false
              Scope = scope
              MaxInputTokens = int maxTokens
              GetContextUsage = (fun _ -> Promise.lift (Some 120000)) }

        let backlogRef = ref ([]: BacklogEntry list)

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = (fun _ _ -> backlogRef.Value) }

        // Start phase 0
        let mutable currentTokens = 30000L
        let state = beginPhase currentTokens 100L 0L

        ContextBudgetStore.update scope sessionID (fun entry ->
            { entry with
                State = Some state
                LastBacklog = []
                NudgeInjected = false })

        for i in 1..5 do
            // Update plan context usage return value
            let getUsageFun = (fun _ -> Promise.lift (Some(int currentTokens)))

            let currentPlan =
                { plan with
                    GetContextUsage = getUsageFun }

            // 1. Run below threshold
            // b=200000 -> effectiveLimit=150000, s=currentTokens, c=0. F trigger: 2*a >= 150000 + s. -> a >= 75000 + s/2
            let boundary = 75000L + currentTokens / 2L
            let belowTokens = boundary - 5000L

            let planBelow =
                { currentPlan with
                    GetContextUsage = (fun _ -> Promise.lift (Some(int belowTokens))) }

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

            let! resBelow = applyContextBudget planBelow backlogOps messages [||] (fun _ -> [||])
            equal "no nudge below threshold" 1 resBelow.Length

            // 2. Run at trigger threshold
            let triggerTokens = boundary + 5000L
            // Verify trigger token is strictly less than maxInputTokens (a < b)
            check "trigger point a < b" (triggerTokens < maxTokens)

            let planTrigger =
                { currentPlan with
                    GetContextUsage = (fun _ -> Promise.lift (Some(int triggerTokens))) }

            let! resTrigger = applyContextBudget planTrigger backlogOps messages [||] (fun _ -> [||])
            equal "nudge injected at threshold" 2 resTrigger.Length

            // 3. Simulate a successful todowrite committing
            let newTodoEntry =
                { ahaMoments = sprintf "aha-%d" i
                  changesAndReasons = "changes"
                  gotchas = "gotchas"
                  lessonsAndConventions = "lessons"
                  plan = "plan" }

            backlogRef.Value <- backlogRef.Value @ [ newTodoEntry ]

            // Update base prompt tokens for the next phase
            currentTokens <- currentTokens + 5000L

            let nextPlan =
                { plan with
                    GetContextUsage = (fun _ -> Promise.lift (Some(int currentTokens))) }

            let! resTodo = applyContextBudget nextPlan backlogOps messages [||] (fun _ -> [||])

            let updatedStore = ContextBudgetStore.get scope sessionID
            equal "LastTodoCount updated" i updatedStore.LastBacklog.Length
            equal "NudgeInjected reset" false updatedStore.NudgeInjected
    }

let run () : JS.Promise<unit> =
    promise {
        do! spec_applyContextBudget_afterTodoResets ()
        do! spec_applyContextBudget_fiveConsecutiveTodos ()
    }
