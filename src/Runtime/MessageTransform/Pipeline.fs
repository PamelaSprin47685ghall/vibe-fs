module Wanxiangshu.Runtime.MessageTransform.Pipeline

open Wanxiangshu.Runtime

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ToolExecutionStatusModule
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.Stack
open Wanxiangshu.Runtime.RuntimeScope

open Wanxiangshu.Runtime.MessageTransform.ToolCallIntegrity
open Wanxiangshu.Runtime.MessageTransform.ParallelHintStage
open Wanxiangshu.Runtime.MessageTransform.BacklogStage
open Wanxiangshu.Runtime.MessageTransform.CapsStage

type MessageTransformPlan = Wanxiangshu.Runtime.MessageTransform.Plan.MessageTransformPlan

let private applyBacklogAndSave
    (scope: RuntimeScope)
    (sessionID: string)
    (eventCount: int)
    (afterBacklog: Message<obj> list)
    (encodedBacklog: obj array)
    : obj array =
    let state = get scope sessionID

    let backlogSegmentLength =
        afterBacklog
        |> List.takeWhile (fun message -> message.source <> Native)
        |> List.length

    let encodedBacklogSlot, stateAfterBacklog =
        applyBacklogSlot state eventCount backlogSegmentLength encodedBacklog

    set scope sessionID stateAfterBacklog
    encodedBacklogSlot

let private encodeAndApplyBacklog
    (scope: RuntimeScope)
    (sessionID: string)
    (eventCount: int)
    (afterBacklog: Message<obj> list)
    (encodedBacklogSlot: obj array)
    (afterPrompt: Message<obj> list)
    (encodeMessages: Message<obj> list -> obj array)
    : obj array * TransformState =
    let encoded =
        if afterPrompt.Length = afterBacklog.Length then
            encodedBacklogSlot
        else
            encodeMessages afterPrompt

    if afterPrompt.Length <> afterBacklog.Length then
        let backlogSegmentLength =
            afterBacklog
            |> List.takeWhile (fun message -> message.source <> Native)
            |> List.length

        encoded, get scope sessionID
    else
        encoded, get scope sessionID

let runMessageTransformPipeline
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (encodeMessages: Message<obj> list -> obj array)
    (injectFn: Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy -> obj array -> JS.Promise<obj array>)
    (loadCaps: unit -> JS.Promise<CapsFile list>)
    (buildCaps: obj array -> CapsFile list -> string option -> obj array)
    : JS.Promise<obj array> =
    promise {
        if plan.Cleaned.IsEmpty then
            return [||]
        else
            let afterAmend = plan.Cleaned

            let afterBacklog =
                applyBacklogProjection plan.SessionID plan.BacklogProjectionPolicy backlogOps afterAmend

            let encodedBacklog = encodeMessages afterBacklog
            let eventCount = (backlogOps.GetOrRebuildBacklog plan.SessionID plan.Cleaned).Length

            let encodedBacklogSlot =
                applyBacklogAndSave plan.Scope plan.SessionID eventCount afterBacklog encodedBacklog

            let afterPrompt =
                match plan.ParallelHintPolicy with
                | Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Exclude -> afterBacklog
                | Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Include ->
                    tryInjectParallelToolPrompt plan.SessionID afterBacklog

            let encodedAfterBacklogSlot, stateAfterReencode =
                encodeAndApplyBacklog
                    plan.Scope
                    plan.SessionID
                    eventCount
                    afterBacklog
                    encodedBacklogSlot
                    afterPrompt
                    encodeMessages

            let topKey = computeTopSlotKey plan afterBacklog afterBacklog afterPrompt

            let encodedWithTopSlot, stateAfterTop =
                applyTopSlot stateAfterReencode topKey encodedAfterBacklogSlot

            set plan.Scope plan.SessionID stateAfterTop

            let! injected = injectFn plan.BacklogProjectionPolicy encodedWithTopSlot
            let! withoutBudgetNudge = prependCapsWithState plan.Scope plan.SessionID plan injected loadCaps buildCaps
            let! budgetedMessages = applyContextBudget plan backlogOps afterPrompt withoutBudgetNudge

            let budgetNudge =
                if budgetedMessages.Length > afterPrompt.Length then
                    budgetedMessages |> List.skip afterPrompt.Length |> encodeMessages
                else
                    [||]

            return Array.append withoutBudgetNudge budgetNudge
    }
