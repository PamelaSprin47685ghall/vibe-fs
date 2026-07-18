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
            | None, Some obs -> int obs.InputTokens, UsageConfidence.Observed
            | None, None -> resolveCurrentTokensFromCalibration totalBytes None storeEntry

        return currentTokens, confidence, calibration, newObservedID
    }

let private computeBudgetState
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (totalBytes: int)
    : JS.Promise<ContextState * bool * ContextBudgetEntry * int * UsageConfidence * bool * int> =
    promise {
        let storeEntry = ContextBudgetStore.get plan.Scope plan.SessionID

        let! currentTokens, confidence, calibration, newObservedID =
            resolveTokensAndCalibration plan storeEntry totalBytes

        let prevConfidence = storeEntry.LastUsage |> Option.map (fun u -> u.confidence)

        let transitioned =
            prevConfidence = Some UsageConfidence.BootstrapEstimate
            && confidence = UsageConfidence.Observed

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

        let! state, isJustInitialized =
            rebuildPhaseState
                plan.SessionID
                plan.Cleaned
                backlogOps.Host
                backlogOps.Host
                plan.SessionID
                plan.Scope
                backlog
                currentStore
                (fun _ -> plan.RawArray |> Option.defaultValue [||])
                currentTokens
                totalBytes
                transitioned

        let finalStore = ContextBudgetStore.get plan.Scope plan.SessionID

        let stableBytes =
            stableProjectionBytes plan.Cleaned backlogOps.Host plan.SessionID backlog (fun _ ->
                plan.RawArray |> Option.defaultValue [||])

        return state, isJustInitialized, finalStore, currentTokens, confidence, transitioned, stableBytes
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
      PhaseBaseTokens = state.phaseBaseTokens
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

            let! state, isJustInitialized, finalStoreEntry, currentTokens, confidence, transitioned, stableBytes =
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
                        stableBytes)

                return messages
            else
                let result =
                    checkAndInjectNudge
                        plan.SessionID
                        plan.MaxInputTokens
                        plan.ContextBudgetPolicy
                        plan.Scope
                        currentTokens
                        confidence
                        state
                        messages
                        backlogOps.Host
                        finalStoreEntry

                let pressure =
                    classifyPressure plan.MaxInputTokens false (int64 currentTokens) state 0

                let action = actionForDecision pressure finalStoreEntry.NudgeTrack

                writeLastTrace
                    plan
                    (buildLastTrace
                        plan
                        totalBytes
                        state
                        finalStoreEntry
                        currentTokens
                        confidence
                        pressure
                        action
                        stableBytes)

                return result
    }
