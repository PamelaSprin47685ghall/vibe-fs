module Wanxiangshu.Tests.NudgeEventSourcingTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.EventLog.Fold
open Wanxiangshu.Kernel.EventLog.NudgeProjection
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.EventLog.ReviewLoopFold
open Wanxiangshu.Kernel.Nudge.Types

let private ev session kind payload =
    { V = 1
      Session = session
      Kind = kind
      At = ""
      Payload = payload }

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
      workState = s.workState
      blockStatus = NudgeBlockStatus.Allowed
      nudgeAnchorKey = nudgeAnchorKey s.turnId s.lastAssistantText
      agentFromMessage = s.agentFromMessage
      modelFromMessage = s.modelFromMessage
      reviewLoop = reviewLoopInfo
      humanTurnId = Some s.turnId }

/// No events → all fields empty/default.
let foldNudgeSnapshotEmpty () =
    let s = foldNudgeSnapshot "s1" []
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

    let s = foldNudgeSnapshot "s1" events
    equal "assistant text" "implemented feature" s.lastAssistantText
    equal "agent from message" (Some "bookkeeper") s.agentFromMessage
    equal "model from message" (Some "anthropic/claude-sonnet") s.modelFromMessage
    equal "turnId" "t1" s.turnId

/// loop_activated sets isLoopActive=true.
let foldNudgeSnapshotLoopActivated () =
    let events = [ ev "s1" eventKindLoopActivated (Map [ "task", "ship it" ]) ]
    let s = foldNudgeSnapshot "s1" events

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

    let s = foldNudgeSnapshot "s1" events
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

    let s = foldNudgeSnapshot "s1" events
    check "not loop active after accept" (not (isLoopActive s.reviewLoop))

/// review_verdict needs_revision keeps isLoopActive.
let foldNudgeSnapshotNeedsRevisionKeeps () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship it" ])
          ev "s1" eventKindReviewVerdict (Map [ "verdict", verdictNeedsRevision ]) ]

    let s = foldNudgeSnapshot "s1" events
    check "loop active after needs_revision" (isLoopActive s.reviewLoop)

/// nudge_dispatched sets lastDispatchedAnchor to the latest anchor.
let foldNudgeSnapshotNudgeDispatched () =
    let events =
        [ ev "s1" eventKindNudgeDispatched (Map [ "anchor", "t1\u001emsg body" ])
          ev "s1" eventKindNudgeDispatched (Map [ "anchor", "t2\u001emsg body" ]) ]

    let s = foldNudgeSnapshot "s1" events
    equal "dispatched contains anchor 2" (Some "t2\u001emsg body") s.lastDispatchedAnchor

/// submit_review_wip_recorded clears lastDispatchedAnchor.
let foldNudgeSnapshotDedupCleared () =
    let events =
        [ ev "s1" eventKindNudgeDispatched (Map [ "anchor", "t1\u001emsg body" ])
          ev "s1" eventKindSubmitReviewWipRecorded Map.empty ]

    let s = foldNudgeSnapshot "s1" events
    check "dispatched anchors cleared" s.lastDispatchedAnchor.IsNone

/// work_backlog_committed updates openTodos.
let foldNudgeSnapshotWorkBacklogUpdatesTodos () =
    let events =
        [ ev "s1" eventKindWorkBacklogCommitted (Map [ "todosJson", "[\"a\",\"b\"]" ]) ]

    let s = foldNudgeSnapshot "s1" events
    equal "open todos from work backlog" [ "a"; "b" ] s.openTodos

/// Cold start (no events) → deriveAction returns NudgeNone.
let foldNudgeSnapshotColdStartNudgeNone () =
    let s = foldNudgeSnapshot "s1" []

    match deriveAction (toSessionSnapshot s) with
    | NudgeNone -> check "cold start -> NudgeNone" true
    | _ -> check "cold start -> NudgeNone" false

/// Sequence of events produces correct snapshot → deriveAction returns NudgeTodo.
let foldNudgeSnapshotIntegrated () =
    let events =
        [ ev "s1" eventKindWorkBacklogCommitted (Map [ "todosJson", "[\"ship feature\"]" ])
          ev
              "s1"
              eventKindAssistantCompleted
              (Map
                  [ "assistantMessage", "working on it"
                    "agent", "bookkeeper"
                    "model", "openai/gpt-4o"
                    "turnId", "t42" ]) ]

    let s = foldNudgeSnapshot "s1" events
    check "integrated: has todos" (s.openTodos = [ "ship feature" ])
    check "integrated: has assistant text" (s.lastAssistantText = "working on it")

    check "integrated: not loop active" (not (isLoopActive s.reviewLoop))
    check "integrated: has turnId" (s.turnId = "t42")
    equal "integrated: model" (Some "openai/gpt-4o") s.modelFromMessage

    match deriveAction (toSessionSnapshot s) with
    | NudgeTodo -> check "integrated -> NudgeTodo" true
    | _ -> check "integrated -> NudgeTodo" false

let run () =
    foldNudgeSnapshotEmpty ()
    foldNudgeSnapshotAssistantCompleted ()
    foldNudgeSnapshotLoopActivated ()
    foldNudgeSnapshotLoopCancelled ()
    foldNudgeSnapshotAcceptedClears ()
    foldNudgeSnapshotNeedsRevisionKeeps ()
    foldNudgeSnapshotNudgeDispatched ()
    foldNudgeSnapshotDedupCleared ()
    foldNudgeSnapshotWorkBacklogUpdatesTodos ()
    foldNudgeSnapshotColdStartNudgeNone ()
    foldNudgeSnapshotIntegrated ()
