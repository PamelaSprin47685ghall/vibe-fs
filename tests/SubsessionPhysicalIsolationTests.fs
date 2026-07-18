module Wanxiangshu.Tests.SubsessionPhysicalIsolationTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Decision
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.Fold
open Wanxiangshu.Runtime.CommandProcessor
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.SubsessionActor
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.SubsessionEventStore
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Hosts.Opencode.SubsessionDispatch

module OpencodeHost = Wanxiangshu.Hosts.Opencode.SubsessionHostAdapter
module OmpHost = Wanxiangshu.Hosts.Omp.SubsessionHostAdapter

let private createEmpty () = createObj []
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

let private policy0 = initialPolicy cfg [ model0 ]

let private makeCtx runId =
    { RunId = runId
      ParentSessionId = SessionId.create "parent-v40"
      SessionId = SessionId.create "child-v40"
      Policy = policy0
      FallbackConfig = cfg
      Chain = [ model0 ]
      NextTurnOrdinal = TurnOrdinal.next TurnOrdinal.first }

let private makePlan runId =
    { TurnId = TurnId.create (RunId.value runId + "-t0")
      Ordinal = TurnOrdinal.first
      Model = Some model0
      Prompt = "go" }

let private err: ErrorInput =
    { ErrorName = "Network"
      DomainError = None
      Message = "timeout"
      StatusCode = None
      IsRetryable = Some true }

// ── OpenCode host adapter: receipt barrier and transport-state queries ──

let private makeClient (promptResult: obj option) (messagesData: obj) =
    let prompt: obj -> JS.Promise<obj> =
        match promptResult with
        | Some r -> fun _ -> Promise.lift r
        | None -> fun _ -> Promise.reject (exn "prompt failed")

    let messages: obj -> JS.Promise<obj> =
        fun _ -> Promise.lift (createObj [ "data", messagesData ])

    let session = createObj [ "prompt", box prompt; "messages", box messages ]
    createObj [ "session", box session ]

let private opencodeReceiptWaitsForObservation () =
    promise {
        let runId = RunId.create "run-v40-oc-receipt"
        let sid = SessionId.create "child-v40-oc-receipt"
        let turnId = TurnId.create (RunId.value runId + "-t0")
        let plan = makePlan runId
        let client = makeClient (Some(box {| id = "msg-1" |})) (box null)
        let host = OpencodeHost.createHost client "" ""
        let dispatchP = host.Dispatch(sid, plan)
        do! sleep 5
        // Transport (prompt) may have resolved, but the receipt must not resolve until observed.
        Wanxiangshu.Hosts.Opencode.SubsessionDispatch.PendingTurnReceipt.tryResolve
            (TurnId.value turnId)
            (UserMessageObserved "msg-1")
        |> ignore

        let! result = dispatchP

        match result with
        | Ok(UserMessageObserved "msg-1") -> check "receipt resolved by observation" true
        | other -> fail ("expected UserMessageObserved msg-1, got " + string other)
    }

let private opencodeQueryDispatchStatusRejectedBeforeSend () =
    promise {
        let runId = RunId.create "run-v40-oc-reject"
        let sid = SessionId.create "child-v40-oc-reject"
        let turnId = TurnId.create (RunId.value runId + "-t0")
        let plan = makePlan runId
        let client = createEmpty () // no session API
        let host = OpencodeHost.createHost client "" ""
        let _ = host.Dispatch(sid, plan) |> ignore
        do! sleep 5
        let! status = host.QueryDispatchStatus(sid, turnId)

        match status with
        | TransportRejectedBeforeSend _ -> check "rejected before send detected" true
        | other -> fail ("expected TransportRejectedBeforeSend, got " + string other)
    }

