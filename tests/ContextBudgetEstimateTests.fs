module Wanxiangshu.Tests.ContextBudgetEstimateTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Shell
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Shell.MessageTransformPipeline

let spec_applyContextBudget_estimatesFromLastUsageWhenApiMissing () =
    promise {
        let scope = RuntimeScope.create ()
        let sessionID = "sess-estimate-nudge"

        let plan =
            { SessionID = sessionID
              Agent = "main"
              Directory = ""
              ProjectionPolicy = ProjectionPolicy.IncludeProjection
              IsSubagentSession = false
              Cleaned = []
              RawArray = None
              SembleInjectEnabled = false
              Scope = scope
              MaxInputTokens = 150000
              GetContextUsage = fun _ -> Promise.lift None }

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = fun _ _ -> [] }

        let state = beginPhase 30000L 100L 0L

        ContextBudgetStore.update scope sessionID (fun entry ->
            { entry with
                State = Some state
                LastUsage =
                    Some
                        {| tokenCount = 60000
                           textBytes = 10000 |} })

        let msgInfo: MessageInfo<obj> =
            { id = "user-1"
              sessionID = sessionID
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

        let bigPayload = String.replicate 20000 "x"
        let encoded = [| box bigPayload |]

        let encodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray

        let! res = applyContextBudget plan backlogOps messages encoded encodeMessages
        equal "estimate path should inject nudge" 2 res.Length
        equal "nudge source" (Synthetic "context-budget-nudge-") (List.last res).source
    }

let spec_applyContextBudget_seedsLastUsageAfterLiveRead () =
    promise {
        let scope = RuntimeScope.create ()
        let sessionID = "sess-seed-usage"
        let mutable getCalls = 0

        let getUsage (_: obj array) =
            promise {
                getCalls <- getCalls + 1

                if getCalls = 1 then return Some 80000 else return None
            }

        let plan =
            { SessionID = sessionID
              Agent = "main"
              Directory = ""
              ProjectionPolicy = ProjectionPolicy.IncludeProjection
              IsSubagentSession = false
              Cleaned = []
              RawArray = None
              SembleInjectEnabled = false
              Scope = scope
              MaxInputTokens = 150000
              GetContextUsage = getUsage }

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = fun _ _ -> [] }

        let state = beginPhase 30000L 100L 0L

        ContextBudgetStore.update scope sessionID (fun entry -> { entry with State = Some state })

        let msgInfo: MessageInfo<obj> =
            { id = "user-1"
              sessionID = sessionID
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

        let encodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray
        let encoded1 = [| box "small" |]
        let! _ = applyContextBudget plan backlogOps messages encoded1 encodeMessages

        let storeAfterLive = ContextBudgetStore.get scope sessionID
        check "LastUsage seeded after live read" storeAfterLive.LastUsage.IsSome

        let bigPayload = String.replicate 20000 "y"
        let encoded2 = [| box bigPayload |]
        let! res2 = applyContextBudget plan backlogOps messages encoded2 encodeMessages
        equal "second pass estimates from seeded ratio and may nudge" 2 res2.Length
    }

let spec_applyContextBudget_emptyBacklogInitialPhase_injects () =
    promise {
        let scope = RuntimeScope.create ()
        let sessionID = "sess-initial-phase"

        let plan =
            { SessionID = sessionID
              Agent = "main"
              Directory = ""
              ProjectionPolicy = ProjectionPolicy.IncludeProjection
              IsSubagentSession = false
              Cleaned = []
              RawArray = None
              SembleInjectEnabled = false
              Scope = scope
              MaxInputTokens = 150000
              GetContextUsage = fun _ -> Promise.lift (Some 120000) }

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = fun _ _ -> [] }

        let msgInfo: MessageInfo<obj> =
            { id = "user-1"
              sessionID = sessionID
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

        let encoded = [| box "payload" |]
        let encodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray

        let! res = applyContextBudget plan backlogOps messages encoded encodeMessages
        equal "empty backlog initial phase injects nudge at high usage" 2 res.Length
    }

let run () : JS.Promise<unit> =
    promise {
        do! spec_applyContextBudget_estimatesFromLastUsageWhenApiMissing ()
        do! spec_applyContextBudget_seedsLastUsageAfterLiveRead ()
        do! spec_applyContextBudget_emptyBacklogInitialPhase_injects ()
    }
