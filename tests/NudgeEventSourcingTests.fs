module Wanxiangshu.Tests.NudgeEventSourcingTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Review
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Runtime.Nudge.NudgeDerivation
open Wanxiangshu.Kernel.Review.ReviewLoopFold
open Wanxiangshu.Kernel.Nudge.Types

let private ev session kind payload =
    { V = 1
      Session = session
      Kind = kind
      At = ""
      Payload = payload
      EventId = None
      WriterId = None
      Sequence = None
      Checksum = None }

let private toSessionSnapshot (s: NudgeSnapshotState) : SessionSnapshot =
    let reviewLoopInfo =
        match s.reviewLoop with
        | ReviewLoopFold.Active info ->
            Some
                { originalTask = info.task
                  reviewLoopId = info.reviewLoopId
                  currentRound = info.currentRound
                  latestVerdict = info.latestVerdict
                  latestFeedback = info.latestFeedback }
        | _ -> None

    { todos = s.openTodos
      lastAssistantMessage = s.lastAssistantText
      skipTodo = s.skipTodo
      skipReview = s.skipReview
      workState = s.workState
      blockStatus = NudgeBlockStatus.Allowed
      nudgeAnchorKey =
          Wanxiangshu.Kernel.Nudge.NudgeProjection.nudgeAnchorKey s.turnId s.agentFromMessage s.modelFromMessage
      agentFromMessage = s.agentFromMessage
      modelFromMessage = s.modelFromMessage
      reviewLoop = reviewLoopInfo
      humanTurnId = Some s.turnId }

/// No events → all fields empty/default.
let foldNudgeSnapshotEmpty () =
    let s = Wanxiangshu.Kernel.Nudge.NudgeProjection.foldSnapshotStream "s1" []
    check "empty: no todos" (s.openTodos = [])
    check "empty: no text" (s.lastAssistantText = "")
    check "empty: no agent" (s.agentFromMessage = None)
    check "empty: no turnId" (s.turnId = "")

    check "empty: not loop active" (not (isLoopActive s.reviewLoop))

    check "empty: no dispatched anchors" s.lastDispatchedAnchor.IsNone

/// assistant_completed populates lastAssistantText, agentFromMessage, turnId, modelFromMessage.
let foldNudgeSnapshotAssistantCompleted () =
    let events =
        [ ev
              "s1"
              eventKindAssistantCompleted
              (Map
                  [ "assistantMessage", "implemented feature"
                    "agent", "bookkeeper"
                    "model", "anthropic/claude-sonnet"
                    "turnId", "t1" ]) ]

    let s = Wanxiangshu.Kernel.Nudge.NudgeProjection.foldSnapshotStream "s1" events
    equal "assistant text" "implemented feature" s.lastAssistantText
    equal "agent from message" (Some "bookkeeper") s.agentFromMessage
    equal "model from message" (Some "anthropic/claude-sonnet") s.modelFromMessage
    equal "turnId" "t1" s.turnId

/// loop_activated sets isLoopActive=true.
let foldNudgeSnapshotLoopActivated () =
    let events = [ ev "s1" eventKindLoopActivated (Map [ "task", "ship it" ]) ]
    let s = Wanxiangshu.Kernel.Nudge.NudgeProjection.foldSnapshotStream "s1" events

    check "loop active after activate" (isLoopActive s.reviewLoop)
    check "no todos" (s.openTodos = [])
    check "no text" (s.lastAssistantText = "")
    check "no agent" (s.agentFromMessage = None)
    check "no dispatched anchors" s.lastDispatchedAnchor.IsNone

/// loop_cancelled clears isLoopActive.
let foldNudgeSnapshotLoopCancelled () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship it" ])
          ev "s1" eventKindLoopCancelled Map.empty ]

    let s = Wanxiangshu.Kernel.Nudge.NudgeProjection.foldSnapshotStream "s1" events
    check "not loop active after cancel" (not (isLoopActive s.reviewLoop))
    check "no todos" (s.openTodos = [])
    check "no text" (s.lastAssistantText = "")
    check "no agent" (s.agentFromMessage = None)
    check "no dispatched anchors" s.lastDispatchedAnchor.IsNone

/// review_verdict accepted clears isLoopActive.
let foldNudgeSnapshotAcceptedClears () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship it" ])
          ev "s1" eventKindReviewVerdict (Map [ "verdict", verdictAccepted ]) ]

    let s = Wanxiangshu.Kernel.Nudge.NudgeProjection.foldSnapshotStream "s1" events
    check "not loop active after accept" (not (isLoopActive s.reviewLoop))

/// review_verdict needs_revision keeps isLoopActive.
let foldNudgeSnapshotNeedsRevisionKeeps () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship it" ])
          ev "s1" eventKindReviewVerdict (Map [ "verdict", verdictNeedsRevision ]) ]

    let s = Wanxiangshu.Kernel.Nudge.NudgeProjection.foldSnapshotStream "s1" events
    check "loop active after needs_revision" (isLoopActive s.reviewLoop)

/// nudge_dispatched sets lastDispatchedAnchor to the latest anchor.
let foldNudgeSnapshotNudgeDispatched () =
    let events =
        [ ev "s1" eventKindNudgeDispatched (Map [ "anchor", "t1" ])
          ev "s1" eventKindNudgeDispatched (Map [ "anchor", "t2" ]) ]

    let s = Wanxiangshu.Kernel.Nudge.NudgeProjection.foldSnapshotStream "s1" events
    equal "dispatched contains anchor 2" (Some "t2") s.lastDispatchedAnchor

/// submit_review_wip_recorded clears lastDispatchedAnchor.
let foldNudgeSnapshotDedupCleared () =
    let events =
        [ ev "s1" eventKindNudgeDispatched (Map [ "anchor", "t1" ])
          ev "s1" eventKindSubmitReviewWipRecorded Map.empty ]

    let s = Wanxiangshu.Kernel.Nudge.NudgeProjection.foldSnapshotStream "s1" events
    check "dispatched anchors cleared" s.lastDispatchedAnchor.IsNone

/// Cold start (no events) → deriveAction returns NudgeNone.
let foldNudgeSnapshotColdStartNudgeNone () =
    let s = Wanxiangshu.Kernel.Nudge.NudgeProjection.foldSnapshotStream "s1" []

    match deriveAction (toSessionSnapshot s) with
    | NudgeNone -> check "cold start -> NudgeNone" true
    | _ -> check "cold start -> NudgeNone" false

let run () =
    foldNudgeSnapshotEmpty ()
    foldNudgeSnapshotAssistantCompleted ()
    foldNudgeSnapshotLoopActivated ()
    foldNudgeSnapshotLoopCancelled ()
    foldNudgeSnapshotAcceptedClears ()
    foldNudgeSnapshotNeedsRevisionKeeps ()
    foldNudgeSnapshotNudgeDispatched ()
    foldNudgeSnapshotDedupCleared ()
    foldNudgeSnapshotColdStartNudgeNone ()
