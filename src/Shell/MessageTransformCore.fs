module Wanxiangshu.Shell.MessageTransformCore

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.BacklogProjection
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.ContextBudgetStore
open Wanxiangshu.Shell.ContextBudgetUsageCodec
open Fable.Core

[<RequireQualifiedAccess>]
type ProjectionPolicy =
    | IncludeProjection
    | ExcludeProjection

type MessageTransformPlan =
    { SessionID: string
      Agent: string
      Directory: string
      ProjectionPolicy: ProjectionPolicy
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
    (policy: ProjectionPolicy)
    (backlogSession: BacklogSessionOps)
    (cleaned: Message<obj> list)
    : Message<obj> list =
    match policy with
    | ProjectionPolicy.ExcludeProjection -> cleaned
    | ProjectionPolicy.IncludeProjection ->
        let backlog = backlogSession.GetOrRebuildBacklog sessionID cleaned
        projectBacklogFor backlogSession.Host cleaned backlog FoldStrategy.FoldAfterSecond sessionID

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

let private resolveCurrentTokens (totalBytes: int) (tokenCountOpt: int option) (storeEntry: ContextBudgetEntry) : int =
    match tokenCountOpt with
    | Some t when t > 0 -> t
    | _ ->
        match estimateTokens totalBytes storeEntry.LastUsage with
        | Some t when t > 0 -> t
        | _ ->
            let estimate = totalBytes / 2
            max 1 estimate

let private rebuildPhaseState
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (backlog: BacklogEntry list)
    (currentStore: ContextBudgetEntry)
    (encodeMessages: Message<obj> list -> obj array)
    (currentTokens: int)
    (totalBytes: int)
    : JS.Promise<ContextState> =
    promise {
        if backlog <> currentStore.LastBacklog || currentStore.State.IsNone then
            let stableMessages =
                projectBacklogFor backlogOps.Host plan.Cleaned backlog FoldStrategy.FoldAfterFirst plan.SessionID

            let stableEncoded = encodeMessages stableMessages
            let stableBytes = utf8JsonBytes (box stableEncoded)
            let! stableTokensOpt = plan.GetContextUsage stableEncoded

            let stableTokens =
                match stableTokensOpt with
                | Some t -> int64 t
                | None ->
                    let currentLastUsage = (ContextBudgetStore.get plan.Scope plan.SessionID).LastUsage

                    match estimateTokens stableBytes currentLastUsage with
                    | Some t -> int64 t
                    | None -> int64 currentTokens

            let backlogBytes =
                ContextBudgetUsageCodec.backlogBytesFromEncoded backlogOps.Host stableEncoded

            let backlogTokens =
                if int64 stableBytes <= 0L then
                    0L
                else
                    stableTokens * int64 backlogBytes / int64 stableBytes

            let currentOrdinal =
                flatten plan.Cleaned
                |> List.filter (fun fp -> isTodoResultFor backlogOps.Host fp.part)
                |> List.length

            let newState =
                match currentStore.State with
                | None when backlog.IsEmpty ->
                    { phaseBaseTokens = 0L
                      backlogTokensAtPhaseStart = 0L
                      phaseStartTodoOrdinal = currentOrdinal }
                | None ->
                    { phaseBaseTokens = stableTokens
                      backlogTokensAtPhaseStart = backlogTokens
                      phaseStartTodoOrdinal = currentOrdinal }
                | Some old ->
                    { phaseBaseTokens = stableTokens
                      backlogTokensAtPhaseStart = backlogTokens
                      phaseStartTodoOrdinal = currentOrdinal }

            let nextEpisode = System.Guid.NewGuid().ToString("N")

            ContextBudgetStore.update plan.Scope plan.SessionID (fun entry ->
                { entry with
                    State = Some newState
                    LastBacklog = backlog
                    NudgeTrack = afterPhaseBoundaryReset entry.NudgeTrack
                    EpisodeID = nextEpisode
                    NudgeCount = 0 })

            return newState
        else
            return currentStore.State.Value
    }

let private checkAndInjectNudge
    (plan: MessageTransformPlan)
    (currentTokens: int)
    (state: ContextState)
    (messages: Message<obj> list)
    (host: Host)
    (storeEntry: ContextBudgetEntry)
    : Message<obj> list =
    let completedTodoCount =
        flatten messages
        |> List.filter (fun fp -> isTodoResultFor host fp.part)
        |> List.length

    match classifyPressure plan.MaxInputTokens false (int64 currentTokens) state completedTodoCount with
    | RequireTodoWriteEmergency ->
        let isMaxReached = storeEntry.NudgeCount >= 2

        let alreadyHasNudge =
            isMaxReached
            || messages
               |> List.exists (fun m ->
                   m.info.id.StartsWith("context-budget-nudge-")
                   || (match m.source with
                       | Synthetic s when s.StartsWith("context-budget-nudge-") -> true
                       | _ -> false))

        if alreadyHasNudge then
            messages
        else
            ContextBudgetStore.update plan.Scope plan.SessionID (fun entry ->
                { entry with
                    NudgeTrack = afterEmergencyNudge entry.NudgeTrack
                    NudgeCount = entry.NudgeCount + 1 })

            List.append messages [ buildContextBudgetNudgeMessage plan.SessionID ]
    | _ -> messages

let applyContextBudget
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (messages: Message<obj> list)
    (encodedAll: obj array)
    (encodeMessages: Message<obj> list -> obj array)
    : JS.Promise<Message<obj> list> =
    promise {
        if messages.IsEmpty || plan.MaxInputTokens <= 0 then
            return messages
        else
            let totalBytes = utf8JsonBytes (box encodedAll)
            let! tokenCountOpt = plan.GetContextUsage encodedAll
            let storeEntry = ContextBudgetStore.get plan.Scope plan.SessionID

            let currentTokens = resolveCurrentTokens totalBytes tokenCountOpt storeEntry

            ContextBudgetStore.update plan.Scope plan.SessionID (fun entry ->
                { entry with
                    LastUsage =
                        Some
                            {| tokenCount = currentTokens
                               textBytes = totalBytes |} })

            let backlog = backlogOps.GetOrRebuildBacklog plan.SessionID plan.Cleaned
            let currentStore = ContextBudgetStore.get plan.Scope plan.SessionID

            let! state = rebuildPhaseState plan backlogOps backlog currentStore encodeMessages currentTokens totalBytes

            let finalStoreEntry = ContextBudgetStore.get plan.Scope plan.SessionID
            return checkAndInjectNudge plan currentTokens state messages backlogOps.Host finalStoreEntry
    }
