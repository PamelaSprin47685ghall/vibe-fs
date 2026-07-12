module Wanxiangshu.Kernel.NudgeDerivation

open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.EventLog.Fold
open Wanxiangshu.Kernel.Nudge.NudgeSnapshotSource
open Wanxiangshu.Kernel.Nudge.Types

type Snapshot = Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot

let deriveSnapshot (source: NudgeSnapshotSource) : Snapshot =
    let reviewLoopInfo =
        match source.reviewLoop with
        | Wanxiangshu.Kernel.EventLog.ReviewLoopFold.Active info ->
            Some
                { originalTask = info.task
                  reviewLoopId = info.reviewLoopId
                  currentRound = info.currentRound
                  latestVerdict = info.latestVerdict
                  latestFeedback = info.latestFeedback }
        | _ -> None

    { todos = source.openTodos
      lastAssistantMessage = source.lastAssistantText
      workState = workStateFromSource source
      blockStatus = source.blockStatus
      nudgeAnchorKey = nudgeAnchorKeyForSource source
      agentFromMessage = source.agentFromMessage
      modelFromMessage = source.modelFromMessage
      reviewLoop = reviewLoopInfo
      humanTurnId = Some source.turnId }

let sessionSnapshotFromFold
    (snap: NudgeSnapshotState)
    (runner: RunnerPresence)
    (blockStatus: NudgeBlockStatus)
    : Snapshot =
    deriveSnapshot
        { openTodos = snap.openTodos
          lastAssistantText = snap.lastAssistantText
          agentFromMessage = snap.agentFromMessage
          modelFromMessage = snap.modelFromMessage
          reviewLoop = snap.reviewLoop
          runnerPresence = runner
          blockStatus = blockStatus
          turnId = snap.turnId }

let deriveAction (snapshot: Snapshot) : NudgeAction =
    let text = snapshot.lastAssistantMessage.Trim()

    match snapshot.blockStatus, snapshot.workState with
    | NudgeBlockStatus.Blocked, _ -> NudgeNone
    | _, SessionWorkState.Idle -> NudgeNone
    | _, SessionWorkState.RunnerWithBacklog
    | _, SessionWorkState.AllAxes -> NudgeNone
    | _, SessionWorkState.RunnerWithLoop when not (skipsReview text) -> NudgeLoop
    | _, SessionWorkState.RunnerWithLoop -> NudgeNone
    | _, SessionWorkState.RunnerOnly -> NudgeRunner
    | _, SessionWorkState.LoopWithBacklog when not (skipsTodo text) -> NudgeTodo
    | _, SessionWorkState.LoopWithBacklog when not (skipsReview text) -> NudgeLoop
    | _, SessionWorkState.LoopIdle when not (skipsReview text) -> NudgeLoop
    | _, SessionWorkState.BacklogOnly when not (skipsTodo text) -> NudgeTodo
    | _ -> NudgeNone

let selectNudgePrompt (host: Host) (action: NudgeAction) (snapshot: Snapshot) : string option =
    match action with
    | NudgeTodo -> Some(todoNudgePromptFor snapshot.todos)
    | NudgeLoop ->
        match snapshot.reviewLoop with
        | Some info ->
            let fields =
                [ yamlField "original_task" info.originalTask
                  yamlField "review_loop_id" info.reviewLoopId
                  yamlField "review_round" (info.currentRound.ToString())
                  yamlField "prompt_origin" "review_nudge" ]
                @ (match info.latestVerdict with
                   | Some v -> [ yamlField "latest_verdict" v ]
                   | None -> [])
                @ (match info.latestFeedback with
                   | Some f -> [ yamlField "latest_feedback" f ]
                   | None -> [])
                @ (if not snapshot.todos.IsEmpty then
                       [ yamlStringSeqField "todos" snapshot.todos ]
                   else
                       [])

            Some(frontMatterPrompt fields (loopNudgePromptFor snapshot.todos))
        | None -> None
    | NudgeRunner -> Some(runnerNudgePromptFor host)
    | _ -> None
