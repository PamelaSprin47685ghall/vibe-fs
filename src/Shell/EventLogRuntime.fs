module Wanxiangshu.Shell.EventLogRuntime

open Fable.Core
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.EventLog.Fold
open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Shell.EventLogCodec
open Wanxiangshu.Shell.EventLogFiles
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.ReviewReplaySync
open Wanxiangshu.Shell.Clock
open Wanxiangshu.Shell.WorkBacklogToolsCodec
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.SessionProjectionStore
open Wanxiangshu.Shell.RuntimeScope
open Thoth.Json

[<Import("stat", "node:fs/promises")>]
let private statAsync (path: string) : JS.Promise<obj> = jsNative

let private directoryExists (path: string) : JS.Promise<bool> =
    promise {
        try
            let! stats = statAsync path
            return unbox<bool> (Wanxiangshu.Shell.Dyn.callMethod0 stats "isDirectory")
        with _ ->
            return false
    }

let mutable private stores: Map<string, EventLogStore> = Map.empty

let getStore (workspaceRoot: string) : EventLogStore =
    match Map.tryFind workspaceRoot stores with
    | Some s -> s
    | None ->
        let s = EventLogStore workspaceRoot
        stores <- Map.add workspaceRoot s stores
        s

let private appendAndCache (workspaceRoot: string) (e: WanEvent) : JS.Promise<Result<unit, string>> =
    getStore(workspaceRoot).AppendEvent e

let private appendAndCacheOrFail (workspaceRoot: string) (e: WanEvent) : JS.Promise<unit> =
    getStore(workspaceRoot).AppendEventOrFail e

let isLoopActiveFromEventLog (workspaceRoot: string) (sessionID: string) : JS.Promise<bool> =
    promise {
        if sessionID = "" || workspaceRoot = "" then return false
        else
            let! state = getStore(workspaceRoot).GetSessionState(sessionID)
            return state.ReviewTask |> Option.isSome
    }

/// Dedicated startup path: sync review + backlog for all sessions from a single
/// read of the event file. Called once at plugin init, not on every interaction.
let syncAllSessionsFromEventLogDedicated (host: Host) (store: ReviewStore) (scope: RuntimeScope) (workspaceRoot: string) : JS.Promise<unit> =
    promise {
        try
            if workspaceRoot = "" then ()
            else
                let! exists = directoryExists workspaceRoot
                if exists then
                    let! allEvents = getStore(workspaceRoot).ReadAllEvents()
                    let sessionIds =
                        allEvents
                        |> Seq.map (fun e -> e.Session)
                        |> Seq.distinct
                        |> Seq.toList
                    for sid in sessionIds do
                        let! state = getStore(workspaceRoot).GetSessionState(sid)
                        syncReviewProjection store sid state.ReviewTask
                        scope.Projection.StoreBacklog(host, sid, state.Backlog)
        with _ ->
            ()
    }

