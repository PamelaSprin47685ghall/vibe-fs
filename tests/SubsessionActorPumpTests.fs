module Wanxiangshu.Tests.SubsessionActorPumpTests

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell.SubsessionActor
open Wanxiangshu.Shell.SubsessionEventStore
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
      MaxRecoveries = 3 }

type FakeHost
    (
        ?dispatchScript: unit -> Result<HostStartReceipt, DispatchFailure>,
        ?abortScript: unit -> AbortResult,
        ?queryDispatchStatusScript: unit -> DispatchStatus
    ) =
    let mutable dispatchCount = 0
    let mutable abortCount = 0
    let mutable cancelled: TurnId list = []

    member _.DispatchCount = dispatchCount
    member _.AbortCount = abortCount
    member _.Cancelled = cancelled

    interface ISubsessionHost with
        member _.Dispatch(_sid, _turn) =
            dispatchCount <- dispatchCount + 1

            let script = defaultArg dispatchScript (fun () -> Ok OrderedTurnMarkerObserved)

            Promise.lift (script ())

        member _.Abort(_sid, _tid) =
            abortCount <- abortCount + 1
            let script = defaultArg abortScript (fun () -> ConfirmedStopped)
            Promise.lift (script ())

        member _.CancelPendingDispatch(tid) = cancelled <- tid :: cancelled

        member _.QueryDispatchStatus(_, _) =
            let script =
                defaultArg queryDispatchStatusScript (fun () -> DispatchStatus.DefinitelyNotAccepted)

            Promise.lift (script ())

let private sleep (ms: int) : JS.Promise<unit> =
    Promise.create (fun resolve _ -> JS.setTimeout (fun () -> resolve ()) ms |> ignore)

let private mkRequest runId =
    { RunId = RunId.create runId
      SessionId = SessionId.create ("child-" + runId)
      ParentSessionId = SessionId.create "parent"
      Prompt = "go"
      FallbackConfig = cfg
      Directive = RetryChain [ model0 ]
      InitiallyCancelled = false }

/// 1. Dispatch Ok → DispatchAccepted → Running (no queue self-deadlock)
let dispatchOkReachesRunning () =
    promise {
        let host = FakeHost()
        let store = MemorySubsessionEventStore()
        let sid = SessionId.create "child-pump-1"
        let actor = SubsessionActor(sid, host, store)

        let request =
            { mkRequest "run-pump-1" with
                SessionId = sid }

        let runP = actor.StartRun request
        do! sleep 20

        match actor.GetState() with
        | Running _ -> check "reached Running after dispatch Ok" true
        | other -> fail ("expected Running, got " + string other)

        equal "dispatch called once" 1 host.DispatchCount

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
        let! result = runP

        match result with
        | Succeeded "out" -> check "run succeeded" true
        | other -> fail ("expected Succeeded, got " + string other)

        check "events appended" (not (List.isEmpty store.Events))
    }

/// 2. Second StartRun while running → AlreadyRunning reject, no hang
let concurrentStartRejected () =
    promise {
        let host = FakeHost()
        let store = MemorySubsessionEventStore()
        let sid = SessionId.create "child-pump-2"
        let actor = SubsessionActor(sid, host, store)

        let req1 =
            { mkRequest "run-a" with
                SessionId = sid }

        let req2 =
            { req1 with
                RunId = RunId.create "run-b"
                Prompt = "b" }

        let p1 = actor.StartRun req1
        do! sleep 20

        let! r2 = actor.StartRun req2

        match r2 with
        | Failed(ProtocolViolation _) -> check "second start rejected" true
        | other -> fail ("expected ProtocolViolation, got " + string other)

        do!
            actor.Post(
                EvidenceUpdated
                    { TurnId = TurnId.create ""
                      Evidence =
                        { CurrentTurnEvidence.empty with
                            Assistant = AssistantContent("x", Some NormalFinish) } }
            )

        do! sleep 5
        do! actor.Post SessionIdleObserved
        let! r1 = p1

        match r1 with
        | Succeeded _ -> check "first run completed" true
        | other -> fail ("expected Succeeded, got " + string other)
    }

