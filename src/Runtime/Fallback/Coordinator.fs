module Wanxiangshu.Runtime.Fallback.Coordinator

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.StateMachine
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.LeaseValidation
open Wanxiangshu.Runtime.Fallback.ContinuationExecution
open Wanxiangshu.Runtime.Fallback.ContinuationIntentExecution
open Wanxiangshu.Runtime.Fallback.HumanTurnHandler
open Wanxiangshu.Runtime.Fallback.SessionStatusHandler
open Wanxiangshu.Runtime.Fallback.CompactionHandler
open Wanxiangshu.Runtime.Fallback.FallbackBridgeScanToolText
open Wanxiangshu.Runtime.Fallback.FallbackConfigCodec
open Wanxiangshu.Runtime.ContinuationEventWriter

open Wanxiangshu.Runtime.Fallback.FallbackCoordination

let resolveChain = FallbackCoordination.resolveChain
let calculateConsumed = FallbackCoordination.calculateConsumed
let handleTerminalPostSettlement = FallbackCoordination.handleTerminalPostSettlement
let executeAction = FallbackCoordination.executeAction
let extractEventContext = FallbackCoordination.extractEventContext

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

        let cfg = configLookup ((runtime.GetSession sessionID).AgentName)
        let! chain = resolveChain runtime executor cfg sessionID ((runtime.GetSession sessionID).AgentName)

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

            runtime.Update(sessionID, setCore ns)

            if evt = FallbackEvent.SessionBusy then
                updateBusyLeases runtime sessionID

            let actionFiltered = filterActionDuringCompaction runtime sessionID action

            let! finalState2, intentOpt = executeAction runtime executor workspaceRoot sessionID actionFiltered ns chain
            do! handleTerminalPostSettlement runtime workspaceRoot sessionID evt finalState2 intentOpt

            let consumed = calculateConsumed evt state.Phase finalState2
            runtime.Update(sessionID, recordConsumed consumed)

            return
                { Consumed = consumed
                  State = finalState2 },
                intentOpt
    }

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

        let! eventOpt, eventTurnIdOpt, isMatchedContinuation =
            extractEventContext translator executor runtime workspaceRoot sessionID rawEvent pendingReview

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

let private isRelevantEvent (translator: IEventTranslator) (sessionID: string) (rawEvent: obj) : bool =
    translator.TranslateError rawEvent |> Option.isSome
    || translator.IsNewUserMessage(sessionID, rawEvent)
    || translator.IsSessionBusy rawEvent
    || translator.IsSessionIdle rawEvent
    || translator.ExtractContinuationIdentity rawEvent |> Option.isSome

let private enqueueRelevantEvent
    (queues: Map<string, SerialQueue> ref)
    (translator: IEventTranslator)
    (runtime: FallbackRuntimeStore)
    (configLookup: ConfigLookup)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (pendingReview: (string -> bool) option)
    (sessionID: string)
    (rawEvent: obj)
    : JS.Promise<FallbackHookResult> =
    let queue =
        match Map.tryFind sessionID queues.Value with
        | Some q -> q
        | None ->
            let q = SerialQueue()
            queues.Value <- Map.add sessionID q queues.Value
            q

    promise {
        let! result, intentOpt =
            queue.Enqueue(fun () ->
                handleEvent translator runtime configLookup executor workspaceRoot rawEvent pendingReview)

        match intentOpt with
        | Some intent ->
            run runtime executor workspaceRoot sessionID intent
            |> Promise.catch (fun ex ->
                JS.console.error ("fallback continuation effect failed for " + sessionID + ": " + ex.Message))
            |> Promise.start
        | None -> ()

        return result
    }

let createHandler
    (translator: IEventTranslator)
    (runtime: FallbackRuntimeStore)
    (configLookup: ConfigLookup)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (pendingReview: (string -> bool) option)
    : (obj -> JS.Promise<FallbackHookResult>) =
    let queues = ref Map.empty<string, SerialQueue>

    fun (rawEvent: obj) ->
        promise {
            let sessionID = translator.ExtractSessionID rawEvent
            if not (isRelevantEvent translator sessionID rawEvent) then
                let state = runtime.GetOrCreateState sessionID
                return { Consumed = false; State = state }
            else
                return! enqueueRelevantEvent queues translator runtime configLookup executor workspaceRoot pendingReview sessionID rawEvent
        }
