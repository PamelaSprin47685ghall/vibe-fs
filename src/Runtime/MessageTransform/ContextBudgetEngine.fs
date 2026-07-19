module Wanxiangshu.Runtime.MessageTransform.ContextBudgetEngine

open Wanxiangshu.Runtime

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Runtime.BacklogProjection
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ContextBudgetStore
open Wanxiangshu.Runtime.ContextBudgetUsageCodec
open Wanxiangshu.Runtime.ContextBudgetTrace
open Wanxiangshu.Runtime.MessageTransform.NudgeInject
open Wanxiangshu.Runtime.MessageTransform.ContextBudgetPhase
open Fable.Core

let private resolveTokensAndCalibration
    (plan: MessageTransformPlan)
    (storeEntry: ContextBudgetEntry)
    (totalBytes: int)
    : JS.Promise<int * UsageConfidence * UsageCalibration option * string option> =
    promise {
        let! maybeObservation = plan.ObserveLatestUsage()

        let calibration =
            match maybeObservation, storeEntry.PendingOutbound with
            | Some obs, Some pending ->
                tryCalibrateFromObservation obs storeEntry.LastObservedAssistantID (Some pending)
            | _ -> storeEntry.LastCalibration

        let newObservedID =
            maybeObservation |> Option.map (fun obs -> obs.AssistantMessageID)

        let currentTokens, confidence =
            match calibration, maybeObservation with
            | Some _, _ ->
                resolveCurrentTokensFromCalibration
                    totalBytes
                    None
                    { storeEntry with
                        LastCalibration = calibration }
            | None, Some obs ->
                let bootstrap = max 1 (totalBytes / 2)
                let obsTokens = int obs.InputTokens

                if bootstrap > obsTokens then
                    bootstrap, UsageConfidence.BootstrapEstimate
                else
                    obsTokens, UsageConfidence.Observed
            | None, None -> resolveCurrentTokensFromCalibration totalBytes None storeEntry

        return currentTokens, confidence, calibration, newObservedID
    }

let private computeBudgetState
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (totalBytes: int)
    : JS.Promise<ContextState * bool * ContextBudgetEntry * int * UsageConfidence * BacklogProjectionResult<obj>> =
    promise {
        let storeEntry = ContextBudgetStore.get plan.Scope plan.SessionID

        let! currentTokens, confidence, calibration, newObservedID =
            resolveTokensAndCalibration plan storeEntry totalBytes

        ContextBudgetStore.update plan.Scope plan.SessionID (fun entry ->
            { entry with
                PendingOutbound =
                    Some
                        { Fingerprint = "outbound-" + string totalBytes
                          Bytes = totalBytes }
                LastCalibration = calibration
                LastObservedAssistantID = newObservedID
                LastUsage =
                    Some
                        {| tokenCount = currentTokens
                           textBytes = totalBytes
                           confidence = confidence |} })

        let backlog = backlogOps.GetOrRebuildBacklog plan.SessionID plan.Cleaned
        let currentStore = ContextBudgetStore.get plan.Scope plan.SessionID

        let projection, projectedBytes =
            projectedMessagesBytes plan.Cleaned backlogOps.Host plan.SessionID backlog (fun _ ->
                plan.RawArray |> Option.defaultValue [||])

        let transition =
            classifyTransition currentStore.State projection currentStore.LastBacklog backlog

        let state, isJustInitialized =
            applyTransition
                plan.Scope
                plan.SessionID
                transition
                projection
                projectedBytes
                currentTokens
                currentStore
                backlog

        let finalStore = ContextBudgetStore.get plan.Scope plan.SessionID

        return state, isJustInitialized, finalStore, currentTokens, confidence, projection
    }

let private buildLastTrace
    (plan: MessageTransformPlan)
    (totalBytes: int)
    (state: ContextState)
    (finalStoreEntry: ContextBudgetEntry)
    (currentTokens: int)
    (confidence: UsageConfidence)
    (pressure: ContextBudgetPressure)
    (action: string)
    (stableBytes: int)
    : DecisionTrace =
    { Limit = int64 plan.MaxInputTokens
      ModelKey = plan.ModelKey
      LimitSource = plan.LimitSource
      ObservedTokens = finalStoreEntry.LastUsage |> Option.map (fun usage -> int64 usage.tokenCount)
      CalibrationBytes =
        finalStoreEntry.LastCalibration
        |> Option.map (fun calibration -> calibration.OutboundBytes)
      FinalOutboundBytes = totalBytes
      EstimatedTokens = int64 currentTokens
      StableBytes = stableBytes
      PhaseBaseTokens = state.BaselineTokens
      Confidence = confidence
      Pressure = pressure
      Action = action }

let private writeLastTrace (plan: MessageTransformPlan) (trace: DecisionTrace) : unit =
    ContextBudgetStore.update plan.Scope plan.SessionID (fun entry -> { entry with LastTrace = Some trace })

let applyContextBudget
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (messages: Message<obj> list)
    (encodedAll: obj array)
    : JS.Promise<Message<obj> list> =
    promise {
        if
            messages.IsEmpty
            || plan.MaxInputTokens <= 0
            || plan.ContextBudgetPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Disable
        then
            return messages
        else
            let totalBytes = utf8JsonBytes (box encodedAll)

            let! state, isJustInitialized, finalStoreEntry, currentTokens, confidence, projection =
                computeBudgetState plan backlogOps totalBytes

            let hardSafetyRequired =
                confidence = UsageConfidence.BootstrapEstimate
                && bootstrapHardSafety (int64 currentTokens) (int64 plan.MaxInputTokens)

            if isJustInitialized && not hardSafetyRequired then
                writeLastTrace
                    plan
                    (buildLastTrace
                        plan
                        totalBytes
                        state
                        finalStoreEntry
                        currentTokens
                        confidence
                        BelowThreshold
                        "bootstrap-suppressed"
                        0)

                return messages
            else
                let result, pressure, _nudgeAction =
                    checkAndInjectNudge
                        plan.SessionID
                        plan.MaxInputTokens
                        plan.ContextBudgetPolicy
                        plan.Scope
                        currentTokens
                        confidence
                        state
                        messages
                        projection.TotalTodoOrdinal
                        finalStoreEntry

                let action = actionForDecision pressure finalStoreEntry.NudgeTrack

                writeLastTrace
                    plan
                    (buildLastTrace plan totalBytes state finalStoreEntry currentTokens confidence pressure action 0)

                return result
    }
