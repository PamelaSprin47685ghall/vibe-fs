// ARCHITECTURE_EXEMPT: 356-line file, needs splitting
module Wanxiangshu.Runtime.Fallback.FallbackEventBridge

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.StateMachine
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Runtime.Fallback.ModelInjection
open Wanxiangshu.Runtime.Fallback.FallbackConfigCodec
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime.EventLogRuntimeAppend
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.Fallback.FallbackBridgePorts
open Wanxiangshu.Runtime.Fallback.FallbackBridgeLease
open Wanxiangshu.Runtime.Fallback.FallbackBridgeContinuation
open Wanxiangshu.Runtime.Fallback.FallbackBridgeScanToolText
open Wanxiangshu.Runtime.EventLogAppendSession

let emptyOutputError =
    FallbackEvent.SessionError
        { ErrorName = "EmptyOutputError"
          DomainError = None
          Message = "LLM returned empty output without tools"
          StatusCode = None
          IsRetryable = Some true }

let translateEvent
    (translator: IEventTranslator)
    (executor: IActionExecutor)
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (rawEvent: obj)
    (pendingReview: (string -> bool) option)
    : JS.Promise<FallbackEvent option> =
    match translator.TranslateError rawEvent with
    | Some _ as ev -> promise { return ev }
    | None ->
        if translator.IsNewUserMessage(sessionID, rawEvent) then
            promise { return Some FallbackEvent.NewUserMessage }
        elif translator.IsSessionBusy rawEvent then
            promise { return Some FallbackEvent.SessionBusy }
        elif translator.IsSessionIdle rawEvent then
            promise {
                let state = runtime.GetOrCreateState sessionID

                if state.Lifecycle = FallbackLifecycle.Cancelled then
                    return Some FallbackEvent.SessionIdle
                else
                    let! msgs = executor.FetchMessages sessionID

                    match FallbackMessageCodec.tryGetLastAssistantAbortInfo msgs with
                    | Some abortErr -> return Some(FallbackEvent.SessionError abortErr)
                    | None ->
                        if
                            isIdleNoContentAndNoTools msgs
                            && not (pendingReview |> Option.exists (fun f -> f sessionID))
                        then
                            return Some emptyOutputError
                        else
                            return Some FallbackEvent.SessionIdle
            }
        else
            promise { return None }

