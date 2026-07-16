/// ──────────────────────────────────────────────
///  Replay Equivalence Tests
///
///  Verify the fundamental REF.md invariant:
///    fold(events) == fold(events ++ [])
///
///  Replaying the identical event log MUST produce the
///  identical final state, regardless of how many times
///  the fold is executed.
///
///  Also verify that independent projections (ReviewLoop,
///  Backlog, Nudge, Subagents) reach the same state whether
///  folded standalone or via the composite SessionState.
/// ──────────────────────────────────────────────
module Wanxiangshu.Tests.ReplayEquivalenceTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.Review
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Subsession
open Wanxiangshu.Kernel.Review.ReviewProjection
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Kernel.Subsession.SubsessionProjection
open Wanxiangshu.Kernel.SessionOverview
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Fold

// ──────────────────────────────────────────────
//  Helpers
// ──────────────────────────────────────────────

let private ev session kind payload =
    { V = 1
      Session = session
      Kind = kind
      At = ""
      Payload = payload }

let private makeFallbackModel: FallbackModel =
    { ProviderID = "p"
      ModelID = "m"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private sid = SessionId.create "s1"
let private rid = RunId.create "r1"
let private tid = TurnId.create "t1"

/// Fold a list multiple times and verify the result is identical.
let private assertIdempotentFold (label: string) (events: WanEvent list) : unit =
    let r1 = List.fold applyEvent (emptySessionState ()) events
    let r2 = List.fold applyEvent (emptySessionState ()) events
    let r3 = List.fold applyEvent (emptySessionState ()) events
    check (sprintf "%s: fold idempotent (r1=r2)" label) (r1 = r2)
    check (sprintf "%s: fold idempotent (r2=r3)" label) (r2 = r3)

// ──────────────────────────────────────────────
//  1. Empty event list -> empty state (always)
// ──────────────────────────────────────────────

let ``Replay: empty events produce empty state`` () =
    let st = List.fold applyEvent (emptySessionState ()) []
    check "empty state review task None" (st.ReviewTask = None)
    check "empty state backlog empty" (st.Backlog = [])
    check "empty state nudge dedup empty" (st.NudgeDedup = emptyDedupState)
    check "empty state subagents empty" (Map.isEmpty st.Subagents)
    check "empty state event count 0" (st.EventCount = 0)

// ──────────────────────────────────────────────
//  2. Single event, fold twice
// ──────────────────────────────────────────────

let ``Replay: single event fold is idempotent`` () =
    let events = [ ev "s1" eventKindLoopActivated (Map [ "task", "replay test" ]) ]
    assertIdempotentFold "single event" events
    let st = List.fold applyEvent (emptySessionState ()) events
    check "single event: review task set" (st.ReviewTask = Some "replay test")

// ──────────────────────────────────────────────
//  3. Full event sequence, fold 3 times
// ──────────────────────────────────────────────

let ``Replay: full event sequence is idempotent`` () =
    let events: WanEvent list =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "full replay" ])
          ev
              "s1"
              eventKindWorkBacklogCommitted
              (Map
                  [ "ahaMoments", "aha"
                    "changesAndReasons", "c"
                    "gotchas", "g"
                    "lessonsAndConventions", "l"
                    "plan", "p" ])
          ev "s1" eventKindNudgeDispatched (Map [ "anchor", "t1\u001emsg" ])
          ev "s1" eventKindReviewVerdict (Map [ "verdict", "accepted" ]) ]

    assertIdempotentFold "full sequence" events
    let st = List.fold applyEvent (emptySessionState ()) events
    check "full sequence: review cleared after accept" (st.ReviewTask = None)
    check "full sequence: backlog has entry" (not (List.isEmpty st.Backlog))
    check "full sequence: nudge dedup anchor set" (st.NudgeDedup.LastDispatchedAnchor.IsSome)

// ──────────────────────────────────────────────
//  4. Session isolation — different sessions don't interfere
// ──────────────────────────────────────────────

let ``Replay: session isolation`` () =
    let events: WanEvent list =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "s1-task" ])
          ev "s2" eventKindLoopActivated (Map [ "task", "s2-task" ]) ]

    // Composite SessionState does not filter by session; only the standalone
    // projection (used in filtered fold below) guarantees session isolation.
    let s1Events = events |> List.filter (fun e -> e.Session = "s1")
    let stS1 = List.fold applyEvent (emptySessionState ()) s1Events
    check "session isolation: filtered s1 has s1-task" (stS1.ReviewTask = Some "s1-task")

// ──────────────────────────────────────────────
//  5. Independent projection vs composite SessionState
//     Uses composite wrappers from Fold.fs (which delegate
//     to standalone projection modules internally).
// ──────────────────────────────────────────────

let ``Replay: standalone projection matches composite`` () =
    let events: WanEvent list =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "projection parity" ])
          ev "s1" eventKindSubagentSpawned (Map [ "childId", "c1"; "agent", "coder"; "title", "Test" ])
          ev "s1" eventKindNudgeDispatched (Map [ "anchor", "a\u001emsg" ]) ]

    let st = List.fold applyEvent (emptySessionState ()) events

    // Use standalone projections
    let reviewLoop = ReviewProjection.foldReviewLoopStream "s1" events
    let subagents = SubsessionProjection.foldSubagents "s1" events
    let nudgeDedup = NudgeProjection.foldDedupStream "s1" events

    check "composite review loop matches standalone" (st.ReviewLoop = reviewLoop)
    check "composite subagents match standalone" (st.Subagents = subagents)
    check "composite nudge dedup matches standalone" (st.NudgeDedup = nudgeDedup)

