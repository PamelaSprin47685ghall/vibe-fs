module Wanxiangshu.Shell.EventLogRuntimeAppend

open Fable.Core
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Shell.EventLogCodec
open Wanxiangshu.Shell.EventLogRuntimeStore
open Wanxiangshu.Shell.Clock
open Wanxiangshu.Shell.WorkBacklogToolsCodec
open Thoth.Json

let appendLoopActivated (workspaceRoot: string) (sessionID: string) (task: string) : JS.Promise<Result<unit, string>> =
    appendAndCache workspaceRoot (buildEvent sessionID eventKindLoopActivated (Map [ "task", task ]) (getTimestampMs().ToString()))

let appendLoopCancelled (workspaceRoot: string) (sessionID: string) : JS.Promise<Result<unit, string>> =
    appendAndCache workspaceRoot (buildEvent sessionID eventKindLoopCancelled Map.empty (getTimestampMs().ToString()))

let appendReviewVerdict
    (workspaceRoot: string) (sessionID: string) (verdict: string) (feedback: string option)
    : JS.Promise<Result<unit, string>> =
    let payload =
        match feedback with
        | Some f when f <> "" -> Map.add "feedback" f (Map [ "verdict", verdict ])
        | _ -> Map [ "verdict", verdict ]
    appendAndCache workspaceRoot (buildEvent sessionID eventKindReviewVerdict payload (getTimestampMs().ToString()))

let appendSubmitReviewWipRecorded (workspaceRoot: string) (sessionID: string) : JS.Promise<Result<unit, string>> =
    appendAndCache workspaceRoot (buildEvent sessionID eventKindSubmitReviewWipRecorded Map.empty (getTimestampMs().ToString()))

let appendNudgeDedupCleared (workspaceRoot: string) (sessionID: string) : JS.Promise<Result<unit, string>> =
    appendAndCache workspaceRoot (buildEvent sessionID eventKindNudgeDedupCleared Map.empty (getTimestampMs().ToString()))

let private backlogPayload (args: TodoWriteArgs) =
    Map
        [ "ahaMoments", args.AhaMoments
          "changesAndReasons", args.ChangesAndReasons
          "gotchas", args.Gotchas
          "lessonsAndConventions", args.LessonsAndConventions
          "plan", args.Plan
          "todosJson", Encode.Auto.toString (0, args.Todos)
          "selectMethodologyJson", Encode.Auto.toString (0, args.SelectMethodology) ]

let appendWorkBacklogCommitted (workspaceRoot: string) (sessionID: string) (args: TodoWriteArgs) : JS.Promise<Result<unit, string>> =
    appendAndCache workspaceRoot (buildEvent sessionID eventKindWorkBacklogCommitted (backlogPayload args) (getTimestampMs().ToString()))

let appendLoopActivatedOrFail (workspaceRoot: string) (sessionID: string) (task: string) : JS.Promise<unit> =
    appendAndCacheOrFail workspaceRoot (buildEvent sessionID eventKindLoopActivated (Map [ "task", task ]) (getTimestampMs().ToString()))

let appendLoopCancelledOrFail (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    appendAndCacheOrFail workspaceRoot (buildEvent sessionID eventKindLoopCancelled Map.empty (getTimestampMs().ToString()))

let appendReviewVerdictOrFail
    (workspaceRoot: string) (sessionID: string) (verdict: string) (feedback: string option)
    : JS.Promise<unit> =
    let payload =
        match feedback with
        | Some f when f <> "" -> Map.add "feedback" f (Map [ "verdict", verdict ])
        | _ -> Map [ "verdict", verdict ]
    appendAndCacheOrFail workspaceRoot (buildEvent sessionID eventKindReviewVerdict payload (getTimestampMs().ToString()))

let appendSubmitReviewWipRecordedOrFail (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    appendAndCacheOrFail workspaceRoot (buildEvent sessionID eventKindSubmitReviewWipRecorded Map.empty (getTimestampMs().ToString()))

let appendNudgeDedupClearedOrFail (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    appendAndCacheOrFail workspaceRoot (buildEvent sessionID eventKindNudgeDedupCleared Map.empty (getTimestampMs().ToString()))

let appendWorkBacklogCommittedOrFail (workspaceRoot: string) (sessionID: string) (args: TodoWriteArgs) : JS.Promise<unit> =
    appendAndCacheOrFail workspaceRoot (buildEvent sessionID eventKindWorkBacklogCommitted (backlogPayload args) (getTimestampMs().ToString()))

let appendAssistantCompleted
    (workspaceRoot: string) (sessionID: string) (assistantMessage: string)
    (agent: string option) (model: string option) (turnId: string) (openTodos: string list)
    : JS.Promise<Result<unit, string>> =
    appendAndCache workspaceRoot (buildEvent sessionID eventKindAssistantCompleted (assistantPayload assistantMessage agent model turnId openTodos) (getTimestampMs().ToString()))

let appendAssistantCompletedOrFail
    (workspaceRoot: string) (sessionID: string) (assistantMessage: string)
    (agent: string option) (model: string option) (turnId: string) (openTodos: string list)
    : JS.Promise<unit> =
    appendAndCacheOrFail workspaceRoot (buildEvent sessionID eventKindAssistantCompleted (assistantPayload assistantMessage agent model turnId openTodos) (getTimestampMs().ToString()))