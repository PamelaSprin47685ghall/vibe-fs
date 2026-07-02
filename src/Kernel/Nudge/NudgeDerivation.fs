module Wanxiangshu.Kernel.NudgeDerivation

open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.Nudge.SubmitReviewHooks
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.HostTools

let todoNudgePrompt = Wanxiangshu.Kernel.PromptFragments.todoNudgePrompt
let loopNudgePrompt = Wanxiangshu.Kernel.PromptFragments.loopNudgePrompt
let submitReviewWipAcknowledgment = Wanxiangshu.Kernel.ReviewPrompts.submitReviewWipAcknowledgment
let submitReviewWipToolClearsNudgeDedup = Wanxiangshu.Kernel.Nudge.SubmitReviewHooks.submitReviewWipToolClearsNudgeDedup

let deriveAlreadyNudged (tailTexts: string list) : bool =
    tailTexts
    |> List.filter (fun text -> text.Trim() <> "")
    |> List.tryLast
    |> Option.exists isNudgePrompt

type SnapshotInput =
    { tailTexts: string list
      openTodos: string list
      lastAssistantText: string
      agentFromMessage: string option
      isLoopActive: bool
      lastAssistantIsCompaction: bool
      hasActiveRunner: bool }

type Snapshot = Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot

let deriveSnapshot (input: SnapshotInput) : Snapshot =
    { todos = input.openTodos
      lastAssistantMessage = input.lastAssistantText
      isLoopActive = input.isLoopActive
      alreadyNudged = deriveAlreadyNudged input.tailTexts
      agentFromMessage = input.agentFromMessage
      lastAssistantIsCompaction = input.lastAssistantIsCompaction
      anchorPromptIssued = false
      hasActiveRunner = input.hasActiveRunner }

let deriveAction
    (snapshot: Snapshot)
    (lastSentAction: NudgeAction option)
    (lastSentMessageOpt: string option)
    : NudgeAction =
    if snapshot.alreadyNudged then NudgeNone
    elif snapshot.todos.IsEmpty && not snapshot.isLoopActive && not snapshot.hasActiveRunner then NudgeNone
    else
        let text = snapshot.lastAssistantMessage.Trim()
        if text = "" || isQuestion text then NudgeNone
        elif skipsTodo text || skipsLoop text then NudgeNone
        else
            let desired =
                if snapshot.hasActiveRunner && not snapshot.todos.IsEmpty then NudgeNone
                elif snapshot.hasActiveRunner && snapshot.todos.IsEmpty && not snapshot.isLoopActive then NudgeRunner
                elif not snapshot.todos.IsEmpty then NudgeTodo
                elif snapshot.isLoopActive then NudgeLoop
                else NudgeNone
            match lastSentAction with
            | Some prev when prev = desired && Option.exists (fun m -> m = snapshot.lastAssistantMessage) lastSentMessageOpt -> NudgeNone
            | _ -> desired

let selectNudgePrompt = function
    | NudgeTodo -> Some todoNudgePrompt
    | NudgeLoop -> Some loopNudgePrompt
    | NudgeRunner -> Some (runnerNudgePromptFor omp)
    | _ -> None
