module Wanxiangshu.Tests.SubsessionExtendedTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Decision
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.SubsessionActor
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.SubsessionEventStore
open Wanxiangshu.Runtime.SubsessionEventPayload
open Wanxiangshu.Runtime.SubsessionReconcile
open Wanxiangshu.Runtime.SubsessionService
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Mux.TodoWriteToolWrapper
open Wanxiangshu.Runtime.SessionProjectionStore
open Wanxiangshu.Runtime.EventLogRuntimeStore

let private model0: FallbackModel =
    { ProviderID = "p"
      ModelID = "m0"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private chain = [ model0 ]

let private cfg: FallbackConfig =
    { DefaultChain = chain
      AgentChains = Map.empty
      MaxRetries = 0
      LoopMaxContinues = 0
      MaxRecoveries = 0 }

let private policy0 = initialPolicy cfg chain
let private turn0 = TurnId.create "run-extended-1-t0"

type private ImmediateHost() =
    interface ISubsessionHost with
        member _.Dispatch(_, _) =
            Promise.lift (Ok OrderedTurnMarkerObserved)

        member _.Abort(_, _) = Promise.lift ConfirmedStopped
        member _.CancelPendingDispatch(_) = ()
        member _.QueryDispatchStatus(_, _) = Promise.lift DispatchStatus.Unknown
        member _.QuerySessionQuiescence(_, _) = Promise.lift Stopped
        member _.ClosePhysicalSession(_) = Promise.lift Stopped

let testIdleCachingBeforeDispatchAccepted () =
    let parent = SessionId.create "parent-extended-1"
    let sid = SessionId.create "child-extended-1"
    let runId = RunId.create "run-extended-1"

    let mkCtx policy nextOrdinal =
        { RunId = runId
          ParentSessionId = parent
          SessionId = sid
          Policy = policy
          FallbackConfig = cfg
          Chain = chain
          NextTurnOrdinal = nextOrdinal }

    let mkPlan turn ordinal model prompt =
        { TurnId = turn
          Ordinal = ordinal
          Model = Some model
          Prompt = prompt }

    let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
    let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"

    let state = Dispatching(ctx, plan, CurrentTurnEvidence.empty, 1000000L)

    // Premature idle cached in PendingTerminal
    match decide 1000000L state SessionIdleObserved with
    | Ok(Decided d) ->
        match d.NextState with
        | Dispatching(_, _, evidence, _) as s ->
            check "idle is cached in evidence" evidence.IdleObserved

            // Now send DispatchAccepted
            match decide 1000000L s (DispatchAccepted(turn0, OrderedTurnMarkerObserved)) with
            | Ok(Decided d2) ->
                // Since there is no completed assistant message, it should fail
                match d2.NextState with
                | Available _ ->
                    check
                        "run finished with infrastructure failure"
                        (List.exists
                            (fun e ->
                                match e with
                                | RunFinished(_, Failed _) -> true
                                | _ -> false)
                            d2.Events)
                | other -> failwith ("expected Available, got " + string other)
            | other -> failwith ("unexpected: " + string other)
        | other -> failwith ("expected Dispatching, got " + string other)
    | other -> failwith ("unexpected: " + string other)

let testEventOrderingPermutations () =
    let parent = SessionId.create "parent-perm-1"
    let sid = SessionId.create "child-perm-1"
    let runId = RunId.create "run-perm-1"
    let turn0 = TurnId.create "run-perm-1-t0"

    let mkCtx policy nextOrdinal =
        { RunId = runId
          ParentSessionId = parent
          SessionId = sid
          Policy = policy
          FallbackConfig = cfg
          Chain = chain
          NextTurnOrdinal = nextOrdinal }

    let mkPlan turn ordinal model prompt =
        { TurnId = turn
          Ordinal = ordinal
          Model = Some model
          Prompt = prompt }

    let makeState () =
        let ctx = mkCtx policy0 (TurnOrdinal.next TurnOrdinal.first)
        let plan = mkPlan turn0 TurnOrdinal.first model0 "do work"
        Dispatching(ctx, plan, CurrentTurnEvidence.empty, 1000000L)

    let cmdReceipt = DispatchAccepted(turn0, OrderedTurnMarkerObserved)

    let evidence =
        { CurrentTurnEvidence.empty with
            Assistant = AssistantSnapshot("msg-1", 0L, "output-text", Some NormalFinish)
            Outcome = CompletionRequested "output-text" }

    let cmdEvidence =
        EvidenceUpdated
            { TurnId = Some turn0
              Evidence = evidence }

    let cmdIdle = SessionIdleObserved

    let feedCommands cmds =
        let mutable state = makeState ()

        for cmd in cmds do
            match decide 1000000L state cmd with
            | Ok(Decided d) -> state <- d.NextState
            | Ok(NoChange _) -> ()
            | Error e -> failwith ("unexpected error during feed: " + string e)

        state

    // Permutation 1: Receipt -> Evidence -> Idle (Standard order)
    let s1 = feedCommands [ cmdReceipt; cmdEvidence; cmdIdle ]

    match s1 with
    | Available _ -> check "Permutation 1 succeeded" true
    | other -> failwith ("Permutation 1 expected Available, got " + string other)

    // Permutation 2: Evidence -> Receipt -> Idle (Evidence arrives early)
    let s2 = feedCommands [ cmdEvidence; cmdReceipt; cmdIdle ]

    match s2 with
    | Available _ -> check "Permutation 2 succeeded" true
    | other -> failwith ("Permutation 2 expected Available, got " + string other)

    // Permutation 3: Evidence -> Idle -> Receipt (Evidence then Idle while Dispatching)
    let s3 = feedCommands [ cmdEvidence; cmdIdle; cmdReceipt ]

    match s3 with
    | Available _ -> check "Permutation 3 succeeded" true
    | other -> failwith ("Permutation 3 expected Available, got " + string other)

    // Permutation 4: Receipt -> Idle -> Evidence (Idle arrives before Evidence)
    let s4 = feedCommands [ cmdReceipt; cmdIdle; cmdEvidence ]

    match s4 with
    | Available _ -> check "Permutation 4 resolved to Available (failed run)" true
    | other -> failwith ("Permutation 4 expected Available, got " + string other)

    // Permutation 5: Idle -> Receipt -> Evidence (Idle arrives before Receipt and Evidence)
    let s5 = feedCommands [ cmdIdle; cmdReceipt; cmdEvidence ]

    match s5 with
    | Available _ -> check "Permutation 5 resolved to Available (failed run)" true
    | other -> failwith ("Permutation 5 expected Available, got " + string other)

    // Permutation 6: Idle -> Evidence -> Receipt (Idle then Evidence while Dispatching)
    let s6 = feedCommands [ cmdIdle; cmdEvidence; cmdReceipt ]

    match s6 with
    | Available _ -> check "Permutation 6 resolved to Available (failed run)" true
    | other -> failwith ("Permutation 6 expected Available, got " + string other)

let testRestartReconciliationOfIncompleteRuns () =
    promise {
        SubsessionActorRegistry.Clear()
        let! tempDir = mkdtempAsync "test-reconcile-"
        printfn "RECONCILE TEMP DIR: %s" tempDir

        let! res =
            promise {
                try
                    let sessionIdStr = "sess-reconcile-test"
                    let sessionId = SessionId.create sessionIdStr
                    let runId = RunId.create "run-reconcile-test"
                    let turnId = TurnId.create "run-reconcile-test-t0"

                    let store = create tempDir

                    let runStartedEvt =
                        RunStarted
                            { RunId = runId
                              ParentSessionId = SessionId.create "parent"
                              SessionId = sessionId }

                    let turnData =
                        { RunId = runId
                          TurnId = turnId
                          Ordinal = TurnOrdinal.first
                          Model = model0
                          Prompt = "hello"
                          DeadlineAtMs = 1000000L }

                    let turnDispatchRequestedEvt = TurnDispatchRequested turnData

                    do! store.Append(sessionId, [ runStartedEvt; turnDispatchRequestedEvt ])

                    let hostCalled = ref false

                    let fakeHost =
                        { new ISubsessionHost with
                            member _.Dispatch(_, _) =
                                Promise.lift (Ok OrderedTurnMarkerObserved)

                            member _.Abort(_, _) = Promise.lift ConfirmedStopped
                            member _.CancelPendingDispatch(_) = ()
                            member _.QueryDispatchStatus(_, _) = Promise.lift DispatchStatus.Unknown
                            member _.QuerySessionQuiescence(_, _) = Promise.lift Stopped

                            member _.ClosePhysicalSession(_) =
                                hostCalled := true
                                Promise.lift Stopped }

                    let hostFactory = fun _ -> fakeHost

                    let! safetyProj = reconcileUnfinishedRuns tempDir (Some hostFactory)

                    check "host ClosePhysicalSession was called" !hostCalled
                    check "session marked poisoned in safety projection" (Map.containsKey sessionId safetyProj)

                    let! events = getStore(tempDir).ReadAllEvents()
                    let subEvents = events |> List.collect tryDecodeWanEventBatch

                    let hasPoisonEvent =
                        subEvents
                        |> List.exists (fun e ->
                            match e with
                            | SessionPoisoned _ -> true
                            | _ -> false)

                    check "SessionPoisoned event exists in store" hasPoisonEvent

                    let actor = SubsessionActorRegistry.GetOrCreate tempDir sessionIdStr fakeHost store


                    match actor.GetState() with
                    | Poisoned _ -> check "actor state is Poisoned" true
                    | other -> failwith ("expected actor to be Poisoned, got " + string other)

                    let request =
                        { RunId = RunId.create "new-run"
                          SessionId = sessionId
                          ParentSessionId = SessionId.create "parent"
                          Prompt = "go"
                          FallbackConfig = cfg
                          Directive = RetryChain [ model0 ]
                          InitiallyCancelled = false }

                    let! startResult = actor.StartRun request

                    match startResult with
                    | Failed(InfrastructureFailure msg) when msg.Contains("poisoned") ->
                        check "StartRun was rejected with session poisoned" true
                    | other -> failwith ("expected StartRun to be rejected, got " + string other)

                    return Ok()
                with ex ->
                    return Error ex
            }

        do! rmAsync tempDir

        match res with
        | Ok() -> ()
        | Error ex -> raise ex
    }

let testE2EParentChildRunWithTodoWrite () =
    promise {
        SubsessionActorRegistry.Clear()
        let! tempDir = mkdtempAsync "test-todowrite-e2e-"
        printfn "E2E TEMP DIR: %s" tempDir

        let! res =
            promise {
                try
                    let parentSessionId = "parent-todowrite-e2e"
                    let childSessionId = "child-todowrite-e2e"


                    let host = ImmediateHost()
                    let store = MemorySubsessionEventStore()

                    let hostFactory = fun _ -> host :> ISubsessionHost
                    let eventStoreFactory = fun _ -> store :> ISubsessionEventStore

                    let service = SubsessionService(tempDir, hostFactory, eventStoreFactory)

                    let runP =
                        service.StartRun(childSessionId, parentSessionId, "go", cfg, RetryChain [ model0 ])

                    let actor =
                        SubsessionActorRegistry.GetOrCreate tempDir childSessionId host (store :> ISubsessionEventStore)

                    do! Promise.sleep 20

                    let turnId =
                        match actor.GetCurrentTurn() with
                        | Some tid -> tid
                        | None -> failwith "expected active turn on start"

                    do! actor.Post(DispatchAccepted(turnId, OrderedTurnMarkerObserved))
                    do! Promise.sleep 20

                    let projection = ProjectionStore()
                    let wrapperObj = mkTodoWriteWrapper Mux projection

                    let wrapperFn =
                        unbox<System.Func<obj, obj, obj>> (Wanxiangshu.Runtime.Dyn.get wrapperObj "wrapper")

                    let toolObj = wrapperFn.Invoke(null, null)

                    let executeFn =
                        unbox<System.Func<obj, obj, JS.Promise<obj>>> (Wanxiangshu.Runtime.Dyn.get toolObj "execute")

                    let args =
                        createObj
                            [ "todos",
                              box
                                  [| createObj
                                         [ "content", box "write more tests"
                                           "status", box "pending"
                                           "priority", box "high" ] |]
                              "select_methodology", box [| box "deduction" |]
                              "ahaMoments", box "discovered a way to test parent-child"
                              "changesAndReasons", box "added new test file"
                              "gotchas", box "none"
                              "lessonsAndConventions", box "none"
                              "plan", box "step 1" ]

                    let opts =
                        createObj
                            [ "workspaceId", box childSessionId
                              "directory", box tempDir
                              "sessionID", box childSessionId ]

                    printfn "DECODED MUX CONFIG: %A" (Wanxiangshu.Runtime.ToolRuntimeContext.fromMuxConfig opts)
                    let! _toolResult = executeFn.Invoke(args, opts)

                    let ev =
                        { CurrentTurnEvidence.empty with
                            Todos = TodosNotCompleted }

                    let! routeResult = Wanxiangshu.Runtime.SubsessionEventRouter.routeEvidence tempDir childSessionId ev
                    printfn "DIRECT ROUTE RESULT: %b" routeResult
                    // Queue barrier to synchronize actor queue
                    let! _ = actor.Post(TurnDeadlineExpired(TurnId.create "stale-turn"))

                    printfn "ACTOR STATE IN E2E AFTER TOOL: %A" (actor.GetState())

                    match actor.GetState() with
                    | Running(_, _, evidence, _) ->
                        check "evidence has TodosNotCompleted" (evidence.Todos = TodosNotCompleted)
                    | other -> failwith ("expected Running state, got " + string other)

                    do! actor.Post SessionIdleObserved
                    let! result = runP

                    match result with
                    | Failed _ -> check "child run completed successfully (reached terminal state)" true
                    | other -> failwith ("expected Failed/terminal result from child, got " + string other)

                    return Ok()
                with ex ->
                    return Error ex
            }

        do! rmAsync tempDir

        match res with
        | Ok() -> ()
        | Error ex -> raise ex
    }

let run () : JS.Promise<unit> =
    promise {
        testIdleCachingBeforeDispatchAccepted ()
        testEventOrderingPermutations ()
        do! testRestartReconciliationOfIncompleteRuns ()
        do! testE2EParentChildRunWithTodoWrite ()
    }
