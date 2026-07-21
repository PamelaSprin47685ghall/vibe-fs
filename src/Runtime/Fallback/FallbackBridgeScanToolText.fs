module Wanxiangshu.Runtime.Fallback.FallbackBridgeScanToolText

/// ScanToolCallAsText action: recover raw tool-call text from the last
/// assistant message as a RecoverWithPrompt continuation, or settle the
/// episode when the transcript is already complete.

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.LeaseValidation
open Wanxiangshu.Runtime.Fallback.ContinuationExecution
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Runtime.Fallback.FallbackMessageDetection
open Wanxiangshu.Runtime.ContinuationEventWriter
open Wanxiangshu.Runtime.Clock

let private completeAsTaskDone (runtime: FallbackRuntimeStore) (sessionID: string) (finalState: SessionFallbackState) =
    let updated =
        { finalState with
            Phase = FallbackPhase.Idle
            ContinueCount = 0
            Lifecycle = FallbackLifecycle.TaskComplete }

    runtime.Update(sessionID, setCore updated)
    updated, None

let private settleIdle (runtime: FallbackRuntimeStore) (sessionID: string) (finalState: SessionFallbackState) =
    let updated =
        { finalState with
            Phase = FallbackPhase.Idle
            ContinueCount = 0 }

    runtime.Update(sessionID, setCore updated)
    updated, None

let private settleByTranscript
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (finalState: SessionFallbackState)
    (msgs: obj array)
    =
    let isToolFinish = isLastAssistantToolFinish msgs
    let hasResult = hasToolResultAfter msgs
    let taskComplete = (not isToolFinish) || hasResult

    let updated =
        { finalState with
            Phase = FallbackPhase.Idle
            ContinueCount = 0
            Lifecycle =
                (if taskComplete then
                     FallbackLifecycle.TaskComplete
                 else
                     FallbackLifecycle.Active) }

    runtime.Update(sessionID, setCore updated)
    updated, None

let private dispatchRecovery
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (finalState: SessionFallbackState)
    (model: FallbackModel)
    (promptText: string)
    : JS.Promise<SessionFallbackState * ContinuationIntent option> =
    promise {
        let currentState = runtime.GetSession sessionID
        let updated = { finalState with Phase = FallbackPhase.RecoveringToolCallText }
        match tryReserveContinuationLease currentState model (Some promptText) with
        | None ->
            return finalState, None
        | Some (nextState, lease) ->
            let agent = currentState.AgentName
            let modelStr =
                match model.Variant with
                | Some v -> model.ProviderID + "/" + model.ModelID + ":" + v
                | None -> model.ProviderID + "/" + model.ModelID
            let atMs = getTimestampMs ()
            do!
                appendContinuationRequestedOrFail
                    workspaceRoot
                    sessionID
                    lease.ContinuationID
                    modelStr
                    agent
                    atMs
                    lease.SessionGeneration
                    lease.CancelGeneration
                    lease.HumanTurnID
                    "Fallback"
                    lease.ContinuationOrdinal
            let committed = commitContinuationLease runtime sessionID currentState nextState (Some updated)
            if not committed then
                return finalState, None
            else
                let intent =
                    RecoverWithPromptIntent(
                        model,
                        promptText,
                        agent,
                        lease.HumanTurnID,
                        lease.SessionGeneration,
                        lease.CancelGeneration,
                        lease.ContinuationID,
                        lease.ContinuationOrdinal
                    )
                return updated, Some intent
    }

let handleScanToolCallAsText
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (finalState: SessionFallbackState)
    (chain: FallbackModel list)
    : JS.Promise<SessionFallbackState * ContinuationIntent option> =
    promise {
        let! msgs = executor.FetchMessages sessionID

        if allTodosCompleted msgs then
            return completeAsTaskDone runtime sessionID finalState
        else
            match scanToolCallAsText msgs with
            | Some promptText ->
                match List.tryItem finalState.CurrentIndex chain with
                | Some model -> return! dispatchRecovery runtime workspaceRoot sessionID finalState model promptText
                | None -> return settleIdle runtime sessionID finalState
            | None -> return settleByTranscript runtime sessionID finalState msgs
    }
