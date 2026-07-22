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
      skipTodo = source.skipTodo
      skipReview = source.skipReview
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
          skipTodo = snap.skipTodo
          skipReview = snap.skipReview
          agentFromMessage = snap.agentFromMessage
          modelFromMessage = snap.modelFromMessage
          reviewLoop = snap.reviewLoop
          runnerPresence = runner
          blockStatus = blockStatus
          turnId = snap.turnId }

let deriveAction (snapshot: Snapshot) : NudgeAction =
    let reviewTaskAvailable =
        match snapshot.reviewLoop with
        | Some info -> not (System.String.IsNullOrWhiteSpace info.originalTask)
        | None -> true

    match snapshot.blockStatus, snapshot.workState with
    | NudgeBlockStatus.Blocked, _ -> NudgeNone
    | _, SessionWorkState.Idle -> NudgeNone
    | _, SessionWorkState.RunnerWithTodos
    | _, SessionWorkState.AllAxes -> NudgeNone
    | _, SessionWorkState.RunnerWithLoop when reviewTaskAvailable && not snapshot.skipReview -> NudgeLoop
    | _, SessionWorkState.RunnerWithLoop -> NudgeNone
    | _, SessionWorkState.RunnerOnly -> NudgeRunner
    | _, SessionWorkState.LoopWithTodos when not snapshot.skipTodo -> NudgeTodo
    | _, SessionWorkState.LoopWithTodos when reviewTaskAvailable && not snapshot.skipReview -> NudgeLoop
    | _, SessionWorkState.LoopIdle when reviewTaskAvailable && not snapshot.skipReview -> NudgeLoop
    | _, SessionWorkState.TodosOnly when not snapshot.skipTodo -> NudgeTodo
    | _ -> NudgeNone

let private loopNudgeWithReviewInfo (snapshot: Snapshot) (info: ReviewLoopSnapshotInfo) : string =
    let targets =
        [ yield PromptTarget.EvidenceTarget("review_mode", "active")
          match info.latestVerdict with
          | Some v when v.Trim() <> "" -> yield PromptTarget.EvidenceTarget("latest_verdict", v.Trim())
          | _ -> ()
          match info.latestFeedback with
          | Some f when f.Trim() <> "" -> yield PromptTarget.EvidenceTarget("latest_feedback", f.Trim())
          | _ -> ()
          yield! snapshot.todos |> List.map PromptTarget.TodoTarget ]

    let view =
        { objective = info.originalTask
          background = None
          agentRole = AgentRole.NudgeSupervisor
          targets = targets
          boundaries = []
          rules =
            [ PromptRule.Contract
                  "Call submit_review with a detailed report and the complete affected-file list before finishing." ]
          outcomes =
            [ { label = "continue"
                text = "Submit review or complete remaining loop work." } ] }

    match PromptDocument.create view with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Invalid loop nudge with review info: %A" errs

let selectNudgePrompt (host: Host) (action: NudgeAction) (snapshot: Snapshot) : string option =
    match action with
    | NudgeTodo -> Some(todoNudgePromptFor snapshot.todos)
    | NudgeLoop ->
        match snapshot.reviewLoop with
        | Some info when not (System.String.IsNullOrWhiteSpace info.originalTask) ->
            Some(loopNudgeWithReviewInfo snapshot info)
        | Some _ -> None
        | None -> None
    | NudgeRunner -> Some(runnerNudgePromptFor host)
    | _ -> None
