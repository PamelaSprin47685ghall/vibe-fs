module Wanxiangshu.Tests.SessionOverviewTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.SessionOverview
open Wanxiangshu.Kernel.Review.ReviewLoopFold
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Kernel.FallbackKernel.Types

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

/// emptyOverview has all fields at default/empty values.
let emptyOverviewDefaults () =
    let o = emptyOverview
    check "review loop is initial" (o.ReviewLoop = initial)
    check "review task is None" (o.ReviewTask = None)
    check "backlog is empty" (o.Backlog = [])
    check "nudge dedup pending is None" (o.NudgeDedup.PendingNudge = None)
    check "nudge dedup last dispatched is None" (o.NudgeDedup.LastDispatchedAnchor = None)
    check "nudge snapshot todos empty" (o.NudgeSnapshot.openTodos = [])
    check "nudge snapshot text empty" (o.NudgeSnapshot.lastAssistantText = "")
    check "subagents empty" (Map.isEmpty o.Subagents)
    check "latest human turn None" (o.LatestHumanTurn = None)
    check "session generation 0" (o.SessionGeneration = 0)
    check "cancel generation 0" (o.CancelGeneration = 0)
    check "fallback lifecycle None" (o.FallbackLifecycle = None)
    check "fallback phase None" (o.FallbackPhase = None)
    check "session owner None" (o.SessionOwner = None)
    check "pending lease None" (o.PendingLease = None)
    check "pending nudge lease None" (o.PendingNudgeLease = None)
    check "active compaction None" (o.ActiveCompaction = None)
    check "human turn ordinal 0" (o.HumanTurnOrdinal = 0)
    check "event count 0" (o.EventCount = 0)

/// fromSessionState correctly maps all fields from a populated SessionState.
let fromSessionStatePopulated () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "refactor core" ])
          ev
              "s1"
              eventKindWorkBacklogCommitted
              (Map
                  [ "ahaMoments", "discovery"
                    "changesAndReasons", "refactor"
                    "gotchas", "gotcha"
                    "lessonsAndConventions", "lessons"
                    "plan", "next steps"
                    "todosJson", "[\"task1\"]" ]) ]

    let st = List.fold applyEvent (emptySessionState ()) events
    let overview = fromSessionState st

    check "review loop active" (isLoopActive overview.ReviewLoop)
    equal "review task" (Some "refactor core") overview.ReviewTask
    check "backlog non-empty" (not (List.isEmpty overview.Backlog))

/// fromSessionState maps NudgeDedupState correctly.
let fromSessionStateNudgeDedup () =
    let events =
        [ ev "s1" eventKindNudgeDispatched (Map [ "anchor", "t1\u001emsg body" ]) ]

    let st = List.fold applyEvent (emptySessionState ()) events
    let overview = fromSessionState st
    check "nudge dedup last dispatched anchor set" (overview.NudgeDedup.LastDispatchedAnchor = Some "t1\u001emsg body")

/// fromSessionState maps SubsessionState correctly.
let fromSessionStateSubagents () =
    let events =
        [ ev "s1" eventKindSubagentSpawned (Map [ "childId", "c1"; "agent", "coder"; "title", "Test" ]) ]

    let st = List.fold applyEvent (emptySessionState ()) events
    let overview = fromSessionState st
    check "subagent c1 exists" (Map.containsKey "c1" overview.Subagents)
    equal "subagent agent" "coder" (Map.find "c1" overview.Subagents).Agent

/// fromSessionState maps HumanTurnState correctly.
let fromSessionStateHumanTurn () =
    let events =
        [ ev
              "s1"
              eventKindHumanTurnStarted
              (Map
                  [ "turnId", "t1"
                    "provider", "openai"
                    "model", "gpt-4"
                    "agent", "bookkeeper"
                    "messageId", "m1"
                    "humanTurnOrdinal", "1" ]) ]

    let st = List.fold applyEvent (emptySessionState ()) events
    let overview = fromSessionState st
    check "latest human turn exists" (overview.LatestHumanTurn |> Option.isSome)

    let ht =
        overview.LatestHumanTurn
        |> Option.defaultValue
            { TurnId = ""
              Provider = ""
              Model = ""
              Variant = ""
              Agent = ""
              MessageId = None }

    equal "human turn id" "t1" ht.TurnId
    equal "human turn provider" "openai" ht.Provider
    equal "human turn ordinal" 1 overview.HumanTurnOrdinal

/// fromSessionState maps continuation episode state correctly.
let fromSessionStateContinuation () =
    let events =
        [ ev
              "s1"
              eventKindContinuationRequested
              (Map
                  [ "continuationId", "c1"
                    "model", "p/m"
                    "agent", "a"
                    "humanTurnId", "t1"
                    "continuationOrdinal", "1"
                    "generation", "1"
                    "cancelGeneration", "0" ]) ]

    let st = List.fold applyEvent (emptySessionState ()) events
    let overview = fromSessionState st
    check "session owner is Fallback" (overview.SessionOwner = Some "Fallback")
    check "pending lease exists" (overview.PendingLease |> Option.isSome)

/// fromSessionState maps fallback lifecycle correctly.
let fromSessionStateFallbackLifecycle () =
    let events = [ ev "s1" eventKindUserAbortObserved Map.empty ]
    let st = List.fold applyEvent (emptySessionState ()) events
    let overview = fromSessionState st
    check "fallback lifecycle is Cancelled" (overview.FallbackLifecycle = Some FallbackLifecycle.Cancelled)

/// fromSessionState handles empty event list (SessionState is empty).
let fromSessionStateEmpty () =
    let st = emptySessionState ()
    let overview = fromSessionState st
    check "empty review task" (overview.ReviewTask = None)
    check "empty backlog" (overview.Backlog = [])
    check "empty nudge dedup" (overview.NudgeDedup = emptyDedupState)
    check "empty nudge snapshot" (overview.NudgeSnapshot = emptySnapshotState)
    check "empty subagents" (Map.isEmpty overview.Subagents)
    check "empty event count" (overview.EventCount = 0)

let run () =
    emptyOverviewDefaults ()
    fromSessionStatePopulated ()
    fromSessionStateNudgeDedup ()
    fromSessionStateSubagents ()
    fromSessionStateHumanTurn ()
    fromSessionStateContinuation ()
    fromSessionStateFallbackLifecycle ()
    fromSessionStateEmpty ()
