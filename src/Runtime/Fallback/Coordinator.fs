module Wanxiangshu.Runtime.Fallback.Coordinator

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.StateMachine
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.Fallback.GateFlagTransitions
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.LeaseValidation
open Wanxiangshu.Runtime.Fallback.ContinuationExecution
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

            let actionFiltered = filterActionDuringCompaction runtime sessionID action

            let! finalState2, intentOpt = executeAction runtime executor workspaceRoot sessionID actionFiltered ns chain
            do! handleTerminalPostSettlement runtime workspaceRoot sessionID evt finalState2 intentOpt

            let consumed = calculateConsumed evt state.Phase finalState2.Phase
            runtime.SetConsumed sessionID consumed

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
            extractEventContext translator executor runtime sessionID rawEvent pendingReview

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

                // F-01 fix: continuation intent execution MUST also be
                // ordered through the same per-session SerialQueue so a
                // late human message cannot interleave with the side
                // effect of an intent decided one tick earlier.  The
                // previous path ran executeContinuationIntent outside
                // the queue, which is exactly the race the audit
                // catalogues: the actor decides "send prompt" inside
                // the queue, returns the intent, and then a fresh
                // human message cancels the intent while the prompt is
                // still being dispatched.
                match intentOpt with
                | Some intent ->
                    do!
                        queue.Enqueue(fun () ->
                            executeContinuationIntent runtime executor workspaceRoot sessionID intent)
                | None -> ()

                return result
        }
