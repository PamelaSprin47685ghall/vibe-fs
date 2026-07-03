module Wanxiangshu.Kernel.NudgeDerivation

open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.EventLog.Fold

let todoNudgePrompt = Wanxiangshu.Kernel.PromptFragments.todoNudgePrompt
let loopNudgePrompt = Wanxiangshu.Kernel.PromptFragments.loopNudgePrompt

type SnapshotInput =
    { openTodos: string list
      lastAssistantText: string
      agentFromMessage: string option
      isLoopActive: bool
      lastAssistantIsCompaction: bool
      hasActiveRunner: bool
      nudgeBlockedForTurn: bool
      turnId: string }

type Snapshot = Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot

let deriveSnapshot (input: SnapshotInput) : Snapshot =
    { todos = input.openTodos
      lastAssistantMessage = input.lastAssistantText
      isLoopActive = input.isLoopActive
      nudgeBlockedForTurn = input.nudgeBlockedForTurn
      nudgeAnchorKey = nudgeAnchorKey input.turnId input.lastAssistantText
      agentFromMessage = input.agentFromMessage
      lastAssistantIsCompaction = input.lastAssistantIsCompaction
      anchorPromptIssued = false
      hasActiveRunner = input.hasActiveRunner }

let deriveAction (snapshot: Snapshot) : NudgeAction =
    if snapshot.nudgeBlockedForTurn then NudgeNone
    elif snapshot.todos.IsEmpty && not snapshot.isLoopActive && not snapshot.hasActiveRunner then NudgeNone
    else
        let text = snapshot.lastAssistantMessage.Trim()
        if text = "" || isQuestion text then NudgeNone
        elif skipsTodo text || skipsLoop text then NudgeNone
        else
            if snapshot.hasActiveRunner && not snapshot.todos.IsEmpty then NudgeNone
            elif snapshot.hasActiveRunner && snapshot.todos.IsEmpty && not snapshot.isLoopActive then NudgeRunner
            elif not snapshot.todos.IsEmpty then NudgeTodo
            elif snapshot.isLoopActive then NudgeLoop
            else NudgeNone

let selectNudgePrompt = function
    | NudgeTodo -> Some todoNudgePrompt
    | NudgeLoop -> Some loopNudgePrompt
    | NudgeRunner -> Some (runnerNudgePromptFor omp)
    | _ -> None
