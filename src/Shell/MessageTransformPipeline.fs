module Wanxiangshu.Shell.MessageTransformPipeline

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.MessageTransformCore

type MessageTransformPlan = Wanxiangshu.Shell.MessageTransformCore.MessageTransformPlan

let tryInjectParallelToolPrompt (sessionID: string) (messages: Message<obj> list) : Message<obj> list =
    let cleaned = messages |> List.filter (fun m -> m.source = Native)

    let isToolPart (part: Part<obj>) : bool =
        match part with
        | ToolPart _ -> true
        | _ -> false

    let isTriggerableToolPart (part: Part<obj>) : bool =
        match part with
        | ToolPart(_, callID, _, _) -> not (Wanxiangshu.Kernel.HostTools.isSynthCallId callID)
        | _ -> false

    let assistantToolCalls =
        cleaned
        |> List.filter (fun m -> m.info.role = Assistant && m.parts |> List.exists isTriggerableToolPart)

    match List.tryLast assistantToolCalls with
    | None -> messages
    | Some lastAssistantMsg ->
        let allToolParts = lastAssistantMsg.parts |> List.filter isToolPart

        let triggerableToolParts =
            lastAssistantMsg.parts |> List.filter isTriggerableToolPart

        let triggerableCount = List.length triggerableToolParts

        if allToolParts.Length <> 1 || triggerableCount <> 1 then
            messages
        else
            let targetCallID =
                match List.tryHead triggerableToolParts with
                | Some(ToolPart(_, callID, _, _)) -> callID
                | _ -> ""

            if targetCallID = "" then
                messages
            else
                let lastIdx =
                    cleaned |> List.findIndex (fun m -> m.info.id = lastAssistantMsg.info.id)

                let laterMessages = cleaned.[lastIdx + 1 ..]
                let hasResult = laterMessages |> List.exists (fun m -> m.info.role = ToolResult)

                if not hasResult then
                    messages
                else
                    let synthMsg: Message<obj> =
                        { info =
                            { id = "parallel-tool-synth-" + targetCallID
                              sessionID = sessionID
                              role = User
                              agent = "orchestrator"
                              isError = false
                              toolName = ""
                              details = null
                              time = null }
                          parts = [ TextPart Wanxiangshu.Kernel.PromptFragments.parallelToolPromptProse ]
                          source = Synthetic "parallel-tool-synth-"
                          raw = null }

                    List.append messages [ synthMsg ]

let runMessageTransformPipeline
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (encodeMessages: Message<obj> list -> obj array)
    (injectFn: ProjectionPolicy -> obj array -> JS.Promise<obj array>)
    (loadCaps: unit -> JS.Promise<CapsFile list>)
    (buildCaps: obj array -> CapsFile list -> string option -> obj array)
    : JS.Promise<obj array> =
    promise {
        if plan.Cleaned.IsEmpty then
            return [||]
        else
            let afterAmend =
                if plan.SembleInjectEnabled then
                    plan.Cleaned
                else
                    AmendFilter.filterAmendMessages
                        (fun raw ->
                            match DynField.optField raw "amend" with
                            | None -> None
                            | Some v ->
                                match v with
                                | :? int as n when n > 0 -> Some n
                                | :? float as f when f > 0.0 -> Some(int f)
                                | _ -> None)
                        (fun raw ->
                            if Dyn.isNullish raw || not (Dyn.typeIs raw "object") then
                                raw
                            else
                                let copy = JS.Constructors.Object.assign (createObj [], raw)
                                Dyn.deleteKey copy "amend"
                                copy)
                        plan.Cleaned

            let isExcluded =
                match plan.ProjectionPolicy with
                | ProjectionPolicy.ExcludeProjection -> true
                | ProjectionPolicy.IncludeProjection -> false

            let afterBacklog =
                applyBacklogProjection plan.SessionID plan.ProjectionPolicy backlogOps afterAmend

            let encodedBacklog = encodeMessages afterBacklog

            let! afterBudget = applyContextBudget plan backlogOps afterBacklog encodedBacklog encodeMessages

            let afterPrompt =
                if isExcluded then
                    afterBudget
                else
                    tryInjectParallelToolPrompt plan.SessionID afterBudget

            let encoded =
                if afterPrompt.Length = afterBacklog.Length then
                    encodedBacklog
                else
                    encodeMessages afterPrompt

            let! injected = injectFn plan.ProjectionPolicy encoded
            let! capsFiles = loadCaps ()
            return buildCaps injected capsFiles None
    }
