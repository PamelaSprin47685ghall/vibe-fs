module Wanxiangshu.Runtime.MessageTransform.Plan

open Wanxiangshu.Runtime

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Runtime.BacklogProjection
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ContextBudgetStore
open Wanxiangshu.Runtime.ContextBudgetUsageCodec
open Wanxiangshu.Runtime.MessageTransform.PlanHelpers
open Fable.Core

let contextBudgetNudgeText = PlanHelpers.contextBudgetNudgeText

let buildContextBudgetNudgeMessage = PlanHelpers.buildContextBudgetNudgeMessage

[<RequireQualifiedAccess>]
type ProjectionPolicy =
    | IncludeProjection
    | ExcludeProjection

type MessageTransformPlan =
    { SessionID: string
      Agent: string
      Directory: string
      ProjectionPolicy: ProjectionPolicy
      BacklogProjectionPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy
      CapsInjectionPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy
      ParallelHintPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy
      ContextBudgetPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy
      IsSubagentSession: bool
      Cleaned: Message<obj> list
      RawArray: obj array option
      SembleInjectEnabled: bool
      Scope: RuntimeScope
      MaxInputTokens: int
      GetContextUsage: obj array -> JS.Promise<int option> }

type BacklogSessionOps =
    { Host: Host
      GetOrRebuildBacklog: string -> Message<obj> list -> BacklogEntry list }

let backlogSessionOpsFrom
    (host: Host)
    (getOrRebuildBacklog: string -> Message<obj> list -> BacklogEntry list)
    : BacklogSessionOps =
    { Host = host
      GetOrRebuildBacklog = getOrRebuildBacklog }

let applyBacklogProjection
    (sessionID: string)
    (policy: Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy)
    (backlogSession: BacklogSessionOps)
    (cleaned: Message<obj> list)
    : Message<obj> list =
    match policy with
    | Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Exclude -> cleaned
    | Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include ->
        let backlog = backlogSession.GetOrRebuildBacklog sessionID cleaned
        projectBacklogFor backlogSession.Host cleaned backlog FoldStrategy.FoldAfterSecond sessionID

let private computeBudgetState
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (totalBytes: int)
    (encodeMessages: Message<obj> list -> obj array)
    : JS.Promise<ContextState * bool * ContextBudgetEntry * int * UsageConfidence * bool> =
    promise {
        let! tokenCountOpt = plan.GetContextUsage(encodeMessages plan.Cleaned)
        let storeEntry = ContextBudgetStore.get plan.Scope plan.SessionID

        let currentTokens, confidence =
            resolveCurrentTokens totalBytes tokenCountOpt storeEntry

        let prevConfidence = storeEntry.LastUsage |> Option.map (fun u -> u.confidence)

        let transitioned =
            prevConfidence = Some UsageConfidence.BootstrapEstimate
            && confidence = UsageConfidence.Observed

        ContextBudgetStore.update plan.Scope plan.SessionID (fun entry ->
            { entry with
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
                plan.GetContextUsage
                plan.Cleaned
                backlogOps.Host
                backlogOps.Host
                plan.SessionID
                plan.Scope
                backlog
                currentStore
                encodeMessages
                currentTokens
                totalBytes
                transitioned

        let finalStore = ContextBudgetStore.get plan.Scope plan.SessionID
        return state, isJustInitialized, finalStore, currentTokens, confidence, transitioned
    }

let applyContextBudget
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (messages: Message<obj> list)
    (encodedAll: obj array)
    (encodeMessages: Message<obj> list -> obj array)
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

            let! state, isJustInitialized, finalStoreEntry, currentTokens, confidence, transitioned =
                computeBudgetState plan backlogOps totalBytes encodeMessages

            if isJustInitialized || transitioned then
                return messages
            else
                return
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
    }
