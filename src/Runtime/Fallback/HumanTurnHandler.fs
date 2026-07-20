module Wanxiangshu.Runtime.Fallback.HumanTurnHandler

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.StateMachine
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeTransitions
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.NudgeHandler
open Wanxiangshu.Runtime.Fallback.CompactionHandler
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.ContinuationExecution
open Wanxiangshu.Runtime.Fallback.LeaseValidation
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

        // Atomically reset per-turn state, set owner, clear lease gates,
        // and increment turn ordinal + generate new turn ID.
        runtime.Update(sessionID, beginHumanTurn msgId)

        let s = runtime.GetSession sessionID
        let turnId = s.HumanTurnId
        let humanTurnOrdinal = s.HumanTurnOrdinal

        let modelOpt, agentOpt = translator.ExtractRoutingContext rawEvent

        modelOpt
        |> Option.iter (fun m -> runtime.UpdateSession(sessionID, recordLatestHumanModel m))

        if modelOpt.IsNone then
            runtime.UpdateSession(sessionID, clearLatestHumanModel)

        agentOpt
        |> Option.iter (fun a -> runtime.UpdateSession(sessionID, recordAgentName a))

        let provider, model, variant =
            match modelOpt with
            | None -> "", "", ""
            | Some m ->
                match decodeModelFromObj (box m) with
                | Some o -> o.ProviderID, o.ModelID, Option.defaultValue "" o.Variant
                | None -> "", m, ""

        let agent = agentOpt |> Option.defaultValue ""

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

        if msgId = "" || msgId <> (runtime.GetSession sessionID).LastHumanMessageId then
            do! initializeNewTurn translator runtime workspaceRoot sessionID msgId rawEvent

        let state = runtime.GetOrCreateState sessionID

        let ns, _ =
            transition state FallbackEvent.NewUserMessage (configLookup ((runtime.GetSession sessionID).AgentName)) []

        runtime.Update(sessionID, setCore ns)
        runtime.Update(sessionID, recordConsumed false)
        return { Consumed = false; State = ns }, None
    }
