module Wanxiangshu.Tests.SubsessionV40HardTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Decision
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.Fold
open Wanxiangshu.Shell.SubsessionActor
open Wanxiangshu.Shell.SubsessionActorRegistry
open Wanxiangshu.Shell.SubsessionEventStore
open Wanxiangshu.Tests.Assert

module OpencodeHost = Wanxiangshu.Opencode.SubsessionHostAdapter
module OmpHost = Wanxiangshu.Omp.SubsessionHostAdapter

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
      MaxRecoveries = 3 }

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
        OpencodeHost.PendingTurnReceipt.tryResolve (TurnId.value turnId) (UserMessageObserved "msg-1")
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

let private opencodeQuiescenceHonestUnknown () =
    promise {
        let host = OpencodeHost.createHost (createEmpty ()) "" ""
        let! status = host.QuerySessionQuiescence(SessionId.create "s", TurnId.create "t")
        equal "OpenCode quiescence is StopUnknown" StopUnknown status
    }

let private ompQuiescenceHonestUnknown () =
    promise {
        let host = OmpHost.createHost (createEmpty ()) "" (createEmpty ())
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

    let state = ReconcilingUnknownDispatch(ctx, plan, cancelCtx)

    match decide state (ReconciliationDeadlineExpired plan.TurnId) with
    | Ok(Decided d1) ->
        match d1.NextState with
        | ReconcilingUnknownDispatch _ ->
            match decide d1.NextState (ReconciliationDeadlineExpired plan.TurnId) with
            | Ok(Decided d2) ->
                match d2.NextState with
                | Poisoned _ -> check "two reconciliation deadlines poison" true
                | other -> fail ("expected Poisoned after second deadline, got " + string other)
            | other -> fail ("expected Decided on second expiry, got " + string other)
        | other -> fail ("expected ReconcilingUnknownDispatch after first expiry, got " + string other)
    | other -> fail ("expected Decided on first expiry, got " + string other)

// ── Registry workspace isolation ──

let private noopHost =
    { new ISubsessionHost with
        member _.Dispatch(_, _) =
            Promise.lift (Ok OrderedTurnMarkerObserved)

        member _.Abort(_, _) = Promise.lift AbortUnavailable
        member _.CancelPendingDispatch(_) = ()
        member _.QueryDispatchStatus(_, _) = Promise.lift Unknown
        member _.QuerySessionQuiescence(_, _) = Promise.lift StopUnknown }

let private actorRegistryWorkspaceIsolation () =
    SubsessionActorRegistry.Clear()
    let sid = SessionId.create "shared-v40"

    let active =
        ActiveRun
            { RunId = RunId.create "r"
              ParentSessionId = SessionId.create "p" }

    SubsessionActorRegistry.SetSafetyProjection "ws-a" (Map.ofList [ sid, active ])

    SubsessionActorRegistry.SetSafetyProjection
        "ws-b"
        (Map.ofList [ sid, PersistentlyPoisoned SessionStateUnknownAfterRestart ])

    let store = MemorySubsessionEventStore()
    let actor = SubsessionActorRegistry.GetOrCreate (SessionId.value sid) noopHost store
    check "poison from any workspace is visible to GetOrCreate" actor.IsPoisoned

    SubsessionActorRegistry.ClearPoison(SessionId.value sid)

    let actor2 =
        SubsessionActorRegistry.GetOrCreate (SessionId.value sid) noopHost store

    check "ClearPoison removes safety entry from all workspaces" (not actor2.IsPoisoned)
    SubsessionActorRegistry.Clear()

let run () =
    promise {
        evidenceMergeOutcomePriorityAndSnapshot ()
        issuingAbortBuffersIdle ()
        reconcilingAbortSettleUsesQuiescenceNotDispatchStatus ()
        reconcilingUnknownDeadlineExpiresTwicePoisons ()
        actorRegistryWorkspaceIsolation ()

        do!
            Promise.all
                [ opencodeReceiptWaitsForObservation ()
                  opencodeQueryDispatchStatusRejectedBeforeSend ()
                  opencodeQueryDispatchStatusFailedAfterUnknown ()
                  opencodeQuiescenceHonestUnknown ()
                  ompQuiescenceHonestUnknown () ]
            |> Promise.map ignore
    }