let private opencodeRejectedBeforeSendRejectsLateReceipt () =
    promise {
        let runId = RunId.create "run-v40-oc-reject-late-receipt"
        let sid = SessionId.create "child-v40-oc-reject-late-receipt"
        let turnId = TurnId.create (RunId.value runId + "-t0")
        let host = OpencodeHost.createHost (createEmpty ()) "" ""
        let dispatchP = host.Dispatch(sid, makePlan runId)
        do! sleep 5
        let! dispatchResult = dispatchP

        match dispatchResult with
        | Error(HostRejected _) -> check "pre-send rejection completes dispatch" true
        | other -> fail ("expected HostRejected, got " + string other)

        check
            "late receipt after pre-send rejection is ignored"
            (not (
                Wanxiangshu.Hosts.Opencode.SubsessionDispatch.PendingTurnReceipt.tryResolve
                    (TurnId.value turnId)
                    (UserMessageObserved "late")
            ))
    }

let private opencodeQueryDispatchStatusFailedAfterUnknown () =
    promise {
        let runId = RunId.create "run-v40-oc-fail"
        let sid = SessionId.create "child-v40-oc-fail"
        let turnId = TurnId.create (RunId.value runId + "-t0")
        let plan = makePlan runId
        let client = makeClient None (box null)
        let host = OpencodeHost.createHost client "" ""
        let _ = host.Dispatch(sid, plan) |> ignore
        do! sleep 5
        let! status = host.QueryDispatchStatus(sid, turnId)

        match status with
        | TransportFailedAfterUnknownAcceptance _ -> check "failed after unknown detected" true
        | other -> fail ("expected TransportFailedAfterUnknownAcceptance, got " + string other)
    }

let private opencodeCancelPendingDispatchKeepsReceiptCorrelation () =
    promise {
        let runId = RunId.create "run-v40-oc-cancel-receipt"
        let sid = SessionId.create "child-v40-oc-cancel-receipt"
        let turnId = TurnId.create (RunId.value runId + "-t0")
        let plan = makePlan runId
        let client = makeClient (Some(box {| id = "msg-1" |})) (box null)
        let host = OpencodeHost.createHost client "" ""
        let dispatchP = host.Dispatch(sid, plan)
        do! sleep 5
        host.CancelPendingDispatch turnId

        Wanxiangshu.Hosts.Opencode.SubsessionDispatch.PendingTurnReceipt.tryResolve
            (TurnId.value turnId)
            (UserMessageObserved "msg-1")
        |> ignore

        let! result = dispatchP

        match result with
        | Ok(UserMessageObserved "msg-1") -> check "receipt resolved after cancel" true
        | other -> fail ("expected UserMessageObserved msg-1, got " + string other)
    }

let private opencodeQuiescenceHonestUnknown () =
    promise {
        let host = OpencodeHost.createHost (createEmpty ()) "" ""
        let! status = host.QuerySessionQuiescence(SessionId.create "s", TurnId.create "t")
        equal "OpenCode quiescence is StopUnknown" StopUnknown status
    }

let private opencodeQuiescenceStillRunning () =
    promise {
        let runId = RunId.create "run-still-running"
        let sid = SessionId.create "child-still-running"
        let turnId = TurnId.create (RunId.value runId + "-t0")

        let msg = createObj [ "id", box (TurnId.value turnId); "status", box "running" ]
        let messagesData = [| msg |]
        let client = makeClient None messagesData
        let host = OpencodeHost.createHost client "" ""

        let! status = host.QuerySessionQuiescence(sid, turnId)
        equal "OpenCode quiescence is StillRunning" StillRunning status
    }

let private opencodeQuiescenceStopped () =
    promise {
        let runId = RunId.create "run-stopped"
        let sid = SessionId.create "child-stopped"
        let turnId = TurnId.create (RunId.value runId + "-t0")

        let msg = createObj [ "id", box (TurnId.value turnId); "status", box "completed" ]
        let messagesData1 = [| msg |]
        let client1 = makeClient None messagesData1
        let host1 = OpencodeHost.createHost client1 "" ""

        let! status1 = host1.QuerySessionQuiescence(sid, turnId)
        equal "OpenCode quiescence is Stopped when message completed" Stopped status1

        let messagesData2 = [||]
        let client2 = makeClient None messagesData2
        let host2 = OpencodeHost.createHost client2 "" ""

        let! status2 = host2.QuerySessionQuiescence(sid, turnId)
        equal "OpenCode quiescence is StopUnknown when nonce is absent" StopUnknown status2
    }

