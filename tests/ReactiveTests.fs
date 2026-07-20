module Wanxiangshu.Tests.ReactiveTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Reactive
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.FallbackKernel.Types

// Convenience helpers
let private rid = RunId.create "run1"
let private tid1 = TurnId.create "turn1"
let private tid2 = TurnId.create "turn2"
let private sid = SessionId.create "sess1"

let private makeTurnData (runId: RunId) (turnId: TurnId) (prompt: string) : TurnData =
    { RunId = runId
      TurnId = turnId
      Ordinal = TurnOrdinal.first
      Model =
        { ProviderID = "p"
          ModelID = "m"
          Variant = None
          Temperature = None
          TopP = None
          MaxTokens = None
          ReasoningEffort = None
          Thinking = false }
      Prompt = prompt
      DeadlineAtMs = 0L }

let private makeStartedData (runId: RunId) (turnId: TurnId) (receipt: HostStartReceipt) : TurnStartedData =
    { RunId = runId
      TurnId = turnId
      Receipt = receipt }

let private makeRunStartedData (runId: RunId) (sessionId: SessionId) : RunStartedData =
    { RunId = runId
      ParentSessionId = sessionId
      SessionId = sessionId }

/// CommittedProgress.fromEvents maps all relevant SubsessionEvent variants correctly.
let fromEventsMapsAllVariants () =
    let events: SubsessionEvent list =
        [ RunStarted(makeRunStartedData rid sid)
          TurnDispatchRequested(makeTurnData rid tid1 "do work")
          TurnStarted(makeStartedData rid tid1 (UserMessageObserved "m1"))
          TurnFinished(tid1, TurnCompleted "done")
          RunFinished(rid, RunResult.Succeeded "done")
          SessionPoisoned(sid, PoisonReason.HostProtocolBroken "test")
          AbortRequested(rid, tid1, 0L)
          PhysicalSessionClosed sid ]

    let progress = CommittedProgress.fromEvents events
    equal "7 events + 1 RunStarted filtered → 7 progress items" 7 progress.Length

    check
        "turn dispatched"
        (progress
         |> List.exists (function
             | ProgressTurnDispatched(r, t, _) -> r = rid && t = tid1
             | _ -> false))

    check
        "turn accepted"
        (progress
         |> List.exists (function
             | ProgressTurnAccepted(t, _) -> t = tid1
             | _ -> false))

    check
        "turn finished"
        (progress
         |> List.exists (function
             | ProgressTurnFinished(t, _) -> t = tid1
             | _ -> false))

    check
        "run finished"
        (progress
         |> List.exists (function
             | ProgressRunFinished(r, _) -> r = rid
             | _ -> false))

    check
        "session poisoned"
        (progress
         |> List.exists (function
             | ProgressSessionPoisoned(s, _) -> s = sid
             | _ -> false))

    check
        "abort requested"
        (progress
         |> List.exists (function
             | ProgressAbortRequested(r, t) -> r = rid && t = tid1
             | _ -> false))

    check
        "physical session closed"
        (progress
         |> List.exists (function
             | ProgressPhysicalSessionClosed s -> s = sid
             | _ -> false))

/// RunStarted is filtered out — no progress event for it.
let fromEventsFiltersRunStarted () =
    let events = [ RunStarted(makeRunStartedData rid sid) ]
    let progress = CommittedProgress.fromEvents events
    check "RunStart produces no progress" (List.isEmpty progress)

/// Empty event list → empty progress list.
let fromEventsEmpty () =
    let progress = CommittedProgress.fromEvents []
    check "no events → no progress" (List.isEmpty progress)

/// Order of progress matches order of input events.
let fromEventsPreservesOrder () =
    let events: SubsessionEvent list =
        [ TurnDispatchRequested(makeTurnData rid tid1 "first")
          TurnStarted(makeStartedData rid tid1 (UserMessageObserved "m1"))
          TurnFinished(tid1, TurnCompleted "done")
          TurnDispatchRequested(makeTurnData rid tid2 "second")
          TurnStarted(makeStartedData rid tid2 (UserMessageObserved "m2"))
          TurnFinished(tid2, TurnCompleted "done") ]

    let progress = CommittedProgress.fromEvents events
    equal "6 events → 6 progress items" 6 progress.Length

    match progress with
    | ProgressTurnDispatched(_, t1, _) :: ProgressTurnAccepted(t2, _) :: ProgressTurnFinished(t3, _) :: ProgressTurnDispatched(_,
                                                                                                                               t4,
                                                                                                                               _) :: ProgressTurnAccepted(t5,
                                                                                                                                                          _) :: ProgressTurnFinished(t6,
                                                                                                                                                                                     _) :: [] ->
        equal "first turn dispatched" tid1 t1
        equal "first turn accepted" tid1 t2
        equal "first turn finished" tid1 t3
        equal "second turn dispatched" tid2 t4
        equal "second turn accepted" tid2 t5
        equal "second turn finished" tid2 t6
    | _ -> check "expected 6-element list with correct order" false

/// NullReactivePort does nothing on OnCommitted.
let nullReactivePortOnCommitted () =
    let port = NullReactivePort() :> IReactivePort
    port.OnCommitted([])
    port.OnCommitted([ ProgressTurnDispatched(rid, tid1, "hello") ])
    check "no-op OnCommitted doesn't throw" true

/// NullReactivePort does nothing on OnTelemetry.
let nullReactivePortOnTelemetry () =
    let port = NullReactivePort() :> IReactivePort
    port.OnTelemetry([])
    port.OnTelemetry([ TelemetryHostDispatchStart tid1 ])
    check "no-op OnTelemetry doesn't throw" true

let run () =
    fromEventsMapsAllVariants ()
    fromEventsFiltersRunStarted ()
    fromEventsEmpty ()
    fromEventsPreservesOrder ()
    nullReactivePortOnCommitted ()
    nullReactivePortOnTelemetry ()
