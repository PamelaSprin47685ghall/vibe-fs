module Wanxiangshu.Shell.MessageTransformPipeline

open Fable.Core
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.MessageTransformCore

type MessageTransformPlan =
    { SessionID: string
      Agent: string
      Directory: string
      Excluded: bool
      IsSubagentSession: bool
      Cleaned: Message<obj> list
      RawArray: obj array option
      SembleInjectEnabled: bool }

let tryInjectParallelToolPrompt (sessionID: string) (messages: Message<obj> list) : Message<obj> list =
    let cleaned = messages |> List.filter (fun m -> m.source = Native)

    let realToolNames =
        let catalogNames = Wanxiangshu.Kernel.ToolCatalog.all |> List.map (fun s -> s.name)
        "methodology" :: catalogNames |> Set.ofList

    let isRealToolName (name: string) : bool = Set.contains name realToolNames

    let isToolPart (part: Part<obj>) : bool =
        match part with
        | ToolPart _ -> true
        | _ -> false

    let isRealToolPart (part: Part<obj>) : bool =
        match part with
        | ToolPart(toolName, callID, _, _) ->
            isRealToolName toolName
            && not (callID.StartsWith("semble-call-") || callID.StartsWith("caps-call-"))
        | _ -> false

    let assistantToolCalls =
        cleaned
        |> List.filter (fun m -> m.info.role = Assistant && m.parts |> List.exists isRealToolPart)

    match List.tryLast assistantToolCalls with
    | None -> messages
    | Some lastAssistantMsg ->
        let allToolParts = lastAssistantMsg.parts |> List.filter isToolPart
        let realToolParts = lastAssistantMsg.parts |> List.filter isRealToolPart
        let toolPartsCount = List.length realToolParts

        if allToolParts.Length <> 1 || toolPartsCount <> 1 then
            messages
        else
            let targetCallID =
                match List.tryHead realToolParts with
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
                              agent = ""
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
    (injectFn: bool -> obj array -> JS.Promise<obj array>)
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
                        plan.Cleaned

            let afterBacklog =
                applyBacklogProjection plan.SessionID plan.Excluded backlogOps afterAmend

            let afterPrompt =
                if plan.Excluded then
                    afterBacklog
                else
                    tryInjectParallelToolPrompt plan.SessionID afterBacklog

            let encoded = encodeMessages afterPrompt
            let! injected = injectFn plan.Excluded encoded
            let! capsFiles = loadCaps ()
            return buildCaps injected capsFiles None
    }