let private ompQuiescenceHonestUnknown () =
    promise {
        let host = OmpHost.createHost (createEmpty ()) "" (createEmpty ()) ""
        let! status = host.QuerySessionQuiescence(SessionId.create "s", TurnId.create "t")
        equal "OMP quiescence is StopUnknown" StopUnknown status
    }

// ── Decision / evidence semantics ──

let private evidenceMergeOutcomePriorityAndSnapshot () =
    let baseEv =
        { CurrentTurnEvidence.empty with
            Assistant = AssistantSnapshot("", 0L, "first", Some NormalFinish)
            Tool = HasToolResult
            Todos = TodosCompleted }

    let overrideEv =
        { CurrentTurnEvidence.empty with
            Assistant = AssistantSnapshot("", 0L, "second", Some NormalFinish)
            Todos = TodosNotCompleted
            Outcome = CompletionRequested "second-out" }

    let merged = CurrentTurnEvidence.merge baseEv overrideEv

    equal
        "assistant snapshot replaced"
        "second"
        (match merged.Assistant with
         | AssistantSnapshot(_, _, text, _) -> text
         | _ -> "")

    equal "tool result persists" HasToolResult merged.Tool
    equal "todos replaced" TodosNotCompleted merged.Todos
    equal "completion outcome wins" (CompletionRequested "second-out") merged.Outcome

    let failureThenCompletion =
        CurrentTurnEvidence.merge
            { CurrentTurnEvidence.empty with
                Outcome = FailureObserved err }
            { CurrentTurnEvidence.empty with
                Outcome = CompletionRequested "done" }

    equal "completion over failure" (CompletionRequested "done") failureThenCompletion.Outcome

    let completionThenFailure =
        CurrentTurnEvidence.merge
            { CurrentTurnEvidence.empty with
                Outcome = CompletionRequested "done" }
            { CurrentTurnEvidence.empty with
                Outcome = FailureObserved err }

    equal "completion resists failure" (CompletionRequested "done") completionThenFailure.Outcome

    let finishChanged =
        CurrentTurnEvidence.merge
            { CurrentTurnEvidence.empty with
                Assistant = AssistantSnapshot("message", 1L, "draft", Some NormalFinish) }
            { CurrentTurnEvidence.empty with
                Assistant = AssistantSnapshot("message", 2L, "tool call", Some ToolFinish) }

    equal
        "newer snapshot upgrades finish"
        (AssistantSnapshot("message", 2L, "tool call", Some ToolFinish))
        finishChanged.Assistant

let private runIdUsesFullGuid () =
    let value = RunId.newId () |> RunId.value
    check "RunId carries 128-bit GUID" (value.Length = 36 && value.StartsWith "run-")

let private issuingAbortBuffersIdle () =
    let runId = RunId.create "run-v40-idle"
    let ctx = makeCtx runId
    let plan = makePlan runId

    let abortCtx =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = IssuingAbort(ctx, NotYetStarted plan, abortCtx, false)

    match decide state SessionIdleObserved with
    | Ok(Decided d) ->
        match d.NextState with
        | IssuingAbort(_, _, _, true) -> check "idle buffered before abort barrier" true
        | other -> fail ("expected IssuingAbort with idleBuffered=true, got " + string other)

        check "no events on idle buffer" (List.isEmpty d.Events)
        check "no effects on idle buffer" (List.isEmpty d.Effects)
    | other -> fail ("expected Decided on idle, got " + string other)

let private reconcilingAbortSettleUsesQuiescenceNotDispatchStatus () =
    let runId = RunId.create "run-v40-quiescence"
    let ctx = makeCtx runId
    let plan = makePlan runId

    let started =
        { Plan = plan
          StartReceipt = OrderedTurnMarkerObserved }

    let abortCtx =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = ReconcilingAbortSettle(ctx, Started started, abortCtx)

    match decide state (DispatchStatusResolved(Accepted OrderedTurnMarkerObserved)) with
    | Error(IllegalTransition _) -> check "ReconcilingAbortSettle rejects DispatchStatusResolved" true
    | other -> fail ("expected IllegalTransition, got " + string other)

    match decide state (SessionQuiescenceResolved Stopped) with
    | Ok(Decided d) ->
        match d.NextState with
        | Available _ -> check "quiescence Stopped settles run" true
        | other -> fail ("expected Available after Stopped, got " + string other)
    | other -> fail ("expected Decided on SessionQuiescenceResolved, got " + string other)

