module Wanxiangshu.Runtime.Fallback.HumanTurnHandler

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.StateMachine
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.Fallback.HumanTurnTransitions
open Wanxiangshu.Runtime.Fallback.OrdinalTransitions
open Wanxiangshu.Runtime.Fallback.CompactionTransitions
open Wanxiangshu.Runtime.Fallback.ModelInjection
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.ContinuationExecution
open Wanxiangshu.Runtime.Fallback.LeaseValidation
open Wanxiangshu.Runtime.Fallback.NudgeHandler
open Wanxiangshu.Runtime.Fallback.CompactionHandler
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Runtime.SessionEventWriter
open Wanxiangshu.Runtime.Clock

let initializeNewTurn
    (translator: IEventTranslator)
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (msgId: string)
    (rawEvent: obj)
    : JS.Promise<unit> =
    promise {
        do! cancelPendingMainLease runtime workspaceRoot sessionID "New user message"
        do! cancelPendingNudge runtime workspaceRoot sessionID "New user message"

        runtime.SetChain sessionID []
        runtime.ClearModel sessionID
        runtime.ClearInjected sessionID
        runtime.SetSessionOwner sessionID SessionOwner.Human
        runtime.SetLastHumanMessageId sessionID msgId
        runtime.RemoveForceStopped sessionID
        let turnId = runtime.IncrementHumanTurnId sessionID
        runtime.SetActiveContinuationGeneration sessionID (runtime.GetSessionGeneration sessionID)
        runtime.SetActiveContinuationCancelGeneration sessionID (runtime.GetCancelGeneration sessionID)

        let modelOpt, agentOpt = translator.ExtractRoutingContext rawEvent
        modelOpt |> Option.iter (runtime.SetLatestHumanModel sessionID)

        if modelOpt.IsNone then
            runtime.ClearLatestHumanModel sessionID

        agentOpt |> Option.iter (runtime.SetAgentName sessionID)

        let provider, model, variant =
            match modelOpt with
            | None -> "", "", ""
            | Some m ->
                match decodeModelFromObj (box m) with
                | Some o -> o.ProviderID, o.ModelID, Option.defaultValue "" o.Variant
                | None -> "", m, ""

        let agent = agentOpt |> Option.defaultValue ""
        let humanTurnOrdinal = runtime.GetHumanTurnOrdinal sessionID

        do!
            appendHumanTurnStartedOrFail
                workspaceRoot
                sessionID
                turnId
                provider
                model
                variant
                agent
                humanTurnOrdinal
                msgId
    }

let handleNewUserMessage
    (translator: IEventTranslator)
    (runtime: FallbackRuntimeStore)
    (configLookup: ConfigLookup)
    (workspaceRoot: string)
    (sessionID: string)
    (rawEvent: obj)
    : JS.Promise<FallbackHookResult * ContinuationIntent option> =
    promise {
        do! settleActiveCompactionIfOwner runtime workspaceRoot sessionID

        let msgId = translator.ExtractNewUserMessageId rawEvent |> Option.defaultValue ""

        if msgId = "" || msgId <> runtime.GetLastHumanMessageId sessionID then
            do! initializeNewTurn translator runtime workspaceRoot sessionID msgId rawEvent

        let state = runtime.GetOrCreateState sessionID

        let ns, _ =
            transition state FallbackEvent.NewUserMessage (configLookup (runtime.GetAgentName sessionID)) []

        runtime.UpdateState sessionID ns
        runtime.SetConsumed sessionID false
        return { Consumed = false; State = ns }, None
    }
