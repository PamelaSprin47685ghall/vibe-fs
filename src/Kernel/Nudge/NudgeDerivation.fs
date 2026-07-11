module Wanxiangshu.Kernel.NudgeDerivation

open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.EventLog.Fold
open Wanxiangshu.Kernel.Nudge.NudgeSnapshotSource
open Wanxiangshu.Kernel.Nudge.Types

type Snapshot = Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot

let deriveSnapshot (source: NudgeSnapshotSource) : Snapshot =
    { todos = source.openTodos
      lastAssistantMessage = source.lastAssistantText
      workState = workStateFromSource source
      blockStatus = source.blockStatus
      nudgeAnchorKey = nudgeAnchorKeyForSource source
      agentFromMessage = source.agentFromMessage
      modelFromMessage = source.modelFromMessage }

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
    | NudgeLoop -> Some(loopNudgePromptFor snapshot.todos)
    | NudgeRunner -> Some(runnerNudgePromptFor host)
    | _ -> None