let private reconcilingUnknownDeadlineExpiresTwicePoisons () =
    let runId = RunId.create "run-v40-reconcile"
    let ctx = makeCtx runId
    let plan = makePlan runId

    let cancelCtx =
        { Reason = AcceptanceUnknownAfterDispatch
          AfterStop = RetryAfterSafeStop err }

    let state = ReconcilingUnknownDispatch(ctx, plan, cancelCtx, 0)

    match decide state (ReconciliationDeadlineExpired plan.TurnId) with
    | Ok(Decided d1) ->
        match d1.NextState with
        | ReconcilingUnknownDispatch(_, _, _, retryCount) ->
            equal "retry count is 1 after first expiry" 1 retryCount

            match decide d1.NextState (ReconciliationDeadlineExpired plan.TurnId) with
            | Ok(Decided d2) ->
                match d2.NextState with
                | ClosingUnknownDispatch _ as closing ->
                    match decide closing (PhysicalCloseResolved Stopped) with
                    | Ok(Decided closed) ->
                        match closed.NextState with
                        | Poisoned _ ->
                            check "physical close permits terminal poison" true

                            check
                                "physical close completes caller"
                                (closed.Effects
                                 |> List.exists (function
                                     | CompleteCaller _ -> true
                                     | _ -> false))
                        | other -> fail ("expected Poisoned after physical close, got " + string other)
                    | other -> fail ("expected Decided after physical close, got " + string other)
                | other -> fail ("expected ClosingUnknownDispatch after second deadline, got " + string other)
            | other -> fail ("expected Decided on second expiry, got " + string other)
        | other -> fail ("expected ReconcilingUnknownDispatch after first expiry, got " + string other)
    | other -> fail ("expected Decided on first expiry, got " + string other)

let private reconciliationRetryPreservesAbortReason () =
    let runId = RunId.create "run-v40-reconcile-cause"
    let ctx = makeCtx runId
    let plan = makePlan runId

    let cancelCtx =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    match decide (ReconcilingUnknownDispatch(ctx, plan, cancelCtx, 0)) (ReconciliationDeadlineExpired plan.TurnId) with
    | Ok(Decided d) ->
        match d.NextState with
        | ReconcilingUnknownDispatch(_, _, retryContext, retryCount) ->
            equal "retry count is 1 after retry" 1 retryCount
            equal "reconciliation retry preserves abort cause" UserRequested retryContext.Reason
        | other -> fail ("expected reconciliation retry, got " + string other)
    | Ok(NoChange reason) -> fail ("expected decision, got NoChange: " + string reason)
    | Error err -> fail ("unexpected decision error " + string err)

// ── Registry workspace isolation ──

let private noopHost =
    { new ISubsessionHost with
        member _.Dispatch(_, _) =
            Promise.lift (Ok OrderedTurnMarkerObserved)

        member _.Abort(_, _) = Promise.lift AbortUnavailable
        member _.CancelPendingDispatch(_) = ()
        member _.QueryDispatchStatus(_, _) = Promise.lift Unknown
        member _.QuerySessionQuiescence(_, _) = Promise.lift StopUnknown
        member _.ClosePhysicalSession(_) = Promise.lift StopUnknown }

