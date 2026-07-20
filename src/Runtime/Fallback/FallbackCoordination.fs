module Wanxiangshu.Runtime.Fallback.FallbackCoordination

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.StateMachine
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.LeaseValidation
open Wanxiangshu.Runtime.Fallback.ContinuationExecution
open Wanxiangshu.Runtime.Fallback.SessionStatusHandler
open Wanxiangshu.Runtime.Fallback.FallbackBridgeScanToolText
open Wanxiangshu.Runtime.Fallback.FallbackConfigCodec
open Wanxiangshu.Runtime.Fallback.FallbackChainResolution
open Wanxiangshu.Runtime.Fallback.FallbackIdleSettlement

let resolveChain
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (cfg: FallbackConfig)
    (sessionID: string)
    (agentName: string)
    : JS.Promise<FallbackModel list> =
    promise {
        let existing = (runtime.GetSession sessionID).Chain

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
                runtime.UpdateSession(sessionID, selectChain finalChain)

            return finalChain
    }

let calculateConsumed (evt: FallbackEvent) (statePhase: FallbackPhase) (finalState: SessionFallbackState) : bool =
    match evt with
    | FallbackEvent.SessionError _ -> finalState.Phase <> FallbackPhase.Exhausted
    | FallbackEvent.SessionIdle ->
        finalState.Phase = FallbackPhase.ScanningToolCallText
        || finalState.Phase = FallbackPhase.RecoveringToolCallText
    | FallbackEvent.SessionBusy ->
        match statePhase with
        | FallbackPhase.Retrying _
        | FallbackPhase.Scanning _ -> true
        | _ -> false
    | _ -> false
let handleTerminalPostSettlement = FallbackIdleSettlement.handleTerminalPostSettlement

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
    (workspaceRoot: string)
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
        let parentIDOpt = translator.ExtractAssistantParentId rawEvent
        let eventTurnIdOpt = translator.ExtractHostRunId rawEvent

        let isMatchedContinuation, isEventContIdMatch =
            checkContinuationMatchesWithEvidence runtime sessionID continuationId parentIDOpt eventTurnIdOpt

        if
            isEventContIdMatch
            && (translator.IsSessionBusy rawEvent
                || isAssistantMsg
                || translator.IsSessionIdle rawEvent
                || translator.IsSessionError rawEvent)
        then
            runtime.Update(sessionID, setMainContinuationAwaitingStart false)

        let! eventOpt = translateEvent translator executor runtime sessionID rawEvent pendingReview

        let isStale =
            checkIsStale isEventContIdMatch eventOpt eventTurnIdOpt runtime sessionID

        let eventOpt = if isStale then None else eventOpt
        let session = runtime.GetSession sessionID

        let! eventOpt =
            filterIdleEvent
                runtime
                workspaceRoot
                sessionID
                session
                eventOpt
                isMatchedContinuation
                continuationId

        return eventOpt, eventTurnIdOpt, isMatchedContinuation
    }
