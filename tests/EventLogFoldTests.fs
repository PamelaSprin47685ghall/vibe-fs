module Wanxiangshu.Tests.EventLogFoldTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.Review
open Wanxiangshu.Kernel.Backlog
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Subsession
open Wanxiangshu.Kernel.Review.ReviewProjection
open Wanxiangshu.Kernel.Backlog.BacklogProjection
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Kernel.Subsession.SubsessionProjection
open Wanxiangshu.Runtime.BacklogProjectionBuild

let private foldEventStream
    (sessionId: string)
    (zero: 'State)
    (folder: 'State -> WanEvent -> 'State)
    (events: WanEvent list)
    =
    events |> List.filter (fun e -> e.Session = sessionId) |> List.fold folder zero

let private ev session kind payload =
    { V = 1
      Session = session
      Kind = kind
      At = ""
      Payload = payload }

let foldReviewTaskEmpty () =
    equal "no events" None (ReviewProjection.foldReviewTask "s1" [])

let foldReviewTaskActivate () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship S1" ])
          ev "s2" eventKindLoopActivated (Map [ "task", "other" ]) ]

    equal "activate" (Some "ship S1") (ReviewProjection.foldReviewTask "s1" events)

let foldReviewTaskAcceptClears () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship S1" ])
          ev "s1" eventKindReviewVerdict (Map [ "verdict", verdictAccepted ]) ]

    equal "accept clears" None (ReviewProjection.foldReviewTask "s1" events)

let foldReviewTaskNeedsRevisionStays () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship S1" ])
          ev "s1" eventKindReviewVerdict (Map [ "verdict", verdictNeedsRevision ]) ]

    equal "needs_revision keeps" (Some "ship S1") (ReviewProjection.foldReviewTask "s1" events)

let foldReviewTaskLastActivateWins () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "S1" ])
          ev "s1" eventKindLoopActivated (Map [ "task", "S2" ]) ]

    equal "last task" (Some "S2") (ReviewProjection.foldReviewTask "s1" events)

let foldReviewTaskCancelClears () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship S1" ])
          ev "s1" eventKindLoopCancelled Map.empty ]

    equal "cancel clears" None (ReviewProjection.foldReviewTask "s1" events)

let foldReviewTaskTerminatedKeeps () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship S1" ])
          ev "s1" eventKindReviewVerdict (Map [ "verdict", verdictTerminated ]) ]

    equal "terminated keeps" (Some "ship S1") (ReviewProjection.foldReviewTask "s1" events)

let foldWorkBacklogLatestEntry () =
    let full a p =
        Map
            [ "ahaMoments", a
              "changesAndReasons", "c"
              "gotchas", "g"
              "lessonsAndConventions", "l"
              "plan", p ]

    let events =
        [ ev "s1" eventKindWorkBacklogCommitted (full "a1" "p1")
          ev "s1" eventKindWorkBacklogCommitted (full "a2" "p2") ]

    let snap = BacklogProjection.foldBacklogStream "s1" events
    check "latest aha" (snap.LatestEntry |> Option.map (fun (e: BacklogEntry) -> e.ahaMoments) = Some "a2")
    check "latest plan" (snap.LatestEntry |> Option.map (fun (e: BacklogEntry) -> e.plan) = Some "p2")

let foldWorkBacklogTodosJson () =
    let payload todos =
        Map
            [ "ahaMoments", "a"
              "changesAndReasons", "c"
              "gotchas", "g"
              "lessonsAndConventions", "l"
              "plan", "p"
              "todosJson", todos ]

    let events =
        [ ev "s1" eventKindWorkBacklogCommitted (payload "[1]")
          ev "s1" eventKindWorkBacklogCommitted (Map [ "ahaMoments", "only" ]) ]

    let snap = BacklogProjection.foldBacklogStream "s1" events
    check "todosJson kept when partial commit" (snap.TodosJson = Some "[1]")
    check "partial commit keeps prior latest entry" (snap.LatestEntry |> Option.map (fun e -> e.ahaMoments) = Some "a")

let foldNudgeDedupAnchor () =
    let events =
        [ ev "s1" eventKindNudgeDispatched (Map [ "action", "nudge-todo"; "anchor", "1\u001emsg" ])
          ev "s1" eventKindNudgeDispatched (Map [ "action", "nudge-todo"; "anchor", "2\u001emsg" ]) ]

    let st = NudgeProjection.foldDedupStream "s1" events
    check "blocks anchor 1" (not (NudgeProjection.isBlocked st "1\u001emsg"))
    check "blocks anchor 2" (NudgeProjection.isBlocked st "2\u001emsg")
    check "other open" (not (NudgeProjection.isBlocked st "3\u001emsg"))