/// 3. SessionClosed while running resolves caller
let sessionClosedResolves () =
    promise {
        let host = FakeHost()
        let store = MemorySubsessionEventStore()
        let sid = SessionId.create "child-pump-3"
        let actor = SubsessionActor(sid, host, store)

        let request =
            { mkRequest "run-c" with
                SessionId = sid }

        let p = actor.StartRun request
        do! sleep 20
        do! actor.Post SessionClosed
        let! result = p

        match result with
        | Failed(InfrastructureFailure _) -> check "session closed fails run" true
        | other -> fail ("expected InfrastructureFailure, got " + string other)

        check "actor poisoned or disposed" (actor.IsPoisoned || actor.IsDisposed)
    }

/// 5. InitiallyCancelled StartRun never dispatches
let initiallyCancelledNoDispatch () =
    promise {
        let host = FakeHost()
        let store = MemorySubsessionEventStore()
        let sid = SessionId.create "child-pump-5"
        let actor = SubsessionActor(sid, host, store)

        let request =
            { mkRequest "run-cancel" with
                SessionId = sid
                InitiallyCancelled = true }

        let! result = actor.StartRun request

        match result with
        | Cancelled -> check "initially cancelled" true
        | other -> fail ("expected Cancelled, got " + string other)

        equal "no dispatch" 0 host.DispatchCount
    }

/// 6. AcceptanceUnknown + AbortConfirmed → retry or fail, not Cancelled
let acceptanceUnknownAbortConfirmed () =
    promise {
        let host =
            FakeHost(
                dispatchScript =
                    (fun () ->
                        Error(
                            DispatchFailure.HostAcceptanceUnknown
                                { ErrorName = "Network"
                                  DomainError = None
                                  Message = "timeout"
                                  StatusCode = None
                                  IsRetryable = Some true }
                        )),
                abortScript = (fun () -> ConfirmedStopped),
                queryDispatchStatusScript = (fun () -> DispatchStatus.Accepted OrderedTurnMarkerObserved)
            )

        let store = MemorySubsessionEventStore()
        let sid = SessionId.create "child-pump-6"
        let actor = SubsessionActor(sid, host, store)

        let request =
            { mkRequest "run-au" with
                SessionId = sid }

        let p = actor.StartRun request
        // Allow dispatch reject → abort → AbortConfirmed → AfterAbort
        do! sleep 50
        let! result = p

        match result with
        | Cancelled -> fail "AcceptanceUnknown must not masquerade as Cancelled"
        | Failed _
        | Succeeded _ ->
            // With MaxRetries=1 may retry then exhaust, or fail; either is fine.
            check "not cancelled after AcceptanceUnknown" true
            check "abort was requested" (host.AbortCount >= 1)
    // Cancelled already handled
    }

/// 7. AbortConfirmed on idle session settles without waiting full abort deadline
let abortConfirmedSettlesFast () =
    promise {
        let host = FakeHost(abortScript = fun () -> ConfirmedStopped)
        let store = MemorySubsessionEventStore()
        let sid = SessionId.create "child-pump-7"
        let actor = SubsessionActor(sid, host, store)

        let request =
            { mkRequest "run-fast-abort" with
                SessionId = sid }

        let p = actor.StartRun request
        do! sleep 20
        do! actor.Post CancelRequested
        // AbortConfirmed comes from host.Abort ConfirmedStopped
        do! sleep 30
        let! result = p

        match result with
        | Cancelled -> check "user cancel settles via AbortConfirmed" true
        | other -> fail ("expected Cancelled, got " + string other)

        check "not poisoned" (not actor.IsPoisoned)
    }

let run () : JS.Promise<unit> =
    promise {
        do! dispatchOkReachesRunning ()
        do! concurrentStartRejected ()
        do! sessionClosedResolves ()
        do! initiallyCancelledNoDispatch ()
        do! acceptanceUnknownAbortConfirmed ()
        do! abortConfirmedSettlesFast ()
    }
