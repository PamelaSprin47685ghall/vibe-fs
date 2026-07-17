module Wanxiangshu.Runtime.Fallback.CoordinatorHelpers

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

let resolveChain
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (cfg: FallbackConfig)
    (sessionID: string)
    (agentName: string)
    : JS.Promise<FallbackModel list> =
    promise {
        let existing = runtime.GetChain sessionID

        if not (List.isEmpty existing) then
            return existing
        else
            let! currentModel = executor.CaptureCurrentModel sessionID

            let resolved =
                Map.tryFind (normalizeAgentName agentName) cfg.AgentChains
                |> Option.defaultValue cfg.DefaultChain

            let finalChain =
                match currentModel with
                | Some c ->
                    match resolved with
                    | f :: _ when f.ProviderID = c.ProviderID && f.ModelID = c.ModelID -> resolved
                    | _ ->
                        c
                        :: (resolved
                            |> List.filter (fun m -> m.ProviderID <> c.ProviderID || m.ModelID <> c.ModelID))
                | None -> resolved

            if not (List.isEmpty finalChain) then
                runtime.SetChain sessionID finalChain

            return finalChain
    }

let calculateConsumed (evt: FallbackEvent) (statePhase: FallbackPhase) (finalPhase: FallbackPhase) : bool =
    match evt with
    | FallbackEvent.SessionError _ -> finalPhase <> FallbackPhase.Exhausted
    | FallbackEvent.SessionIdle ->
        finalPhase = FallbackPhase.ScanningToolCallText
        || finalPhase = FallbackPhase.RecoveringToolCallText
    | FallbackEvent.SessionBusy ->
        match statePhase with
        | FallbackPhase.Retrying _
        | FallbackPhase.Scanning _ -> true
        | _ -> false
    | _ -> false

let handleTerminalPostSettlement
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (evt: FallbackEvent)
    (finalState2: SessionFallbackState)
    (intentOpt: 'Intent option)
    : JS.Promise<unit> =
    promise {
        let isPostTerminal =
            evt <> FallbackEvent.SessionBusy
            && (finalState2.Lifecycle = FallbackLifecycle.TaskComplete
                || finalState2.Lifecycle = FallbackLifecycle.Cancelled
                || finalState2.Phase = FallbackPhase.Exhausted
                || (finalState2.Phase = FallbackPhase.Idle && intentOpt.IsNone))

        if isPostTerminal then
            match runtime.TryGetPendingLease sessionID with
            | Some lease ->
                if lease.Status <> LeaseStatus.Cancelled then
                    do!
                        appendContinuationSettledOrFail
                            workspaceRoot
                            sessionID
                            lease.ContinuationID
                            lease.HumanTurnID
                            lease.SessionGeneration
                            "completed"
                            lease.ContinuationOrdinal

                if runtime.TryClearPendingLease(sessionID, lease.ContinuationID) then
                    if runtime.GetSessionOwner sessionID = SessionOwner.Fallback then
                        runtime.SetSessionOwner sessionID SessionOwner.NoOwner

                    runtime.SetMainContinuationAwaitingStart sessionID false
            | None -> ()
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

let extractEventContext
    (translator: IEventTranslator)
    (executor: IActionExecutor)
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (rawEvent: obj)
    (pendingReview: (string -> bool) option)
    : JS.Promise<FallbackEvent option * string option * bool> =
    promise {
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

        return eventOpt, eventTurnIdOpt, isMatchedContinuation
    }