let initializeNewTurn
    (translator: IEventTranslator)
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (msgId: string)
    (rawEvent: obj)
    : JS.Promise<unit> =
    promise {
        do! cancelPendingLeasesAndNudges runtime workspaceRoot sessionID
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
        if runtime.GetSessionOwner sessionID = SessionOwner.Compaction then
            let activeComp = runtime.GetActiveCompactionId sessionID

            match runtime.TryGetSettleInfo(sessionID, activeComp) with
            | Some(_, ordinal) ->
                do! appendCompactionSettledOrFail workspaceRoot sessionID activeComp "cancelled" ordinal
                runtime.ApplySettle(sessionID, activeComp) |> ignore
            | None -> ()

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

let executeAction
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (action: FallbackAction)
    (finalState: SessionFallbackState)
    (chain: FallbackModel list)
    : JS.Promise<SessionFallbackState * ContinuationIntent option> =
    promise {
        match action with
        | FallbackAction.DoNothing -> return finalState, None
        | FallbackAction.SendContinue model ->
            return! handleSendContinueAction runtime workspaceRoot sessionID finalState model
        | FallbackAction.RecoverWithPrompt(model, promptText) ->
            return! handleRecoverWithPromptAction runtime workspaceRoot sessionID finalState model promptText
        | FallbackAction.ScanToolCallAsText ->
            return! handleScanToolCallAsText runtime executor workspaceRoot sessionID finalState chain
        | FallbackAction.PropagateFailure -> return finalState, Some PropagateFailureIntent
    }

let handleFallbackTransition // ARCHITECTURE_EXEMPT: function needs splitting
    (translator: IEventTranslator)
    (runtime: FallbackRuntimeStore)
    (configLookup: ConfigLookup)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (evt: FallbackEvent)
    (isMatchedContinuation: bool)
    : JS.Promise<FallbackHookResult * ContinuationIntent option> =
    promise {
        let state = runtime.GetOrCreateState sessionID
        let cfg = configLookup (runtime.GetAgentName sessionID)
        let! chain = resolveChain runtime executor cfg sessionID (runtime.GetAgentName sessionID)

        if List.isEmpty chain && not isMatchedContinuation then
            return { Consumed = false; State = state }, None
        else
            let ns, action = transition state evt cfg chain

            let isAborting =
                match evt with
                | FallbackEvent.SessionError err -> Wanxiangshu.Kernel.FallbackKernel.Decision.errorInputIsAbort err
                | _ -> false

            if isAborting then
                do! handleUserAbort runtime workspaceRoot sessionID

            runtime.UpdateState sessionID ns

            if evt = FallbackEvent.SessionBusy then
                updateBusyLeases runtime sessionID

            let actionFiltered =
                if runtime.GetActiveCompactionId sessionID <> "" || runtime.IsCompacted sessionID then
                    FallbackAction.DoNothing
                else
                    action

            let actionGated =
                match actionFiltered with
                | FallbackAction.SendContinue _ when not cfg.LegacyZeroWidthContinue ->
                    JS.console.warn (
                        $"[Fallback] SendContinue gated for session {sessionID}: legacyZeroWidthContinue is disabled; continuation prompt will not be sent"
                    )

                    FallbackAction.DoNothing
                | _ -> actionFiltered

            let! finalState2, intentOpt = executeAction runtime executor workspaceRoot sessionID actionGated ns chain
            do! handleTerminalPostSettlement runtime workspaceRoot sessionID evt finalState2 intentOpt

            let consumed = calculateConsumed evt state.Phase finalState2.Phase
            runtime.SetConsumed sessionID consumed

            return
                { Consumed = consumed
                  State = finalState2 },
                intentOpt
    }

// ARCHITECTURE_EXEMPT: function at line 237 needs splitting
let handleEvent
    (translator: IEventTranslator)
    (runtime: FallbackRuntimeStore)
    (configLookup: ConfigLookup)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (rawEvent: obj)
    (pendingReview: (string -> bool) option)
    : JS.Promise<FallbackHookResult * ContinuationIntent option> =
    promise {
        let sessionID = translator.ExtractSessionID rawEvent

        let continuationId =
            match translator.ExtractContinuationIdentity rawEvent with
            | Some(cid, _) -> cid
            | None -> ""

        let isAssistantMsg = translator.IsAssistantMessage rawEvent

        let isMatchedContinuation, isEventContIdMatch =
            checkContinuationMatches runtime sessionID continuationId

        if
            isEventContIdMatch
            && (translator.IsSessionBusy rawEvent
                || isAssistantMsg
                || translator.IsSessionIdle rawEvent
                || translator.IsSessionError rawEvent)
        then
            runtime.SetMainContinuationAwaitingStart sessionID false

        let! eventOpt = translateEvent translator executor runtime sessionID rawEvent pendingReview
        let eventTurnIdOpt = translator.ExtractHostRunId rawEvent

        let isStale =
            checkIsStale isEventContIdMatch eventOpt eventTurnIdOpt runtime sessionID

        let eventOpt = if isStale then None else eventOpt

        match eventOpt with
        | None ->
            return
                { Consumed = false
                  State = runtime.GetOrCreateState sessionID },
                None
        | Some evt ->
            let currentState = runtime.GetOrCreateState sessionID

            if isTerminalOrSettled evt currentState runtime sessionID then
                return
                    { Consumed = false
                      State = currentState },
                    None
            elif evt = FallbackEvent.NewUserMessage then
                return! handleNewUserMessage translator runtime configLookup workspaceRoot sessionID rawEvent
            else
                return!
                    handleFallbackTransition
                        translator
                        runtime
                        configLookup
                        executor
                        workspaceRoot
                        sessionID
                        evt
                        isMatchedContinuation
    }

let createHandler
    (translator: IEventTranslator)
    (runtime: FallbackRuntimeStore)
    (configLookup: ConfigLookup)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (pendingReview: (string -> bool) option)
    : (obj -> JS.Promise<FallbackHookResult>) =
    let mutable queues = Map.ofList<string, SerialQueue> []

    fun (rawEvent: obj) ->
        promise {
            let sessionID = translator.ExtractSessionID rawEvent
            // Synchronous pre-filter: skip the SerialQueue entirely when the
            // event cannot produce a FallbackEvent or affect continuation state.
            // This is critical — the queue captures rawEvent in a closure and
            // under high-frequency streaming messages the accumulated Promise
            // nodes retain every raw object, causing O(n²) heap growth.
            let isError = translator.TranslateError rawEvent |> Option.isSome
            let isNewUser = translator.IsNewUserMessage(sessionID, rawEvent)
            let isBusy = translator.IsSessionBusy rawEvent
            let isIdle = translator.IsSessionIdle rawEvent

            let hasContinuation =
                match translator.ExtractContinuationIdentity rawEvent with
                | Some _ -> true
                | None -> false

            if not isError && not isNewUser && not isBusy && not isIdle && not hasContinuation then
                let state = runtime.GetOrCreateState sessionID
                return { Consumed = false; State = state }
            else
                // Relevant event — enqueue for ordered processing.
                let queue =
                    match Map.tryFind sessionID queues with
                    | Some q -> q
                    | None ->
                        let q = SerialQueue()
                        queues <- Map.add sessionID q queues
                        q


                let! (result, intentOpt) =
                    queue.Enqueue(fun () ->
                        handleEvent translator runtime configLookup executor workspaceRoot rawEvent pendingReview)

                match intentOpt with
                | Some intent -> do! executeContinuationIntent runtime executor workspaceRoot sessionID intent
                | None -> ()

                return result
        }
