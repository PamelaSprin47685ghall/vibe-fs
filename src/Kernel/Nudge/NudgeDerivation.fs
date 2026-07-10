module Wanxiangshu.Kernel.NudgeDerivation

open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.EventLog.Fold
open Wanxiangshu.Kernel.Nudge.Types

type SnapshotInput =
    { openTodos: string list
      lastAssistantText: string
      agentFromMessage: string option
      modelFromMessage: string option
      isLoopActive: bool
      hasActiveRunner: bool
      nudgeBlockedForTurn: bool
      turnId: string }

type Snapshot = Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot

let deriveSnapshot (input: SnapshotInput) : Snapshot =
    let workState =
        getSessionWorkState input.hasActiveRunner input.isLoopActive input.openTodos

    let blockStatus =
        if input.nudgeBlockedForTurn then
            NudgeBlockStatus.Blocked
        else
            NudgeBlockStatus.Allowed

    { todos = input.openTodos
      lastAssistantMessage = input.lastAssistantText
      workState = workState
      blockStatus = blockStatus
      nudgeAnchorKey = nudgeAnchorKey input.turnId input.lastAssistantText
      agentFromMessage = input.agentFromMessage
      modelFromMessage = input.modelFromMessage }

let deriveAction (snapshot: Snapshot) : NudgeAction =
    let text = snapshot.lastAssistantMessage.Trim()

    match snapshot.blockStatus, snapshot.workState with
    | NudgeBlockStatus.Blocked, _ -> NudgeNone
    | _, SessionWorkState.Idle -> NudgeNone
    | _, SessionWorkState.RunnerActive(true, _) -> NudgeNone
    | _, SessionWorkState.RunnerActive(false, true) when not (skipsReview text) -> NudgeLoop
    | _, SessionWorkState.RunnerActive(false, false) -> NudgeRunner
    | _, SessionWorkState.RunnerActive(false, true) -> NudgeNone
    | _, SessionWorkState.LoopActive true when not (skipsTodo text) -> NudgeTodo
    | _, SessionWorkState.LoopActive _ when not (skipsReview text) -> NudgeLoop
    | _, SessionWorkState.BacklogActive _ when not (skipsTodo text) -> NudgeTodo
    | _ -> NudgeNone

let selectNudgePrompt (host: Host) (action: NudgeAction) (snapshot: Snapshot) : string option =
    match action with
    | NudgeTodo -> Some(todoNudgePromptFor snapshot.todos)
    | NudgeLoop -> Some(loopNudgePromptFor snapshot.todos)
    | NudgeRunner -> Some(runnerNudgePromptFor host)
    | _ -> None