let private actorRegistryWorkspaceIsolation () =
    SubsessionActorRegistry.Clear()
    let sid = SessionId.create "shared-v40"
    let sidStr = SessionId.value sid

    let active =
        ActiveRun
            { RunId = RunId.create "r"
              ParentSessionId = SessionId.create "p" }

    // Set safety projection for ws-a to persistently poisoned
    SubsessionActorRegistry.SetSafetyProjection
        "ws-a"
        (Map.ofList [ sid, PersistentlyPoisoned SessionStateUnknownAfterRestart ])
    // Set safety projection for ws-b to active run
    SubsessionActorRegistry.SetSafetyProjection "ws-b" (Map.ofList [ sid, active ])

    let storeA = MemorySubsessionEventStore()
    let storeB = MemorySubsessionEventStore()

    // Using the proposed GetOrCreate API which takes workspaceRoot
    let actorA = SubsessionActorRegistry.GetOrCreate "ws-a" sidStr noopHost storeA
    let actorB = SubsessionActorRegistry.GetOrCreate "ws-b" sidStr noopHost storeB

    // Assert that the two workspaces obtain DIFFERENT actors for the same session id
    check "workspace A and B get different actors" (not (obj.ReferenceEquals(actorA, actorB)))

    // Assert that workspace A's poison does not pollute workspace B
    check "poison in workspace A does not pollute workspace B" (not actorB.IsPoisoned)

    SubsessionActorRegistry.Clear()

let private routeNoneTurnIdEvidenceAttributedToCurrentTurn () =
    promise {
        let store = MemorySubsessionEventStore()
        let sid = "child-v40-router-none"
        let actor = SubsessionActorRegistry.GetOrCreate "" sid noopHost store
        let runId = RunId.create "run-v40-router-none"

        let request =
            { RunId = runId
              SessionId = SessionId.create sid
              ParentSessionId = SessionId.create "parent-v40"
              Prompt = "go"
              FallbackConfig = cfg
              Directive = RetryChain [ model0 ]
              InitiallyCancelled = false }

        let _ = actor.BeginRun request
        do! sleep 20

        let turnId = TurnId.create (RunId.value runId + "-t0")
        do! actor.Post(DispatchAccepted(turnId, OrderedTurnMarkerObserved))
        do! sleep 20

        match actor.GetState() with
        | Running _ -> ()
        | other -> fail ("expected actor to be in Running state, got " + string other)

        let targetEvidence =
            { CurrentTurnEvidence.empty with
                Assistant = AssistantSnapshot("", 0L, "inspector-report", Some NormalFinish) }

        let obs =
            { TurnId = None
              Evidence = targetEvidence }

        let! routed = Wanxiangshu.Runtime.SubsessionEventRouter.routeToChild "" sid (EvidenceUpdated obs)
        check "should route successfully" routed
        do! sleep 20

        match actor.GetState() with
        | Running(_, _, currentEvidence) ->
            match currentEvidence.Assistant with
            | AssistantSnapshot(_, _, text, _) ->
                equal
                    "EvidenceUpdated with TurnId = None is attributed to current turn and merged"
                    "inspector-report"
                    text
            | NoAssistant ->
                fail "TurnId=None evidence was dropped — host without nonce propagation loses all assistant content"
            | other -> fail ("unexpected assistant evidence: " + string other)
        | other -> fail ("expected actor to remain in Running state, got " + string other)
    }

let run () =
    promise {
        evidenceMergeOutcomePriorityAndSnapshot ()
        runIdUsesFullGuid ()
        issuingAbortBuffersIdle ()
        reconcilingAbortSettleUsesQuiescenceNotDispatchStatus ()
        reconcilingUnknownDeadlineExpiresTwicePoisons ()
        reconciliationRetryPreservesAbortReason ()
        actorRegistryWorkspaceIsolation ()

        do!
            Promise.all
                [ opencodeReceiptWaitsForObservation ()
                  opencodeQueryDispatchStatusRejectedBeforeSend ()
                  opencodeRejectedBeforeSendRejectsLateReceipt ()
                  opencodeQueryDispatchStatusFailedAfterUnknown ()
                  opencodeCancelPendingDispatchKeepsReceiptCorrelation ()
                  opencodeQuiescenceHonestUnknown ()
                  opencodeQuiescenceStillRunning ()
                  opencodeQuiescenceStopped ()
                  ompQuiescenceHonestUnknown ()
                  routeNoneTurnIdEvidenceAttributedToCurrentTurn () ]
            |> Promise.map ignore
    }
