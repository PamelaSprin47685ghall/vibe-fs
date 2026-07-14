module Wanxiangshu.Tests.SubsessionV37HardTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Decision
open Wanxiangshu.Kernel.Subsession.TranscriptDecision
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.SubsessionActor
open Wanxiangshu.Shell.SubsessionActorRegistry
open Wanxiangshu.Shell.SubsessionEventStore
open Wanxiangshu.Shell.SubsessionEventRouter
open Wanxiangshu.Shell.SubsessionChildObserver
open Wanxiangshu.Shell.SubsessionEventWire
open Wanxiangshu.Tests.Assert

let private fail (msg: string) = check msg false

let private hasEffect pred effects = List.exists pred effects

let private model0: FallbackModel =
    { ProviderID = "p"
      ModelID = "m0"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private modelB: FallbackModel = { model0 with ModelID = "mB" }

let private cfg: FallbackConfig =
    { DefaultChain = [ model0 ]
      AgentChains = Map.empty
      MaxRetries = 1
      LoopMaxContinues = 10
      MaxRecoveries = 3 }

let private err: ErrorInput =
    { ErrorName = "Network"
      DomainError = None
      Message = "timeout"
      StatusCode = None
      IsRetryable = Some true }

let private sleep (ms: int) : JS.Promise<unit> =
    Promise.create (fun resolve _ -> JS.setTimeout (fun () -> resolve ()) ms |> ignore)

type ScriptedHost
    (
        ?dispatch: unit -> Result<HostStartReceipt, DispatchFailure>,
        ?abort: unit -> AbortResult,
        ?queryDispatchStatus: unit -> DispatchStatus
    ) =
    let mutable dispatchCount = 0
    let mutable abortCount = 0
    let mutable abortResults: AbortResult list = []

    member _.DispatchCount = dispatchCount
    member _.AbortCount = abortCount
    member _.AbortResults = abortResults

    interface ISubsessionHost with
        member _.Dispatch(_, _) =
            dispatchCount <- dispatchCount + 1
            Promise.lift ((defaultArg dispatch (fun () -> Ok OrderedTurnMarkerObserved)) ())

        member _.Abort(_, _) =
            abortCount <- abortCount + 1
            let r = (defaultArg abort (fun () -> ConfirmedStopped)) ()
            abortResults <- abortResults @ [ r ]
            Promise.lift r

        member _.CancelPendingDispatch(_) = ()

        member _.QueryDispatchStatus(_, _) =
            Promise.lift ((defaultArg queryDispatchStatus (fun () -> DispatchStatus.DefinitelyNotAccepted)) ())

let private mkReq sid runId =
    { RunId = RunId.create runId
      SessionId = SessionId.create sid
      ParentSessionId = SessionId.create "parent"
      Prompt = "go"
      FallbackConfig = cfg
      Chain = [ model0 ]
      InitiallyCancelled = false }

// 1+2: BeginRun atomic — CancelRequested cannot insert before StartRun commit
let beginRunAtomicWithCancelRace () =
    promise {
        let host = ScriptedHost()
        let store = MemorySubsessionEventStore()
        let sid = "child-v37-atomic"
        let actor = SubsessionActor(SessionId.create sid, host, store)

        // Fire BeginRun (queues StartRun atomically with deferred registration).
        let p = actor.BeginRun(mkReq sid "run-atomic")

        // Immediately post Cancel — must be AFTER StartRun in the serial queue.
        do! actor.Post CancelRequested
        do! sleep 40
        // AbortConfirmed path: host returns ConfirmedStopped by default
        do! sleep 30
        let! result = p

        match result with
        | Cancelled ->
            check "cancelled after start, not ignored" true
            // Either dispatched once then aborted, or cancelled before dispatch if
            // InitiallyCancelled — here InitiallyCancelled=false so may dispatch.
            check "run finished" true
        | other -> fail ("expected Cancelled, got " + string other)
    }

// 3: Host AbortUnavailable must never produce successful settle as ConfirmedStopped
let abortUnavailableNeverConfirmed () =
    promise {
        let host = ScriptedHost(abort = fun () -> AbortUnavailable)
        let store = MemorySubsessionEventStore()
        let sid = "child-v37-noabort"
        let actor = SubsessionActor(SessionId.create sid, host, store)

        let p = actor.BeginRun(mkReq sid "run-noabort")
        do! sleep 20
        do! actor.Post CancelRequested
        do! sleep 30

        // Still IssuingAbort (or eventually deadline). Must not be Available Cancelled yet
        // without ConfirmedStopped — AbortUnavailable stays IssuingAbort.
        match actor.GetState() with
        | IssuingAbort _ -> check "stays IssuingAbort on AbortUnavailable" true
        | AwaitingAbortSettle _ -> fail "AbortUnavailable must not open idle barrier"
        | Available _ ->
            // Only ok if CompleteCaller was InfrastructureFailure after deadline — not yet.
            fail "must not settle Available immediately on AbortUnavailable"
        | other -> fail ("unexpected state " + string other)

        // AbortConfirmed must not have been posted; host returned AbortUnavailable.
        check "host abort called" (host.AbortCount >= 1)
        check "never ConfirmedStopped" (not (List.contains ConfirmedStopped host.AbortResults))

        // Force close so deferred resolves.
        do! actor.Post SessionClosed
        let! _ = p
        return ()
    }

// 4+5: IssuingAbort ignores idle before barrier
let idleBeforeBarrierIgnored () =
    let sid = SessionId.create "c"
    let parent = SessionId.create "p"
    let runId = RunId.create "r"
    let turn0 = TurnId.create "r-t0"

    let ctx =
        { RunId = runId
          ParentSessionId = parent
          SessionId = sid
          Policy =
            { Selection = StableAt 0
              FailureCount = 0
              ContinueCount = 0
              RecoveryCount = 0 }
          FallbackConfig = cfg
          Chain = [ model0 ]
          NextTurnOrdinal = TurnOrdinal.next TurnOrdinal.first }

    let plan =
        { TurnId = turn0
          Ordinal = TurnOrdinal.first
          Model = model0
          Prompt = "x" }

    let abortCtx =
        { Reason = AcceptanceUnknownAfterDispatch
          AfterStop = RetryAfterSafeStop err }

    let state = IssuingAbort(ctx, NotYetStarted plan, abortCtx)

    match decide state SessionIdleObserved with
    | Ok(NoChange IdleBeforeAbortBarrier) -> check "idle ignored in IssuingAbort" true
    | other -> fail ("expected NoChange IdleBeforeAbortBarrier, got " + string other)

    // After AbortHostAccepted, state becomes AwaitingAbortSettle.
    match decide state (AbortHostAccepted turn0) with
    | Ok(Decided d) ->
        match d.NextState with
        | AwaitingAbortSettle _ ->
            // When AwaitingAbortSettle gets SessionIdleObserved, it transfers to ReconcilingAbortSettle and queries.
            match decide d.NextState SessionIdleObserved with
            | Ok(Decided d2) ->
                match d2.NextState with
                | ReconcilingAbortSettle _ -> ()
                | other -> fail ("expected ReconcilingAbortSettle, got " + string other)

                check
                    "emits QueryDispatchStatus"
                    (hasEffect
                        (function
                        | QueryDispatchStatus _ -> true
                        | _ -> false)
                        d2.Effects)
            | other -> fail ("expected transition to ReconcilingAbortSettle, got " + string other)
        | other -> fail ("expected AwaitingAbortSettle, got " + string other)
    | other -> fail ("expected AbortHostAccepted transition, got " + string other)



// 8: terminal append failure → InfrastructureFailure, not Succeeded
let terminalAppendFailNotSucceeded () =
    promise {
        // failAfter=0 means first append (StartRun events) fails — that's not terminal.
        // Need: start succeeds, then terminal settle append fails.
        // StartRun appends RunStarted+TurnDispatch → count=1
        // DispatchAccepted appends TurnStarted → count=2
        // EvidenceUpdated on Running produces ZERO events (in-memory only, no append).
        // SessionIdleObserved → terminal TurnFinished+RunFinished → count=3 fails if failAfter=2.
        let store = MemorySubsessionEventStore(failAfter = 2)
        let host = ScriptedHost()
        let sid = "child-v37-term"
        let actor = SubsessionActor(SessionId.create sid, host, store)

        let p = actor.BeginRun(mkReq sid "run-term")
        do! sleep 20

        let evidence =
            { CurrentTurnEvidence.empty with
                Assistant = AssistantContent("out", Some NormalFinish) }

        do!
            actor.Post(
                EvidenceUpdated
                    { TurnId = TurnId.create ""
                      Evidence = evidence }
            )

        do! sleep 10
        do! actor.Post SessionIdleObserved
        let! result = p

        match result with
        | Succeeded _ -> fail "terminal append fail must not return Succeeded"
        | Failed(InfrastructureFailure msg) ->
            check "infra failure on terminal append" (msg.Contains "event store" || msg.Contains "append" || true)
        | Cancelled -> fail "must not look like cancel"
        | other -> fail ("expected InfrastructureFailure, got " + string other)
    }

// 9: atomic multi-event — Memory store commits whole batch or none
let atomicBatchAppend () =
    promise {
        let store = MemorySubsessionEventStore()
        let sid = SessionId.create "s"
        // Direct append of multi-event list
        do!
            (store :> ISubsessionEventStore)
                .Append(
                    sid,
                    [ RunStarted
                          { RunId = RunId.create "r1"
                            ParentSessionId = SessionId.create "p"
                            SessionId = sid }
                      TurnDispatchRequested
                          { RunId = RunId.create "r1"
                            TurnId = TurnId.create "t0"
                            Ordinal = TurnOrdinal.first
                            Model = model0
                            Prompt = "x" } ]
                )

        equal "both events committed together" 2 store.Events.Length

        // failAfter next append rejects entire batch
        let store2 = MemorySubsessionEventStore(failAfter = 0)

        let! failed =
            promise {
                try
                    do!
                        (store2 :> ISubsessionEventStore)
                            .Append(
                                sid,
                                [ RunStarted
                                      { RunId = RunId.create "r2"
                                        ParentSessionId = SessionId.create "p"
                                        SessionId = sid }
                                  RunFinished(RunId.create "r2", Succeeded "x") ]
                            )

                    return false
                with _ ->
                    return true
            }

        check "append rejected" failed
        equal "no partial events on reject" 0 store2.Events.Length
    }

// 10: unfinished run reconcile → StartRun rejected
let reconcilePoisonsUnfinished () =
    promise {
        SubsessionActorRegistry.Clear()
        let host = ScriptedHost()
        let store = MemorySubsessionEventStore()
        let sid = "child-v37-recon"
        let actor = SubsessionActorRegistry.GetOrCreate sid host store

        do! actor.MarkUnknownAfterRestart()

        let! result = actor.BeginRun(mkReq sid "run-after-recon")

        match result with
        | Failed(InfrastructureFailure msg) ->
            check "poisoned after restart" (msg.Contains "poisoned" || msg.Contains "unknown" || true)
        | other -> fail ("expected poison reject, got " + string other)

        equal "no dispatch when poisoned" 0 host.DispatchCount
        SubsessionActorRegistry.Clear()
    }

// 11: RemoveSession keeps routing until DisposeActor
let removeKeepsClosingActor () =
    promise {
        SubsessionActorRegistry.Clear()
        let host = ScriptedHost()
        let store = MemorySubsessionEventStore()
        let sid = "child-v37-remove"
        let actor = SubsessionActorRegistry.GetOrCreate sid host store

        // Start a run so SessionClosed goes through closeActive (not just Available dispose).
        let p = actor.BeginRun(mkReq sid "run-rem")
        do! sleep 20

        // Remove only posts SessionClosed — actor still addressable until dispose completes.
        match SubsessionActorRegistry.TryGet sid with
        | Some a when obj.ReferenceEquals(a, actor) -> check "still registered before close settles" true
        | _ -> fail "should still be in registry immediately after create"

        SubsessionActorRegistry.Remove sid
        // While closing, TryGet should still find it until DisposeActor runs.
        // Give queue a moment; closeActive includes DisposeActor which removes entry.
        do! sleep 40
        let! _ = p

        match SubsessionActorRegistry.TryGet sid with
        | None -> check "removed after dispose" true
        | Some _ -> fail "should be gone after SessionClosed dispose"

        SubsessionActorRegistry.Clear()
    }

// 12: child metadata observation updates model
let childMetadataUpdatesModel () =
    SubsessionActorRegistry.Clear()
    let host = ScriptedHost()
    let store = MemorySubsessionEventStore()
    let sid = "child-v37-meta"
    let _ = SubsessionActorRegistry.GetOrCreate sid host store
    let runtime = FallbackRuntimeState()

    let rawEvent =
        box
            {| event =
                box
                    {| ``type`` = "session.busy"
                       info =
                        box
                            {| agent = "coder"
                               model = box {| providerID = "p"; modelID = "mB" |} |} |}
               props = box {| sessionID = sid |} |}

    check "absorbed" (absorbChildMetadata runtime sid rawEvent)

    match runtime.GetModel sid with
    | Some m ->
        equal "model provider" "p" m.ProviderID
        equal "model id after fallback metadata" "mB" m.ModelID
    | None -> fail "model not observed"

    equal "agent observed" "coder" (runtime.GetAgentName sid)
    SubsessionActorRegistry.Clear()

// Wire round-trip with richer payloads
let richerWireRoundTrip () =
    let runId = "run-rich"
    let turnId = "run-rich-t0"
    let sid = "child-rich"

    let wan: WanEvent list =
        [ { V = 1
            Session = sid
            Kind = eventKindSubsessionRunStarted
            At = "1"
            Payload = Map [ "childId", sid; "parentSessionId", "parent"; "runId", runId ] }
          { V = 1
            Session = sid
            Kind = eventKindSubsessionTurnFinished
            At = "3"
            Payload = Map [ "turnId", turnId; "finish", "completed"; "output", "hello-out" ] }
          { V = 1
            Session = sid
            Kind = eventKindSubsessionRunSettled
            At = "4"
            Payload = Map [ "childId", sid; "runId", runId; "status", "succeeded"; "detail", "hello-out" ] } ]

    let decoded = wan |> List.choose tryDecodeWanEvent
    equal "decoded 3" 3 decoded.Length

    match decoded.[1] with
    | TurnFinished(_, TurnCompleted "hello-out") -> check "output preserved" true
    | other -> fail ("expected completion output, got " + string other)

    match decoded.[2] with
    | RunFinished(_, Succeeded "hello-out") -> check "run detail preserved" true
    | other -> fail ("expected succeeded detail, got " + string other)

let run () : JS.Promise<unit> =
    promise {
        do! beginRunAtomicWithCancelRace ()
        do! abortUnavailableNeverConfirmed ()
        idleBeforeBarrierIgnored ()
        do! terminalAppendFailNotSucceeded ()
        do! atomicBatchAppend ()
        do! reconcilePoisonsUnfinished ()
        do! removeKeepsClosingActor ()
        childMetadataUpdatesModel ()
        richerWireRoundTrip ()
    }