/// Dedicated single-session review sync for startup paths.
let syncReviewFromEventLogDedicated (store: ReviewStore) (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    promise {
        try
            if sessionID = "" || workspaceRoot = "" then ()
            else
                let! exists = directoryExists workspaceRoot
                if exists then
                    let! state = getStore(workspaceRoot).GetSessionState(sessionID)
                    syncReviewProjection store sessionID state.ReviewTask
        with _ ->
            ()
    }

/// Dedicated single-session backlog sync for startup paths.
let syncBacklogFromEventLogDedicated (host: Host) (projection: ProjectionStore) (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    promise {
        try
            if sessionID = "" || workspaceRoot = "" then ()
            else
                let! exists = directoryExists workspaceRoot
                if exists then
                    let! state = getStore(workspaceRoot).GetSessionState(sessionID)
                    projection.StoreBacklog(host, sessionID, state.Backlog)
        with _ ->
            ()
    }

let appendLoopActivated (workspaceRoot: string) (sessionID: string) (task: string) : JS.Promise<Result<unit, string>> =
    let payload = Map [ "task", task ]
    appendAndCache workspaceRoot (buildEvent sessionID eventKindLoopActivated payload (getTimestampMs().ToString()))

let appendLoopCancelled (workspaceRoot: string) (sessionID: string) : JS.Promise<Result<unit, string>> =
    appendAndCache workspaceRoot (buildEvent sessionID eventKindLoopCancelled Map.empty (getTimestampMs().ToString()))

let appendReviewVerdict (workspaceRoot: string) (sessionID: string) (verdict: string) (feedback: string option) : JS.Promise<Result<unit, string>> =
    let baseMap = Map [ "verdict", verdict ]
    let payload =
        match feedback with
        | Some f when f <> "" -> Map.add "feedback" f baseMap
        | _ -> baseMap
    appendAndCache workspaceRoot (buildEvent sessionID eventKindReviewVerdict payload (getTimestampMs().ToString()))

let nudgeBlockedForTurn (workspaceRoot: string) (sessionID: string) (assistantMessage: string) : JS.Promise<bool> =
    promise {
        if sessionID = "" || workspaceRoot = "" then return false
        else
            let! state = getStore(workspaceRoot).GetSessionState(sessionID)
            return isNudgeBlockedForAnchor state.NudgeDedup assistantMessage
    }

let tryClaimNudgeDispatch (workspaceRoot: string) (sessionID: string) (action: NudgeAction) (anchor: string) : JS.Promise<bool> =
    getStore(workspaceRoot).TryClaimNudgeDispatch sessionID action anchor isNudgeBlockedForAnchor

let getNudgeSnapshotFromEventLog (workspaceRoot: string) (sessionID: string) : JS.Promise<NudgeSnapshotState> =
    promise {
        if sessionID = "" || workspaceRoot = "" then return emptyNudgeSnapshotState
        else
            let! state = getStore(workspaceRoot).GetSessionState(sessionID)
            return state.NudgeSnapshot
    }

let appendSubmitReviewWipRecorded (workspaceRoot: string) (sessionID: string) : JS.Promise<Result<unit, string>> =
    appendAndCache workspaceRoot (buildEvent sessionID eventKindSubmitReviewWipRecorded Map.empty (getTimestampMs().ToString()))

let appendNudgeDedupCleared (workspaceRoot: string) (sessionID: string) : JS.Promise<Result<unit, string>> =
    appendAndCache workspaceRoot (buildEvent sessionID eventKindNudgeDedupCleared Map.empty (getTimestampMs().ToString()))

let appendWorkBacklogCommitted (workspaceRoot: string) (sessionID: string) (args: TodoWriteArgs) : JS.Promise<Result<unit, string>> =
    let todosJson = Encode.Auto.toString(0, args.Todos)
    let methJson = Encode.Auto.toString(0, args.SelectMethodology)
    let payload =
        Map
            [ "ahaMoments", args.AhaMoments
              "changesAndReasons", args.ChangesAndReasons
              "gotchas", args.Gotchas
              "lessonsAndConventions", args.LessonsAndConventions
              "plan", args.Plan
              "todosJson", todosJson
              "selectMethodologyJson", methJson ]
    appendAndCache workspaceRoot (buildEvent sessionID eventKindWorkBacklogCommitted payload (getTimestampMs().ToString()))

let appendLoopActivatedOrFail (workspaceRoot: string) (sessionID: string) (task: string) : JS.Promise<unit> =
    let payload = Map [ "task", task ]
    appendAndCacheOrFail workspaceRoot (buildEvent sessionID eventKindLoopActivated payload (getTimestampMs().ToString()))

let appendLoopCancelledOrFail (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    appendAndCacheOrFail workspaceRoot (buildEvent sessionID eventKindLoopCancelled Map.empty (getTimestampMs().ToString()))

let appendReviewVerdictOrFail (workspaceRoot: string) (sessionID: string) (verdict: string) (feedback: string option) : JS.Promise<unit> =
    let baseMap = Map [ "verdict", verdict ]
    let payload = match feedback with Some f when f <> "" -> Map.add "feedback" f baseMap | _ -> baseMap
    appendAndCacheOrFail workspaceRoot (buildEvent sessionID eventKindReviewVerdict payload (getTimestampMs().ToString()))

let appendSubmitReviewWipRecordedOrFail (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    appendAndCacheOrFail workspaceRoot (buildEvent sessionID eventKindSubmitReviewWipRecorded Map.empty (getTimestampMs().ToString()))

let appendNudgeDedupClearedOrFail (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    appendAndCacheOrFail workspaceRoot (buildEvent sessionID eventKindNudgeDedupCleared Map.empty (getTimestampMs().ToString()))

let appendWorkBacklogCommittedOrFail (workspaceRoot: string) (sessionID: string) (args: TodoWriteArgs) : JS.Promise<unit> =
    let payload = Map [ "ahaMoments", args.AhaMoments; "changesAndReasons", args.ChangesAndReasons; "gotchas", args.Gotchas; "lessonsAndConventions", args.LessonsAndConventions; "plan", args.Plan; "todosJson", Encode.Auto.toString(0, args.Todos); "selectMethodologyJson", Encode.Auto.toString(0, args.SelectMethodology) ]
    appendAndCacheOrFail workspaceRoot (buildEvent sessionID eventKindWorkBacklogCommitted payload (getTimestampMs().ToString()))

let appendAssistantCompleted (workspaceRoot: string) (sessionID: string) (assistantMessage: string) (agent: string option) (model: string option) (turnId: string) (openTodos: string list) : JS.Promise<Result<unit, string>> =
    let baseMap = Map [ "assistantMessage", assistantMessage; "turnId", turnId; "openTodosJson", Encode.Auto.toString(0, openTodos) ]
    let withAgent =
        match agent with
        | Some a when a <> "" -> Map.add "agent" a baseMap
        | _ -> baseMap
    let payload =
        match model with
        | Some m when m <> "" -> Map.add "model" m withAgent
        | _ -> withAgent
    appendAndCache workspaceRoot (buildEvent sessionID eventKindAssistantCompleted payload (getTimestampMs().ToString()))

let appendAssistantCompletedOrFail (workspaceRoot: string) (sessionID: string) (assistantMessage: string) (agent: string option) (model: string option) (turnId: string) (openTodos: string list) : JS.Promise<unit> =
    let baseMap = Map [ "assistantMessage", assistantMessage; "turnId", turnId; "openTodosJson", Encode.Auto.toString(0, openTodos) ]
    let withAgent =
        match agent with
        | Some a when a <> "" -> Map.add "agent" a baseMap
        | _ -> baseMap
    let payload =
        match model with
        | Some m when m <> "" -> Map.add "model" m withAgent
        | _ -> withAgent
    appendAndCacheOrFail workspaceRoot (buildEvent sessionID eventKindAssistantCompleted payload (getTimestampMs().ToString()))

let verdictStringFromReviewResult (result: Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult) : string * string option =
    match result with
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Accepted fb ->
        (verdictAccepted, Some fb)
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.NeedsRevision fb ->
        (verdictNeedsRevision, Some fb)
    | Wanxiangshu.Kernel.ReviewSession.Types.ReviewResult.Terminated ->
        (verdictTerminated, None)
