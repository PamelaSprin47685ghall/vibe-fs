module Wanxiangshu.Runtime.MessageTransform.ContextBudgetEngine

open Wanxiangshu.Runtime

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ContextBudgetUsageCodec
open Wanxiangshu.Runtime.ContextBudgetTrace
open Wanxiangshu.Runtime.MessageTransform.NudgeInject
open Wanxiangshu.Runtime.MessageTransform.ContextBudgetEngineHelper
open Fable.Core

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
