module Wanxiangshu.Tests.NudgeEventSourcingTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.EventLog.Fold
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.Nudge.Types

let private ev session kind payload =
    { V = 1; Session = session; Kind = kind; At = ""; Payload = payload }

let private toSessionSnapshot (s: NudgeSnapshotState) : SessionSnapshot =
    { todos = s.openTodos
      lastAssistantMessage = s.lastAssistantText
      isLoopActive = s.isLoopActive
      nudgeBlockedForTurn = false
      nudgeAnchorKey = nudgeAnchorKey s.turnId s.lastAssistantText
      agentFromMessage = s.agentFromMessage
      modelFromMessage = None
      hasActiveRunner = false }

/// No events → all fields empty/default.
let foldNudgeSnapshotEmpty () =
    let s = foldNudgeSnapshot "s1" []
    check "empty: no todos" (s.openTodos = [])
    check "empty: no text" (s.lastAssistantText = "")
    check "empty: no agent" (s.agentFromMessage = None)
    check "empty: no turnId" (s.turnId = "")
    check "empty: not loop active" (not s.isLoopActive)
    check "empty: no dispatched anchors" (s.dispatchedAnchors = Set.empty)

/// assistant_completed populates lastAssistantText, agentFromMessage, turnId.
let foldNudgeSnapshotAssistantCompleted () =
    let events =
        [ ev "s1" eventKindAssistantCompleted (
              Map [ "assistantMessage", "implemented feature"
                    "agent", "bookkeeper"
                    "turnId", "t1" ]
          ) ]
    let s = foldNudgeSnapshot "s1" events
    equal "assistant text" "implemented feature" s.lastAssistantText
    equal "agent from message" (Some "bookkeeper") s.agentFromMessage
    equal "turnId" "t1" s.turnId

/// loop_activated sets isLoopActive=true.
let foldNudgeSnapshotLoopActivated () =
    let events = [ ev "s1" eventKindLoopActivated (Map [ "task", "ship it" ]) ]
    let s = foldNudgeSnapshot "s1" events
    check "loop active after activate" s.isLoopActive
    check "no todos" (s.openTodos = [])
    check "no text" (s.lastAssistantText = "")
    check "no agent" (s.agentFromMessage = None)
    check "no dispatched anchors" (s.dispatchedAnchors = Set.empty)

/// loop_cancelled clears isLoopActive.
let foldNudgeSnapshotLoopCancelled () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship it" ])
          ev "s1" eventKindLoopCancelled Map.empty ]
    let s = foldNudgeSnapshot "s1" events
    check "not loop active after cancel" (not s.isLoopActive)
    check "no todos" (s.openTodos = [])
    check "no text" (s.lastAssistantText = "")
    check "no agent" (s.agentFromMessage = None)
    check "no dispatched anchors" (s.dispatchedAnchors = Set.empty)

/// review_verdict accepted clears isLoopActive.
let foldNudgeSnapshotAcceptedClears () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship it" ])
          ev "s1" eventKindReviewVerdict (Map [ "verdict", verdictAccepted ]) ]
    let s = foldNudgeSnapshot "s1" events
    check "not loop active after accept" (not s.isLoopActive)

/// review_verdict needs_revision keeps isLoopActive.
let foldNudgeSnapshotNeedsRevisionKeeps () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship it" ])
          ev "s1" eventKindReviewVerdict (Map [ "verdict", verdictNeedsRevision ]) ]
    let s = foldNudgeSnapshot "s1" events
    check "loop active after needs_revision" s.isLoopActive

/// nudge_dispatched adds both anchors to dispatchedAnchors.
let foldNudgeSnapshotNudgeDispatched () =
    let events =
        [ ev "s1" eventKindNudgeDispatched (Map [ "anchor", "t1\u001emsg body" ])
          ev "s1" eventKindNudgeDispatched (Map [ "anchor", "t2\u001emsg body" ]) ]
    let s = foldNudgeSnapshot "s1" events
    check "dispatched contains anchor 1" (s.dispatchedAnchors.Contains "t1\u001emsg body")
    check "dispatched contains anchor 2" (s.dispatchedAnchors.Contains "t2\u001emsg body")
    check "dispatched size = 2" (s.dispatchedAnchors.Count = 2)

/// submit_review_wip_recorded clears dispatchedAnchors.
let foldNudgeSnapshotDedupCleared () =
    let events =
        [ ev "s1" eventKindNudgeDispatched (Map [ "anchor", "t1\u001emsg body" ])
          ev "s1" eventKindSubmitReviewWipRecorded Map.empty ]
    let s = foldNudgeSnapshot "s1" events
    check "dispatched anchors cleared" (s.dispatchedAnchors = Set.empty)

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
          ev "s1" eventKindAssistantCompleted (
              Map [ "assistantMessage", "working on it"
                    "agent", "bookkeeper"
                    "turnId", "t42" ]
          ) ]
    let s = foldNudgeSnapshot "s1" events
    check "integrated: has todos" (s.openTodos = [ "ship feature" ])
    check "integrated: has assistant text" (s.lastAssistantText = "working on it")
    check "integrated: not loop active" (not s.isLoopActive)
    check "integrated: has turnId" (s.turnId = "t42")
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
