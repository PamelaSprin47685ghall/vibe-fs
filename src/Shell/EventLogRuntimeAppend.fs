module Wanxiangshu.Shell.EventLogRuntimeAppend

open Fable.Core
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel
open Wanxiangshu.Shell.EventLogCodec
open Wanxiangshu.Shell.EventLogRuntimeStore
open Wanxiangshu.Shell.Clock
open Wanxiangshu.Shell.WorkBacklogToolsCodec
open Thoth.Json

let private verdictPayload (verdict: string) (feedback: string option) =
    match feedback with
    | Some f when f <> "" -> Map.add "feedback" f (Map [ "verdict", verdict ])
    | _ -> Map [ "verdict", verdict ]

let private statusToString =
    function
    | ToolArgs.Todo -> "pending"
    | ToolArgs.InProgress -> "in_progress"
    | ToolArgs.Completed -> "completed"
    | ToolArgs.Cancelled -> "cancelled"

let private priorityToString =
    function
    | ToolArgs.Low -> "low"
    | ToolArgs.Medium -> "medium"
    | ToolArgs.High -> "high"

let private backlogPayload (args: TodoWriteArgs) =
    let mappedTodos =
        args.Todos
        |> Array.map (fun t ->
            {| Content = t.Content
               Status = statusToString t.Status
               Priority = priorityToString t.Priority |})

    Map
        [ "ahaMoments", args.AhaMoments
          "changesAndReasons", args.ChangesAndReasons
          "gotchas", args.Gotchas
          "lessonsAndConventions", args.LessonsAndConventions
          "plan", args.Plan
          "todosJson", Encode.Auto.toString (0, mappedTodos)
          "selectMethodologyJson", Encode.Auto.toString (0, args.SelectMethodology) ]

let private append (workspaceRoot: string) (e: WanEvent) : JS.Promise<Result<unit, string>> =
    appendAndCache workspaceRoot e

let private appendOrFail (workspaceRoot: string) (e: WanEvent) : JS.Promise<unit> = appendAndCacheOrFail workspaceRoot e

let appendLoopActivated (workspaceRoot: string) (sessionID: string) (task: string) : JS.Promise<Result<unit, string>> =
    append
        workspaceRoot
        (buildEvent sessionID eventKindLoopActivated (Map [ "task", task ]) (getTimestampMs().ToString()))

let appendLoopCancelled (workspaceRoot: string) (sessionID: string) : JS.Promise<Result<unit, string>> =
    append workspaceRoot (buildEvent sessionID eventKindLoopCancelled Map.empty (getTimestampMs().ToString()))

let appendReviewVerdict
    (workspaceRoot: string)
    (sessionID: string)
    (verdict: string)
    (feedback: string option)
    : JS.Promise<Result<unit, string>> =
    append
        workspaceRoot
        (buildEvent sessionID eventKindReviewVerdict (verdictPayload verdict feedback) (getTimestampMs().ToString()))

let appendSubmitReviewWipRecorded (workspaceRoot: string) (sessionID: string) : JS.Promise<Result<unit, string>> =
    append workspaceRoot (buildEvent sessionID eventKindSubmitReviewWipRecorded Map.empty (getTimestampMs().ToString()))

let appendNudgeDedupCleared (workspaceRoot: string) (sessionID: string) : JS.Promise<Result<unit, string>> =
    append workspaceRoot (buildEvent sessionID eventKindNudgeDedupCleared Map.empty (getTimestampMs().ToString()))

let appendWorkBacklogCommitted
    (workspaceRoot: string)
    (sessionID: string)
    (args: TodoWriteArgs)
    : JS.Promise<Result<unit, string>> =
    append
        workspaceRoot
        (buildEvent sessionID eventKindWorkBacklogCommitted (backlogPayload args) (getTimestampMs().ToString()))

let appendLoopActivatedOrFail (workspaceRoot: string) (sessionID: string) (task: string) : JS.Promise<unit> =
    appendOrFail
        workspaceRoot
        (buildEvent sessionID eventKindLoopActivated (Map [ "task", task ]) (getTimestampMs().ToString()))

