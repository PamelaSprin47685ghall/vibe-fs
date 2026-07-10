module Wanxiangshu.Shell.MessageTransformPipeline

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Kernel.BacklogProjection
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.ContextBudgetStore
open Wanxiangshu.Shell.ContextBudgetUsageCodec

type MessageTransformPlan =
    { SessionID: string
      Agent: string
      Directory: string
      Excluded: bool
      IsSubagentSession: bool
      Cleaned: Message<obj> list
      RawArray: obj array option
      SembleInjectEnabled: bool
      Scope: RuntimeScope
      MaxInputTokens: int
      GetContextUsage: obj array -> JS.Promise<int option> }

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

let contextBudgetNudgeText =
    "Attention: the system context is about to be suspended. "
    + "You must immediately force an emergency stop to all work "
    + "and call the todowrite tool."

let buildContextBudgetNudgeMessage (sessionID: string) : Message<obj> =
    { info =
        { id = "context-budget-nudge-" + System.Guid.NewGuid().ToString()
          sessionID = sessionID
          role = User
          agent = "orchestrator"
          isError = false
          toolName = ""
          details = null
          time = null }
      parts = [ TextPart contextBudgetNudgeText ]
      source = Synthetic "context-budget-nudge-"
      raw = null }

let applyContextBudget
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (messages: Message<obj> list)
    (encodedAll: obj array)
    (encodeMessages: Message<obj> list -> obj array)
    : JS.Promise<Message<obj> list> =
    promise {
        if plan.Excluded || messages.IsEmpty then
            return messages
        else
            let totalBytes = JS.JSON.stringify(encodedAll).Length
            let! tokenCountOpt = plan.GetContextUsage encodedAll
            let storeEntry = ContextBudgetStore.get plan.Scope plan.SessionID

            let currentTokens =
                match tokenCountOpt with
                | Some t -> t
                | None ->
                    match ContextBudget.estimateTokens totalBytes storeEntry.LastUsage with
                    | Some t -> t
                    | None -> 0

            if currentTokens <= 0 then
                return messages
            else
                ContextBudgetStore.update plan.Scope plan.SessionID (fun entry ->
                    { entry with LastUsage = Some {| tokenCount = currentTokens; textBytes = totalBytes |} })

                let backlog = backlogOps.GetOrRebuildBacklog plan.SessionID plan.Cleaned
                let currentStore = ContextBudgetStore.get plan.Scope plan.SessionID

                let! state =
                    promise {
                        if backlog <> currentStore.LastBacklog || currentStore.State.IsNone then
                            let stableMessages =
                                projectBacklogFor
                                    backlogOps.Host
                                    plan.Cleaned
                                    backlog
                                    true
                                    plan.SessionID
                            let stableEncoded = encodeMessages stableMessages
                            let stableBytes = JS.JSON.stringify(stableEncoded).Length
                            let! stableTokensOpt = plan.GetContextUsage stableEncoded

                            let stableTokens =
                                match stableTokensOpt with
                                | Some t -> int64 t
                                | None ->
                                    let currentLastUsage = (ContextBudgetStore.get plan.Scope plan.SessionID).LastUsage
                                    match ContextBudget.estimateTokens stableBytes currentLastUsage with
                                    | Some t -> int64 t
                                    | None -> int64 currentTokens

                            let backlogBytes = ContextBudgetUsageCodec.backlogBytesFromEncoded backlogOps.Host stableEncoded
                            let newState = ContextBudget.beginPhase stableTokens (int64 stableBytes) (int64 backlogBytes)

                            ContextBudgetStore.update plan.Scope plan.SessionID (fun entry ->
                                { entry with
                                    State = Some newState
                                    LastBacklog = backlog
                                    NudgeInjected = false })
                            return newState
                        else
                            return currentStore.State.Value
                    }

                let state_c = state.backlogTokensAtPhaseStart
                let state_s = state.phaseBaseTokens - state_c

                if ContextBudget.isCompactingRequired state.phaseBaseTokens (int64 plan.MaxInputTokens) then
                    return messages
                elif ContextBudget.F (int64 currentTokens) (int64 plan.MaxInputTokens) state_c state_s then
                    let updatedStore = ContextBudgetStore.get plan.Scope plan.SessionID
                    if updatedStore.NudgeInjected then
                        return messages
                    else
                        ContextBudgetStore.update plan.Scope plan.SessionID (fun entry ->
                            { entry with NudgeInjected = true })
                        return List.append messages [ buildContextBudgetNudgeMessage plan.SessionID ]
                else
                    return messages
    }

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
                        (fun raw ->
                            if Dyn.isNullish raw || not (Dyn.typeIs raw "object") then
                                raw
                            else
                                let copy = JS.Constructors.Object.assign (createObj [], raw)
                                Dyn.deleteKey copy "amend"
                                copy)
                        plan.Cleaned

            let afterBacklog =
                applyBacklogProjection plan.SessionID plan.Excluded backlogOps afterAmend

            let! afterBudget, encodedBacklogOpt =
                if plan.Excluded then
                    promise { return afterBacklog, None }
                else
                    promise {
                        let encodedBacklog = encodeMessages afterBacklog
                        let! res = applyContextBudget plan backlogOps afterBacklog encodedBacklog encodeMessages
                        return res, Some encodedBacklog
                    }

            let afterPrompt =
                if plan.Excluded then
                    afterBudget
                else
                    tryInjectParallelToolPrompt plan.SessionID afterBudget

            let encoded =
                match encodedBacklogOpt with
                | Some encodedBacklog when afterPrompt.Length = afterBacklog.Length ->
                    encodedBacklog
                | _ ->
                    encodeMessages afterPrompt

            let! injected = injectFn plan.Excluded encoded
            let! capsFiles = loadCaps ()
            return buildCaps injected capsFiles None
    }
