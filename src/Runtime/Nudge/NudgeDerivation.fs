module Wanxiangshu.Runtime.Nudge.NudgeDerivation

open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Runtime.PromptFragments
open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Prompt
open Wanxiangshu.Kernel.HostTools
open global.Wanxiangshu.Kernel.Nudge.NudgeProjection
open global.Wanxiangshu.Kernel.Nudge.NudgeSnapshotSource
open global.Wanxiangshu.Kernel.Nudge.Types

type Snapshot = Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot

let deriveSnapshot (source: NudgeSnapshotSource) : Snapshot =
    let reviewLoopInfo =
        match source.reviewLoop with
        | Wanxiangshu.Kernel.Review.ReviewLoopFold.Active info ->
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

    let reviewTaskAvailable =
        match snapshot.reviewLoop with
        | Some info -> not (System.String.IsNullOrWhiteSpace info.originalTask)
        | None -> true

    match snapshot.blockStatus, snapshot.workState with
    | NudgeBlockStatus.Blocked, _ -> NudgeNone
    | _, SessionWorkState.Idle -> NudgeNone
    | _, SessionWorkState.RunnerWithTodos
    | _, SessionWorkState.AllAxes -> NudgeNone
    | _, SessionWorkState.RunnerWithLoop when reviewTaskAvailable && not (skipsReview text) -> NudgeLoop
    | _, SessionWorkState.RunnerWithLoop -> NudgeNone
    | _, SessionWorkState.RunnerOnly -> NudgeRunner
    | _, SessionWorkState.LoopWithTodos when not (skipsTodo text) -> NudgeTodo
    | _, SessionWorkState.LoopWithTodos when reviewTaskAvailable && not (skipsReview text) -> NudgeLoop
    | _, SessionWorkState.LoopIdle when reviewTaskAvailable && not (skipsReview text) -> NudgeLoop
    | _, SessionWorkState.TodosOnly when not (skipsTodo text) -> NudgeTodo
    | _ -> NudgeNone

let selectNudgePrompt (host: Host) (action: NudgeAction) (snapshot: Snapshot) : string option =
    match action with
    | NudgeTodo -> Some(todoNudgePromptFor snapshot.todos)
    | NudgeLoop ->
        match snapshot.reviewLoop with
        | Some info when not (System.String.IsNullOrWhiteSpace info.originalTask) ->
            let bgLines =
                [ sprintf "Review loop ID: %s, round: %d" info.reviewLoopId info.currentRound
                  match info.latestVerdict with
                  | Some v -> sprintf "Latest verdict: %s" v
                  | None -> ()
                  match info.latestFeedback with
                  | Some f -> sprintf "Latest feedback: %s" f
                  | None -> () ]

            let bgText = String.concat "\n" bgLines
            let targets = snapshot.todos |> List.map PromptTarget.TodoTarget

            let view =
                { objective = info.originalTask
                  background = Some bgText
                  agentRole = AgentRole.NudgeSupervisor
                  targets = targets
                  boundaries = []
                  rules =
                    [ PromptRule.Policy
                          "Call the submit_review tool to submit your detailed report and list of modified files for review before finishing." ]
                  outcomes =
                    [ { label = "continue"
                        text = "Submit review or complete remaining loop work." } ] }

            match PromptDocument.create view with
            | Ok doc -> Some(PromptToml.render doc)
            | Error _ -> Some(loopNudgePromptFor snapshot.todos)
        | Some _ -> None
        | None -> None
    | NudgeRunner -> Some(runnerNudgePromptFor host)
    | _ -> None