let appendLoopCancelledOrFail (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    appendOrFail workspaceRoot (buildEvent sessionID eventKindLoopCancelled Map.empty (getTimestampMs().ToString()))

let appendReviewVerdictOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (verdict: string)
    (feedback: string option)
    : JS.Promise<unit> =
    appendOrFail
        workspaceRoot
        (buildEvent sessionID eventKindReviewVerdict (verdictPayload verdict feedback) (getTimestampMs().ToString()))

let appendSubmitReviewWipRecordedOrFail (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    appendOrFail
        workspaceRoot
        (buildEvent sessionID eventKindSubmitReviewWipRecorded Map.empty (getTimestampMs().ToString()))

let appendNudgeDedupClearedOrFail (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    appendOrFail workspaceRoot (buildEvent sessionID eventKindNudgeDedupCleared Map.empty (getTimestampMs().ToString()))

let appendWorkBacklogCommittedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (args: TodoWriteArgs)
    : JS.Promise<unit> =
    appendOrFail
        workspaceRoot
        (buildEvent sessionID eventKindWorkBacklogCommitted (backlogPayload args) (getTimestampMs().ToString()))

let appendAssistantCompleted
    (workspaceRoot: string)
    (sessionID: string)
    (assistantMessage: string)
    (agent: string option)
    (model: string option)
    (turnId: string)
    (openTodos: string list)
    : JS.Promise<Result<unit, string>> =
    append
        workspaceRoot
        (buildEvent
            sessionID
            eventKindAssistantCompleted
            (assistantPayload assistantMessage agent model turnId openTodos)
            (getTimestampMs().ToString()))

let appendAssistantCompletedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (assistantMessage: string)
    (agent: string option)
    (model: string option)
    (turnId: string)
    (openTodos: string list)
    : JS.Promise<unit> =
    appendOrFail
        workspaceRoot
        (buildEvent
            sessionID
            eventKindAssistantCompleted
            (assistantPayload assistantMessage agent model turnId openTodos)
            (getTimestampMs().ToString()))

let appendSubagentSpawned
    (workspaceRoot: string)
    (sessionID: string)
    (childId: string)
    (agent: string)
    (title: string)
    : JS.Promise<Result<unit, string>> =
    append
        workspaceRoot
        (buildEvent
            sessionID
            eventKindSubagentSpawned
            (Map [ "childId", childId; "agent", agent; "title", title ])
            (getTimestampMs().ToString()))

let appendSubagentSpawnedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (childId: string)
    (agent: string)
    (title: string)
    : JS.Promise<unit> =
    appendOrFail
        workspaceRoot
        (buildEvent
            sessionID
            eventKindSubagentSpawned
            (Map [ "childId", childId; "agent", agent; "title", title ])
            (getTimestampMs().ToString()))

let appendSubagentContinued
    (workspaceRoot: string)
    (sessionID: string)
    (childId: string)
    (prompt: string)
    : JS.Promise<Result<unit, string>> =
    append
        workspaceRoot
        (buildEvent
            sessionID
            eventKindSubagentContinued
            (Map [ "childId", childId; "prompt", prompt ])
            (getTimestampMs().ToString()))

let appendSubagentContinuedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (childId: string)
    (prompt: string)
    : JS.Promise<unit> =
    appendOrFail
        workspaceRoot
        (buildEvent
            sessionID
            eventKindSubagentContinued
            (Map [ "childId", childId; "prompt", prompt ])
            (getTimestampMs().ToString()))

let private fallbackInjectionPayload (modelStr: string) (agentStr: string) (atMs: int64) =
    Map [ "model", modelStr; "agent", agentStr; "at", atMs.ToString() ]

let appendFallbackContinueInjected
    (workspaceRoot: string)
    (sessionID: string)
    (modelStr: string)
    (agentStr: string)
    (atMs: int64)
    : JS.Promise<Result<unit, string>> =
    append
        workspaceRoot
        (buildEvent
            sessionID
            eventKindFallbackContinueInjected
            (fallbackInjectionPayload modelStr agentStr atMs)
            (getTimestampMs().ToString()))

let appendFallbackContinueInjectedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (modelStr: string)
    (agentStr: string)
    (atMs: int64)
    : JS.Promise<unit> =
    appendOrFail
        workspaceRoot
        (buildEvent
            sessionID
            eventKindFallbackContinueInjected
            (fallbackInjectionPayload modelStr agentStr atMs)
            (getTimestampMs().ToString()))

let appendHumanTurnStartedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (turnId: string)
    (provider: string)
    (model: string)
    (variant: string)
    (agent: string)
    (humanTurnOrdinal: int)
    (messageId: string)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "turnId", turnId
              "provider", provider
              "model", model
              "variant", variant
              "agent", agent
              "humanTurnOrdinal", humanTurnOrdinal.ToString()
              "messageId", messageId ]

    appendOrFail workspaceRoot (buildEvent sessionID eventKindHumanTurnStarted payload (getTimestampMs().ToString()))

let appendUserAbortObservedOrFail (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    appendOrFail workspaceRoot (buildEvent sessionID eventKindUserAbortObserved Map.empty (getTimestampMs().ToString()))

let appendContinuationRequestedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (continuationID: string)
    (modelStr: string)
    (agentStr: string)
    (atMs: int64)
    (generation: int)
    (cancelGeneration: int)
    (humanTurnID: string)
    (owner: string)
    (continuationOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "continuationId", continuationID
              "model", modelStr
              "agent", agentStr
              "at", atMs.ToString()
              "generation", generation.ToString()
              "cancelGeneration", cancelGeneration.ToString()
              "humanTurnId", humanTurnID
              "owner", owner
              "continuationOrdinal", continuationOrdinal.ToString() ]

    appendOrFail
        workspaceRoot
        (buildEvent sessionID eventKindContinuationRequested payload (getTimestampMs().ToString()))

let appendContinuationDispatchStartedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (continuationID: string)
    (continuationOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "continuationId", continuationID
              "continuationOrdinal", continuationOrdinal.ToString() ]

    appendOrFail
        workspaceRoot
        (buildEvent sessionID eventKindContinuationDispatchStarted payload (getTimestampMs().ToString()))

let appendContinuationDispatchedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (continuationID: string)
    (modelStr: string)
    (agentStr: string)
    (atMs: int64)
    (continuationOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "continuationId", continuationID
              "model", modelStr
              "agent", agentStr
              "at", atMs.ToString()
              "continuationOrdinal", continuationOrdinal.ToString() ]

    appendOrFail
        workspaceRoot
        (buildEvent sessionID eventKindContinuationDispatched payload (getTimestampMs().ToString()))

let appendContinuationFailedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (continuationID: string)
    (errorMsg: string)
    (continuationOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "continuationId", continuationID
              "error", errorMsg
              "continuationOrdinal", continuationOrdinal.ToString() ]

    appendOrFail workspaceRoot (buildEvent sessionID eventKindContinuationFailed payload (getTimestampMs().ToString()))

let appendContinuationCancelledOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (continuationID: string)
    (reason: string)
    (continuationOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "continuationId", continuationID
              "reason", reason
              "continuationOrdinal", continuationOrdinal.ToString() ]

    appendOrFail
        workspaceRoot
        (buildEvent sessionID eventKindContinuationCancelled payload (getTimestampMs().ToString()))

let appendContinuationSettledOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (continuationID: string)
    (humanTurnID: string)
    (generation: int)
    (status: string)
    (continuationOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "continuationId", continuationID
              "humanTurnId", humanTurnID
              "generation", generation.ToString()
              "status", status
              "continuationOrdinal", continuationOrdinal.ToString() ]

    appendOrFail workspaceRoot (buildEvent sessionID eventKindContinuationSettled payload (getTimestampMs().ToString()))

let appendCompactionStartedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (compactionId: string)
    (generationAtStart: int)
    (humanTurnId: string)
    (compactionOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "compactionId", compactionId
              "generationAtStart", generationAtStart.ToString()
              "humanTurnId", humanTurnId
              "compactionOrdinal", compactionOrdinal.ToString() ]

    appendOrFail workspaceRoot (buildEvent sessionID eventKindCompactionStarted payload (getTimestampMs().ToString()))

let appendCompactionSettledOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (compactionId: string)
    (status: string)
    (compactionOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "compactionId", compactionId
              "status", status
              "compactionOrdinal", compactionOrdinal.ToString() ]

    appendOrFail workspaceRoot (buildEvent sessionID eventKindCompactionSettled payload (getTimestampMs().ToString()))

let appendContextGenerationChangedOrFail (workspaceRoot: string) (sessionID: string) (newGen: int) : JS.Promise<unit> =
    let payload = Map [ "generation", newGen.ToString() ]

    appendOrFail
        workspaceRoot
        (buildEvent sessionID eventKindContextGenerationChanged payload (getTimestampMs().ToString()))

let appendRouteObservedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (provider: string)
    (model: string)
    (variant: string)
    (agent: string)
    : JS.Promise<unit> =
    let payload =
        Map [ "provider", provider; "model", model; "variant", variant; "agent", agent ]

    appendOrFail workspaceRoot (buildEvent sessionID eventKindRouteObserved payload (getTimestampMs().ToString()))

let appendNudgeRequestedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (nudgeID: string)
    (action: string)
    (anchor: string)
    (sessionGen: int)
    (cancelGen: int)
    (humanTurnID: string)
    (nudgeOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "nudgeId", nudgeID
              "action", action
              "anchor", anchor
              "generation", sessionGen.ToString()
              "cancelGeneration", cancelGen.ToString()
              "humanTurnId", humanTurnID
              "nudgeOrdinal", nudgeOrdinal.ToString() ]

    appendOrFail workspaceRoot (buildEvent sessionID eventKindNudgeRequested payload (getTimestampMs().ToString()))

let appendNudgeDispatchedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (nudgeID: string)
    (action: string)
    (anchor: string)
    (nudgeOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "nudgeId", nudgeID
              "action", action
              "anchor", anchor
              "nudgeOrdinal", nudgeOrdinal.ToString() ]

    appendOrFail workspaceRoot (buildEvent sessionID eventKindNudgeDispatched payload (getTimestampMs().ToString()))

let appendNudgeFailedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (nudgeID: string)
    (errorMsg: string)
    (nudgeOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "nudgeId", nudgeID
              "error", errorMsg
              "nudgeOrdinal", nudgeOrdinal.ToString() ]

    appendOrFail workspaceRoot (buildEvent sessionID eventKindNudgeFailed payload (getTimestampMs().ToString()))

let appendNudgeCancelledOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (nudgeID: string)
    (reason: string)
    (nudgeOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "nudgeId", nudgeID
              "reason", reason
              "nudgeOrdinal", nudgeOrdinal.ToString() ]

    appendOrFail workspaceRoot (buildEvent sessionID eventKindNudgeCancelled payload (getTimestampMs().ToString()))

let appendNudgeSettledOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (nudgeID: string)
    (status: string)
    (nudgeOrdinal: int)
    : JS.Promise<unit> =
    let payload =
        Map
            [ "nudgeId", nudgeID
              "status", status
              "nudgeOrdinal", nudgeOrdinal.ToString() ]

    appendOrFail workspaceRoot (buildEvent sessionID eventKindNudgeSettled payload (getTimestampMs().ToString()))

let appendSubsessionRunStartedOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (childId: string)
    (parentSessionId: string)
    (runId: string)
    : JS.Promise<unit> =
    let payload =
        Map [ "childId", childId; "parentSessionId", parentSessionId; "runId", runId ]

    appendOrFail
        workspaceRoot
        (buildEvent sessionID eventKindSubsessionRunStarted payload (getTimestampMs().ToString()))

let appendSubsessionRunSettledOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (childId: string)
    (runId: string)
    (status: string)
    : JS.Promise<unit> =
    let payload = Map [ "childId", childId; "runId", runId; "status", status ]

    appendOrFail
        workspaceRoot
        (buildEvent sessionID eventKindSubsessionRunSettled payload (getTimestampMs().ToString()))

let appendSubsessionDomainEventOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (kind: string)
    (payload: Map<string, string>)
    : JS.Promise<unit> =
    appendOrFail workspaceRoot (buildEvent sessionID kind payload (getTimestampMs().ToString()))

/// Atomic multi-event append for one Subsession Decision.
let appendSubsessionDomainEventsOrFail
    (workspaceRoot: string)
    (sessionID: string)
    (items: (string * Map<string, string>) list)
    : JS.Promise<unit> =
    if List.isEmpty items then
        Promise.lift ()
    else
        let at = getTimestampMs().ToString()

        let events =
            items |> List.map (fun (kind, payload) -> buildEvent sessionID kind payload at)

        appendEventsAndCacheOrFail workspaceRoot events