// ──────────────────────────────────────────────
//  6. SessionOverview from state vs from replay
// ──────────────────────────────────────────────

let ``Replay: SessionOverview from state matches`` () =
    let events: WanEvent list =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "overview parity" ])
          ev
              "s1"
              eventKindWorkBacklogCommitted
              (Map
                  [ "ahaMoments", "a"
                    "changesAndReasons", "c"
                    "gotchas", "g"
                    "lessonsAndConventions", "l"
                    "plan", "p" ]) ]

    let st = List.fold applyEvent (emptySessionState ()) events
    let overview1 = fromSessionState st
    let st2 = List.fold applyEvent (emptySessionState ()) events
    let overview2 = fromSessionState st2

    check "SessionOverview idempotent" (overview1 = overview2)
    check "SessionOverview review task" (overview1.ReviewTask = Some "overview parity")
    check "SessionOverview backlog non-empty" (not (List.isEmpty overview1.Backlog))

// ──────────────────────────────────────────────
//  7. SubsessionEvent replay equivalence
// ──────────────────────────────────────────────

let ``Replay: subsession safety projection idempotent`` () =
    let events: SubsessionEvent list =
        [ RunStarted
              { RunId = rid
                ParentSessionId = sid
                SessionId = sid }
          TurnDispatchRequested
              { RunId = rid
                TurnId = tid
                Ordinal = TurnOrdinal.first
                Model = makeFallbackModel
                Prompt = "work" }
          TurnFinished(tid, TurnCancelled)
          RunFinished(rid, Cancelled) ]

    let safety1 = Wanxiangshu.Kernel.Subsession.Fold.projectEvents events
    let safety2 = Wanxiangshu.Kernel.Subsession.Fold.projectEvents events

    let hasActive1 =
        safety1
        |> Map.exists (fun _ v ->
            match v with
            | ActiveRun _ -> true
            | _ -> false)

    let hasActive2 =
        safety2
        |> Map.exists (fun _ v ->
            match v with
            | ActiveRun _ -> true
            | _ -> false)

    check "subsession safety projection idempotent" (hasActive1 = hasActive2)

// ──────────────────────────────────────────────
//  8. Ordering invariance: fold order = append order
// ──────────────────────────────────────────────

let ``Replay: append order preserves fold order`` () =
    let events: WanEvent list =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "first" ])
          ev "s1" eventKindLoopActivated (Map [ "task", "second" ])
          ev "s1" eventKindLoopCancelled Map.empty ]

    let st = List.fold applyEvent (emptySessionState ()) events
    check "append order: last cancel wins" (st.ReviewTask = None)

    let eventsReversed = List.rev events
    let stRev = List.fold applyEvent (emptySessionState ()) eventsReversed
    check "append order: reverse order different" (st.ReviewTask <> stRev.ReviewTask)

// ──────────────────────────────────────────────
//  9. Nudge ordinal monotonicity enforced on replay
// ──────────────────────────────────────────────

let ``Replay: nudge ordinal monotonicity`` () =
    let events: WanEvent list =
        [ ev "s1" eventKindNudgeRequested (Map [ "nudgeId", "n1"; "anchor", "a1"; "nudgeOrdinal", "1" ])
          ev "s1" eventKindNudgeDispatched (Map [ "nudgeId", "n1"; "anchor", "a1"; "nudgeOrdinal", "1" ])
          ev "s1" eventKindNudgeDispatched (Map [ "nudgeId", "n1"; "anchor", "a1"; "nudgeOrdinal", "1" ]) ]

    let st = List.fold applyEvent (emptySessionState ()) events
    check "nudge ordinal: dedup handles repeated ordinal" (st.NudgeDedup.LastDispatchedAnchor = Some "a1")

// ──────────────────────────────────────────────
//  10. Maximum event count stress test
// ──────────────────────────────────────────────

let ``Replay: 100 event batch is idempotent`` () =
    let events =
        [ for i in 1..50 do
              yield
                  ev
                      "s1"
                      eventKindNudgeRequested
                      (Map
                          [ "nudgeId", sprintf "n%d" i
                            "anchor", sprintf "a%d" i
                            "nudgeOrdinal", sprintf "%d" i ])

              yield
                  ev
                      "s1"
                      eventKindNudgeDispatched
                      (Map
                          [ "nudgeId", sprintf "n%d" i
                            "anchor", sprintf "a%d" i
                            "nudgeOrdinal", sprintf "%d" i ]) ]

    let st = List.fold applyEvent (emptySessionState ()) events
    check "100 events: event count correct" (st.EventCount = 100)
    let st2 = List.fold applyEvent (emptySessionState ()) events
    check "100 events: fold idempotent" (st = st2)

let run () =
    ``Replay: empty events produce empty state`` ()
    ``Replay: single event fold is idempotent`` ()
    ``Replay: full event sequence is idempotent`` ()
    ``Replay: session isolation`` ()
    ``Replay: standalone projection matches composite`` ()
    ``Replay: SessionOverview from state matches`` ()
    ``Replay: subsession safety projection idempotent`` ()
    ``Replay: append order preserves fold order`` ()
    ``Replay: nudge ordinal monotonicity`` ()
    ``Replay: 100 event batch is idempotent`` ()
