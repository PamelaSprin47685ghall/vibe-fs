module Wanxiangshu.Runtime.Fallback.FallbackEventBridgeCoordinator

/// Coordinator for the fallback event bridge: dispatch + ordered queue.
/// Extracted from FallbackEventBridge so the file stays within line limits.

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.StateMachine
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.ModelInjection
open Wanxiangshu.Runtime.Fallback.FallbackConfigCodec
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.Fallback.FallbackBridgePorts
open Wanxiangshu.Runtime.Fallback.FallbackBridgeLease
open Wanxiangshu.Runtime.Fallback.FallbackBridgeContinuation
open Wanxiangshu.Runtime.Fallback.FallbackBridgeScanToolText
open Wanxiangshu.Runtime.Fallback.FallbackEventBridge
open Wanxiangshu.Runtime.EventLogAppendSession
open Wanxiangshu.Runtime.EventLogRuntimeAppend

// ARCHITECTURE_EXEMPT: function at line 27 needs splitting
let handleFallbackTransition
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

// ARCHITECTURE_EXEMPT: function at line 89 needs splitting
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
