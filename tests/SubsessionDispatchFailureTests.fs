module Wanxiangshu.Tests.SubsessionDispatchFailureTests

open Wanxiangshu.Runtime.SubsessionEventPayload
open Fable.Core
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Fold
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.CommandProcessor
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.SubsessionActor
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.SubsessionEventStore
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Runtime.SubsessionChildObserver
open Wanxiangshu.Runtime.SubsessionEventWire
open Wanxiangshu.Tests.Assert

let private fail (msg: string) = check msg false

let private model0: FallbackModel =
    { ProviderID = "p"
      ModelID = "m0"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private cfg: FallbackConfig =
    { DefaultChain = [ model0 ]
      AgentChains = Map.empty
      MaxRetries = 1
      LoopMaxContinues = 10
      MaxRecoveries = 3
      LegacyZeroWidthContinue = false }

let private sleep (ms: int) : JS.Promise<unit> =
    Promise.create (fun resolve _ -> JS.setTimeout (fun () -> resolve ()) ms |> ignore)

type CountingHost() =
    let mutable dispatchCount = 0
    let mutable abortCount = 0

    member _.DispatchCount = dispatchCount
    member _.AbortCount = abortCount

    interface ISubsessionHost with
        member _.Dispatch(_sid, _turn) =
            dispatchCount <- dispatchCount + 1
            Promise.lift (Ok OrderedTurnMarkerObserved)

        member _.Abort(_sid, _tid) =
            abortCount <- abortCount + 1
            Promise.lift ConfirmedStopped

        member _.CancelPendingDispatch(_tid) = ()

        member _.QueryDispatchStatus(_, _) = Promise.lift Unknown

        member _.QuerySessionQuiescence(_, _) = Promise.lift Stopped

        member _.ClosePhysicalSession(_) = Promise.lift Stopped

/// 3. TurnStarted EventLog append failure → fail-safe abort, not early return without abort
let appendFailTriggersAbort () =
    promise {
        // StartRun appends RunStarted+TurnDispatchRequested → count=1 ok.
        // DispatchAccepted appends TurnStarted → count=2 fails → fail-safe abort.
        let store = MemorySubsessionEventStore(failAfter = 1)
        let host = CountingHost()
        let sid = SessionId.create "child-hard-append"
        let actor = SubsessionActor(sid, host, store)

        let request =
            { RunId = RunId.create "run-append"
              SessionId = sid
              ParentSessionId = SessionId.create "parent"
              Prompt = "x"
              FallbackConfig = cfg
              Directive = RetryChain [ model0 ]
              InitiallyCancelled = false }

        let p = actor.StartRun request
        // Dispatch Ok → DispatchAccepted → append TurnStarted fails → AbortHostSession
        do! sleep 40
        let! result = p

        match result with
        | Failed(InfrastructureFailure msg) ->
            check "append failure message" (msg.Contains "event store" || msg.Contains "append")
            check "host was aborted before resolve" (host.AbortCount >= 1)
        | Cancelled -> fail "append failure must not look like user cancel"
        | Succeeded s -> fail ("must not succeed: " + s)
        | other -> fail ("expected InfrastructureFailure, got " + string other)
    }

/// 9. Session dispose → Actor registry entry deleted
let removeSessionClearsRegistry () =
    promise {
        SubsessionActorRegistry.Clear()

        let host = CountingHost()
        let store = MemorySubsessionEventStore()
        let childId = "child-hard-remove"
        let actor = SubsessionActorRegistry.GetOrCreate "" childId host store

        match SubsessionActorRegistry.TryGet "" childId with
        | Some a -> check "actor registered" (obj.ReferenceEquals(a, actor))
        | None -> fail "actor missing after GetOrCreate"

        SubsessionActorRegistry.Remove "" childId
        do! sleep 20

        match SubsessionActorRegistry.TryGet "" childId with
        | None -> check "actor removed" true
        | Some _ -> fail "actor still in registry after Remove"

        SubsessionActorRegistry.Clear()
    }

/// 10. All SubsessionEvent kinds round-trip via WanEvent wire + fold
let allEventsReplayFromWire () =
    let runId = RunId.create "run-wire"
    let sid = SessionId.create "child-wire"
    let parent = SessionId.create "parent-wire"
    let turnId = TurnId.create "run-wire-t0"

    let domainEvents: SubsessionEvent list =
        [ RunStarted
              { RunId = runId
                ParentSessionId = parent
                SessionId = sid }
          TurnDispatchRequested
              { RunId = runId
                TurnId = turnId
                Ordinal = TurnOrdinal.first
                Model = model0
                Prompt = "go" }
          TurnStarted
              { RunId = runId
                TurnId = turnId
                Receipt = OrderedTurnMarkerObserved }
          TurnFinished(turnId, TurnCompleted "done")
          AbortRequested(runId, turnId)
          RunFinished(runId, Succeeded "done")
          SessionPoisoned(sid, HostProtocolBroken "test") ]

    // Encode the same payload shape NdjsonSubsessionEventStore would write,
    // then decode via tryDecodeWanEvent and fold.
    let wanEvents: WanEvent list =
        [ { V = 1
            Session = SessionId.value sid
            Kind = eventKindSubsessionRunStarted
            At = "1"
            Payload =
              Map
                  [ "childId", SessionId.value sid
                    "parentSessionId", SessionId.value parent
                    "runId", RunId.value runId ] }
          { V = 1
            Session = SessionId.value sid
            Kind = eventKindSubsessionTurnDispatchRequested
            At = "2"
            Payload =
              Map
                  [ "runId", RunId.value runId
                    "turnId", TurnId.value turnId
                    "turnOrdinal", "0"
                    "sessionId", SessionId.value sid
                    "model", "p/m0"
                    "prompt", "go" ] }
          { V = 1
            Session = SessionId.value sid
            Kind = eventKindSubsessionTurnStarted
            At = "3"
            Payload =
              Map
                  [ "runId", RunId.value runId
                    "turnId", TurnId.value turnId
                    "sessionId", SessionId.value sid
                    "receipt", "ordered_marker" ] }
          { V = 1
            Session = SessionId.value sid
            Kind = eventKindSubsessionTurnFinished
            At = "5"
            Payload =
              Map
                  [ "turnId", TurnId.value turnId
                    "sessionId", SessionId.value sid
                    "finish", "completed" ] }
          { V = 1
            Session = SessionId.value sid
            Kind = eventKindSubsessionAbortRequested
            At = "6"
            Payload = Map [ "turnId", TurnId.value turnId; "sessionId", SessionId.value sid ] }
          { V = 1
            Session = SessionId.value sid
            Kind = eventKindSubsessionRunSettled
            At = "7"
            Payload =
              Map
                  [ "childId", SessionId.value sid
                    "runId", RunId.value runId
                    "status", "succeeded" ] }
          { V = 1
            Session = SessionId.value sid
            Kind = eventKindSubsessionSessionPoisoned
            At = "8"
            Payload = Map [ "sessionId", SessionId.value sid; "reason", "host_protocol:test" ] } ]

    let decoded = wanEvents |> List.choose tryDecodeWanEvent
    equal "decoded all 7 domain events" 7 decoded.Length

    // In-memory domain fold of original events: start then finish removes active run.
    // SessionPoisoned leaves a PersistentlyPoisoned entry — no ActiveRun remains.
    let proj = projectEvents domainEvents

    let hasActiveRun =
        proj
        |> Map.exists (fun _ v ->
            match v with
            | ActiveRun _ -> true
            | _ -> false)

    check "no active run after finish+poison" (not hasActiveRun)

    let projWire = projectFromWanEvents wanEvents

    let hasActiveRunWire =
        projWire
        |> Map.exists (fun _ v ->
            match v with
            | ActiveRun _ -> true
            | _ -> false)

    check "wire fold also empty" (not hasActiveRunWire)

    // Active-only projection: start alone leaves one entry.
    let activeOnly =
        projectEvents
            [ RunStarted
                  { RunId = runId
                    ParentSessionId = parent
                    SessionId = sid } ]

    equal "one active after start" 1 (Map.count activeOnly)

/// 11. Child busy/metadata event is absorbed and does not require Main bridge
let childMetadataAbsorbed () =
    SubsessionActorRegistry.Clear()

    let host = CountingHost()
    let store = MemorySubsessionEventStore()
    let childId = "child-meta-absorb"
    let _ = SubsessionActorRegistry.GetOrCreate "" childId host store
    let runtime = FallbackRuntimeStore()

    check "isChildSession true" (isChildSession "" childId)
    check "absorb returns true" (absorbChildMetadata "" runtime childId (box null))
    check "unknown session false" (not (isChildSession "" "main-session-xyz"))

    SubsessionActorRegistry.Clear()

/// Memory store retains turn events (not only start/settle)
let memoryStoreKeepsAllDomainEvents () =
    promise {
        let host = CountingHost()
        let store = MemorySubsessionEventStore()
        let sid = SessionId.create "child-hard-events"
        let actor = SubsessionActor(sid, host, store)

        let request =
            { RunId = RunId.create "run-ev"
              SessionId = sid
              ParentSessionId = SessionId.create "parent"
              Prompt = "x"
              FallbackConfig = cfg
              Directive = RetryChain [ model0 ]
              InitiallyCancelled = false }

        let p = actor.StartRun request
        do! sleep 20

        let evidence =
            { CurrentTurnEvidence.empty with
                Assistant = AssistantSnapshot("", 0L, "out", Some NormalFinish) }

        do! actor.Post(EvidenceUpdated { TurnId = None; Evidence = evidence })

        do! sleep 5
        do! actor.Post SessionIdleObserved
        let! _ = p

        let kinds =
            store.Events
            |> List.map (function
                | RunStarted _ -> "RunStarted"
                | TurnDispatchRequested _ -> "TurnDispatchRequested"
                | TurnStarted _ -> "TurnStarted"
                | TurnFinished _ -> "TurnFinished"
                | AbortRequested _ -> "AbortRequested"
                | RunFinished _ -> "RunFinished"
                | SessionPoisoned _ -> "SessionPoisoned"
                | PhysicalSessionClosed _ -> "PhysicalSessionClosed")

        check "has TurnDispatchRequested" (List.contains "TurnDispatchRequested" kinds)
        check "has TurnStarted" (List.contains "TurnStarted" kinds)
        check "has TurnFinished" (List.contains "TurnFinished" kinds)
        check "has RunFinished" (List.contains "RunFinished" kinds)
    }

let run () : JS.Promise<unit> =
    promise {
        do! appendFailTriggersAbort ()
        do! removeSessionClearsRegistry ()
        allEventsReplayFromWire ()
        childMetadataAbsorbed ()
        do! memoryStoreKeepsAllDomainEvents ()
    }