let foldEventStreamFiltersOtherSessions () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "s1-task" ])
          ev "s2" eventKindLoopActivated (Map [ "task", "s2-task" ])
          ev "s1" eventKindLoopActivated (Map [ "task", "s1-task-2" ]) ]

    let result =
        foldEventStream "s1" 0 (fun count e -> if e.Kind = eventKindLoopActivated then count + 1 else count) events

    equal "only s1 events counted" 2 result

let foldSubagentsTest () =
    let events =
        [ ev "s1" eventKindSubagentSpawned (Map [ "childId", "c1"; "agent", "coder"; "title", "Test Coder" ])
          ev "s1" eventKindSubagentContinued (Map [ "childId", "c1"; "prompt", "Hello" ])
          ev "s1" eventKindSubagentContinued (Map [ "childId", "c1"; "prompt", "World" ]) ]

    let result = SubsessionProjection.foldSubagents "s1" events
    check "subagent exists in map" (Map.containsKey "c1" result)
    let state = Map.find "c1" result
    equal "childId matches" "c1" state.ChildId
    equal "agent matches" "coder" state.Agent
    equal "title matches" "Test Coder" state.Title
    equal "continued prompts length" 2 state.ContinuedPrompts.Length
    equal "latest continued prompt" "World" (List.head state.ContinuedPrompts)

let foldSessionState (events: WanEvent list) : SessionState =
    List.fold applyEvent (emptySessionState ()) events

let ordinalRejectsLateNudgeDispatched () =
    let events =
        [ ev "s1" eventKindHumanTurnStarted (Map [ "turnId", "t1"; "messageId", "m1"; "humanTurnOrdinal", "1" ])
          ev
              "s1"
              eventKindNudgeRequested
              (Map [ "nudgeId", "n1"; "action", "todo"; "anchor", "a"; "nudgeOrdinal", "1" ])
          ev "s1" eventKindNudgeSettled (Map [ "nudgeId", "n1"; "status", "completed"; "nudgeOrdinal", "1" ])
          // Late nudge_dispatched for n1 after n2 has started should be ignored
          ev
              "s1"
              eventKindNudgeDispatched
              (Map [ "nudgeId", "n1"; "action", "todo"; "anchor", "a"; "nudgeOrdinal", "1" ]) ]

    let st = foldSessionState events
    check "nudge stage stays terminal" (st.NudgeStage = Terminal)
    check "owner not reset to Nudge" (st.SessionOwner <> Some "Nudge")

let ordinalRejectsLateContinuationDispatched () =
    let events =
        [ ev "s1" eventKindHumanTurnStarted (Map [ "turnId", "t1"; "messageId", "m1"; "humanTurnOrdinal", "1" ])
          ev
              "s1"
              eventKindContinuationRequested
              (Map
                  [ "continuationId", "c1"
                    "model", "p/m"
                    "agent", "a"
                    "humanTurnId", "t1"
                    "continuationOrdinal", "1" ])
          ev
              "s1"
              eventKindContinuationSettled
              (Map
                  [ "continuationId", "c1"
                    "status", "completed"
                    "humanTurnId", "t1"
                    "generation", "1"
                    "continuationOrdinal", "1" ])
          // Late continuation_dispatched for c1 after terminal should be ignored
          ev
              "s1"
              eventKindContinuationDispatched
              (Map
                  [ "continuationId", "c1"
                    "model", "p/m"
                    "agent", "a"
                    "continuationOrdinal", "1" ]) ]

    let st = foldSessionState events
    check "continuation stage stays terminal" (st.ContinuationStage = Terminal)
    check "owner not reset to Fallback" (st.SessionOwner <> Some "Fallback")

let duplicateHumanTurnMessageIdIgnored () =
    let events =
        [ ev "s1" eventKindHumanTurnStarted (Map [ "turnId", "t1"; "messageId", "m1"; "humanTurnOrdinal", "1" ])
          ev "s1" eventKindHumanTurnStarted (Map [ "turnId", "t2"; "messageId", "m1"; "humanTurnOrdinal", "2" ]) ]

    let st = foldSessionState events
    equal "duplicate message id ignored; only one human turn" 1 st.HumanTurnOrdinal
    check "latest turn id is still t1" (st.LatestHumanTurn |> Option.exists (fun t -> t.TurnId = "t1"))

let run () =
    foldEventStreamFiltersOtherSessions ()
    foldSubagentsTest ()
    foldNudgeDedupAnchor ()
    foldReviewTaskEmpty ()
    foldReviewTaskActivate ()
    foldReviewTaskAcceptClears ()
    foldReviewTaskNeedsRevisionStays ()
    foldReviewTaskLastActivateWins ()
    foldReviewTaskCancelClears ()
    foldReviewTaskTerminatedKeeps ()
    foldWorkBacklogLatestEntry ()
    foldWorkBacklogTodosJson ()
    ordinalRejectsLateNudgeDispatched ()
    ordinalRejectsLateContinuationDispatched ()
    duplicateHumanTurnMessageIdIgnored ()
