module Wanxiangshu.Tests.EventLogFoldTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.EventLog.Fold
open Wanxiangshu.Kernel.BacklogProjectionCore

let private ev session kind payload =
    { V = 1
      Session = session
      Kind = kind
      At = ""
      Payload = payload }

let foldReviewTaskEmpty () =
    equal "no events" None (foldReviewTask "s1" [])

let foldReviewTaskActivate () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship S1" ])
          ev "s2" eventKindLoopActivated (Map [ "task", "other" ]) ]

    equal "activate" (Some "ship S1") (foldReviewTask "s1" events)

let foldReviewTaskAcceptClears () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship S1" ])
          ev "s1" eventKindReviewVerdict (Map [ "verdict", verdictAccepted ]) ]

    equal "accept clears" None (foldReviewTask "s1" events)

let foldReviewTaskNeedsRevisionStays () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship S1" ])
          ev "s1" eventKindReviewVerdict (Map [ "verdict", verdictNeedsRevision ]) ]

    equal "needs_revision keeps" (Some "ship S1") (foldReviewTask "s1" events)

let foldReviewTaskLastActivateWins () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "S1" ])
          ev "s1" eventKindLoopActivated (Map [ "task", "S2" ]) ]

    equal "last task" (Some "S2") (foldReviewTask "s1" events)

let foldReviewTaskCancelClears () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship S1" ])
          ev "s1" eventKindLoopCancelled Map.empty ]

    equal "cancel clears" None (foldReviewTask "s1" events)

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

    let snap = foldWorkBacklogSnapshot "s1" events
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

    let snap = foldWorkBacklogSnapshot "s1" events
    check "todosJson kept when partial commit" (snap.TodosJson = Some "[1]")
    check "partial commit keeps prior latest entry" (snap.LatestEntry |> Option.map (fun e -> e.ahaMoments) = Some "a")

let foldNudgeDedupAnchor () =
    let events =
        [ ev "s1" eventKindNudgeDispatched (Map [ "action", "nudge-todo"; "anchor", "1\u001emsg" ])
          ev "s1" eventKindNudgeDispatched (Map [ "action", "nudge-todo"; "anchor", "2\u001emsg" ]) ]

    let st = foldNudgeDedup "s1" events
    check "blocks anchor 1" (isNudgeBlockedForAnchor st "1\u001emsg")
    check "blocks anchor 2" (isNudgeBlockedForAnchor st "2\u001emsg")
    check "other open" (not (isNudgeBlockedForAnchor st "3\u001emsg"))

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

    let result = foldSubagents "s1" events
    check "subagent exists in map" (Map.containsKey "c1" result)
    let state = Map.find "c1" result
    equal "childId matches" "c1" state.ChildId
    equal "agent matches" "coder" state.Agent
    equal "title matches" "Test Coder" state.Title
    equal "continued prompts length" 2 state.ContinuedPrompts.Length
    equal "latest continued prompt" "World" (List.head state.ContinuedPrompts)

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
    foldWorkBacklogLatestEntry ()
    foldWorkBacklogTodosJson ()
