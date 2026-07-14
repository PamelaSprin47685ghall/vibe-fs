module Wanxiangshu.Tests.SubsessionV38DecisionTests

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Decision
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.Fold
open Wanxiangshu.Shell.SubsessionTranscript
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.SubsessionEventWire
open Wanxiangshu.Kernel.EventLog.Types

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

let private sid = SessionId.create "child-v38"
let private parent = SessionId.create "parent-v38"
let private runId = RunId.create "run-v38"
let private turn0 = TurnId.create "run-v38-t0"

let private err: ErrorInput =
    { ErrorName = "Network"
      DomainError = None
      Message = "timeout"
      StatusCode = None
      IsRetryable = Some true }

let private policy0 = initialPolicy cfg [ model0 ]

let private ctx: RunContext =
    { RunId = runId
      ParentSessionId = parent
      SessionId = sid
      Policy = policy0
      FallbackConfig = cfg
      Chain = [ model0 ]
      NextTurnOrdinal = TurnOrdinal.next TurnOrdinal.first }

let private plan: TurnPlan =
    { TurnId = turn0
      Ordinal = TurnOrdinal.first
      Model = model0
      Prompt = "go" }

let private receipt = OrderedTurnMarkerObserved

let private started: StartedTurn = { Plan = plan; StartReceipt = receipt }

let private abortCtxCancelled: AbortContext =
    { Reason = UserRequested
      AfterStop = FinishCancelled }

let private hasEffect pred effects = List.exists pred effects

let private isCancelPendingDispatch =
    function
    | CancelPendingDispatch _ -> true
    | _ -> false

let private isAbortHostSession =
    function
    | AbortHostSession _ -> true
    | _ -> false

let private isCompleteCaller =
    function
    | CompleteCaller _ -> true
    | _ -> false

let private isCompleteCallerCancelled =
    function
    | CompleteCaller(_, Cancelled) -> true
    | _ -> false

// ── 1: Dispatching + CancelRequested → CancellingDispatch (not IssuingAbort) ──
let private dispatchingCancelProducesCancellingDispatch () =
    let state = Dispatching(ctx, plan)

    match decide state CancelRequested with
    | Ok(Decided d) ->
        match d.NextState with
        | CancellingDispatch(ctx', plan', cancelCtx') ->
            equal "ctx preserved" ctx.RunId ctx'.RunId
            equal "plan preserved" plan.TurnId plan'.TurnId
            equal "reason is UserRequested" UserRequested cancelCtx'.Reason
            equal "afterStop is FinishCancelled" FinishCancelled cancelCtx'.AfterStop
        | IssuingAbort _ -> fail "v38: must produce CancellingDispatch, not IssuingAbort"
        | other -> fail ("expected CancellingDispatch, got " + string other)

        check "emits CancelPendingDispatch" (hasEffect isCancelPendingDispatch d.Effects)
        check "no AbortHostSession yet" (not (hasEffect isAbortHostSession d.Effects))
    | other -> fail ("expected Decided, got " + string other)

// ── 2: CancellingDispatch + DispatchAccepted → IssuingAbort with Started turn ──
let private cancellingDispatchAcceptedInitiatesAbort () =
    let cancelCtx: CancelContext =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = CancellingDispatch(ctx, plan, cancelCtx)

    match decide state (DispatchAccepted(turn0, receipt)) with
    | Ok(Decided d) ->
        match d.NextState with
        | IssuingAbort(_, activeTurn, abortCtx) ->
            match activeTurn with
            | Started st ->
                equal "started plan matches" plan.TurnId st.Plan.TurnId
                equal "receipt matches" receipt st.StartReceipt
            | NotYetStarted _ -> fail "must upgrade to Started on DispatchAccepted"

            equal "abort reason UserRequested" UserRequested abortCtx.Reason
            equal "afterStop FinishCancelled" FinishCancelled abortCtx.AfterStop
        | other -> fail ("expected IssuingAbort, got " + string other)

        check "emits AbortHostSession" (hasEffect isAbortHostSession d.Effects)
    | other -> fail ("expected Decided, got " + string other)

// ── 3: CancellingDispatch + DispatchRejected(DefinitelyNotAccepted) → Available + Cancelled ──
let private cancellingDispatchRejectedDefinitelyCancels () =
    let cancelCtx: CancelContext =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = CancellingDispatch(ctx, plan, cancelCtx)

    match decide state (DispatchRejected(turn0, HostRejected err)) with
    | Ok(Decided d) ->
        match d.NextState with
        | Available _ -> check "safely cancelled to Available" true
        | other -> fail ("expected Available, got " + string other)

        check "emits CompleteCaller Cancelled" (hasEffect isCompleteCallerCancelled d.Effects)
        check "no AbortHostSession" (not (hasEffect isAbortHostSession d.Effects))
    | other -> fail ("expected Decided, got " + string other)

// ── 4: IssuingAbort + DispatchAccepted upgrades NotYetStarted → Started ──
let private issuingAbortDispatchAcceptedUpgradesTurn () =
    // v38: IssuingAbort now has 4 fields (ctx, ActiveTurn, AbortContext, idleBuffered)
    let abortCtx =
        { Reason = AcceptanceUnknownAfterDispatch
          AfterStop = RetryAfterSafeStop err }

    let state = IssuingAbort(ctx, NotYetStarted plan, abortCtx)

    match decide state (DispatchAccepted(turn0, receipt)) with
    | Ok(Decided d) ->
        match d.NextState with
        | IssuingAbort(_, activeTurn, _) ->
            match activeTurn with
            | Started st ->
                equal "upgraded plan" plan.TurnId st.Plan.TurnId
                equal "upgraded receipt" receipt st.StartReceipt
            | NotYetStarted _ -> fail "must upgrade ActiveTurn to Started"
        | other -> fail ("expected IssuingAbort with upgraded turn, got " + string other)
    | Ok(NoChange reason) -> fail ("must not ignore DispatchAccepted in IssuingAbort: " + string reason)
    | Error e -> fail ("unexpected error: " + string e)

// ── 5: IssuingAbort + SessionIdleObserved buffers idle ──
let private issuingAbortBuffersIdleThenSettlesOnBarrier () =
    let abortCtx =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = IssuingAbort(ctx, NotYetStarted plan, abortCtx)

    match decide state SessionIdleObserved with
    | Ok(NoChange IdleBeforeAbortBarrier) -> ()
    | other -> fail ("expected NoChange IdleBeforeAbortBarrier, got " + string other)

// ── 6: reconcile persists SessionPoisoned + RunFinished events ──
let private reconcilePersistsPoisonEvents () =
    // v38: reconcile should return events to persist when detecting unfinished state
    let unfinishedState = Dispatching(ctx, plan)

    match reconcile unfinishedState with
    | Some decision ->
        match decision.NextState with
        | Poisoned SessionStateUnknownAfterRestart -> check "poisoned state" true
        | other -> fail ("expected Poisoned, got " + string other)

        let hasSessionPoisoned =
            decision.Events
            |> List.exists (function
                | SessionPoisoned _ -> true
                | _ -> false)

        let hasRunFinished =
            decision.Events
            |> List.exists (function
                | RunFinished _ -> true
                | _ -> false)

        check "reconcile emits SessionPoisoned" hasSessionPoisoned
        check "reconcile emits RunFinished" hasRunFinished
    | None -> fail "reconcile must produce events for unfinished Dispatching state"

// ── 7: CancellingDispatch + DispatchRejected(AcceptanceUnknown) → ReconcilingUnknownDispatch ──
let private cancellingDispatchAcceptanceUnknownReconciles () =
    let cancelCtx: CancelContext =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = CancellingDispatch(ctx, plan, cancelCtx)

    match decide state (DispatchRejected(turn0, HostAcceptanceUnknown err)) with
    | Ok(Decided d) ->
        match d.NextState with
        | ReconcilingUnknownDispatch(ctx', plan', cancelCtx') ->
            equal "ctx preserved" ctx.RunId ctx'.RunId
            equal "plan preserved" plan.TurnId plan'.TurnId
            equal "reason preserved" UserRequested cancelCtx'.Reason
        | other -> fail ("expected ReconcilingUnknownDispatch, got " + string other)

        let hasQueryDispatchStatus =
            d.Effects
            |> List.exists (function
                | QueryDispatchStatus _ -> true
                | _ -> false)

        check "emits QueryDispatchStatus" hasQueryDispatchStatus
        check "no AbortHostSession" (not (hasEffect isAbortHostSession d.Effects))
    | other -> fail ("expected Decided, got " + string other)

// ── 8: ReconcilingUnknownDispatch + DispatchStatusResolved(Unknown) → Poisoned (fail-closed) ──
let private reconcilingDispatchStatusNotConfirmedPoisons () =
    let cancelCtx: CancelContext =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = ReconcilingUnknownDispatch(ctx, plan, cancelCtx)

    match decide state (DispatchStatusResolved Unknown) with
    | Ok(Decided d) ->
        match d.NextState with
        | Poisoned _ -> check "fail-closed: poisoned when cannot confirm" true
        | Available _ -> fail "v38: must NOT cancel when acceptance unknown — must poison"
        | other -> fail ("expected Poisoned, got " + string other)
    | other -> fail ("expected Decided, got " + string other)

// ── 9: ReconcilingUnknownDispatch + DispatchStatusResolved(Accepted receipt) → IssuingAbort ──
let private reconcilingDispatchStatusConfirmedAborts () =
    let cancelCtx: CancelContext =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = ReconcilingUnknownDispatch(ctx, plan, cancelCtx)

    match decide state (DispatchStatusResolved(Accepted receipt)) with
    | Ok(Decided d) ->
        match d.NextState with
        | IssuingAbort(_, activeTurn, abortCtx) ->
            match activeTurn with
            | Started st -> equal "plan matches" plan.TurnId st.Plan.TurnId
            | NotYetStarted _ -> fail "must be Started after confirmed dispatch"

            equal "abort reason" UserRequested abortCtx.Reason
            equal "afterStop" FinishCancelled abortCtx.AfterStop
        | other -> fail ("expected IssuingAbort, got " + string other)

        check "emits AbortHostSession" (hasEffect isAbortHostSession d.Effects)
    | other -> fail ("expected Decided, got " + string other)

// ── Transcript boundary helpers ──

let private mkMsg role (text: string option) : obj =
    let parts: obj =
        match text with
        | Some t -> box [| box (createObj [ "type" ==> "text"; "text" ==> t ]) |]
        | None -> box [||]

    createObj [ "info" ==> createObj [ "role" ==> role ]; "parts" ==> parts; "id" ==> "" ]

let private mkUserMsg () = mkMsg "user" None
let private mkAssistantMsg text = mkMsg "assistant" (Some text)

// ── 10: AnchorByTurnMarkerOnly uses last user message as boundary ──
let private turnEvidenceMarkerOnlyUsesLastUserMessage () =
    let msgs = [| mkUserMsg (); mkAssistantMsg "old output"; mkUserMsg () |]

    match buildTurnEvidence msgs AnchorByTurnMarkerOnly with
    | Ok evidence ->
        match evidence.Assistant with
        | NoAssistant -> check "stale assistant not read" true
        | other -> fail ("expected NoAssistant, got " + string other)
    | Error _ -> fail "expected Ok"

// ── 11: AnchorByHostRunId same boundary behavior ──
let private turnEvidenceHostRunIdUsesLastUserMessage () =
    let mkUserMsgWithRunId rid =
        createObj
            [ "info" ==> createObj [ "role" ==> "user"; "runId" ==> rid ]
              "parts" ==> [||]
              "id" ==> "" ]

    let msgs = [| mkUserMsg (); mkAssistantMsg "old"; mkUserMsgWithRunId "run-x" |]

    match buildTurnEvidence msgs (AnchorByHostRunId "run-x") with
    | Ok evidence ->
        match evidence.Assistant with
        | NoAssistant -> check "stale not read" true
        | other -> fail ("expected NoAssistant, got " + string other)
    | Error _ -> fail "expected Ok"

// ── 12: Empty messages returns NoAssistant ──
let private turnEvidenceEmptyMsgsReturnsNoAssistant () =
    match buildTurnEvidence [||] AnchorByTurnMarkerOnly with
    | Ok evidence -> check "empty ok" (evidence.Assistant = NoAssistant)
    | Error _ -> fail "expected Ok empty"

// ── 13: Current turn has assistant → AssistantWithContent ──
let private turnEvidenceCurrentTurnHasAssistant () =
    let msgs =
        [| mkUserMsg ()
           mkAssistantMsg "old"
           mkUserMsg ()
           mkAssistantMsg "new output" |]

    match buildTurnEvidence msgs AnchorByTurnMarkerOnly with
    | Ok evidence ->
        match evidence.Assistant with
        | AssistantContent(text, _) -> equal "current turn text" "new output" text
        | other -> fail ("expected AssistantContent, got " + string other)
    | Error _ -> fail "expected Ok"

// ── 14: Fold SessionPoisoned persists (not removes) ──
let private foldSessionPoisonedPersists () =
    let sid = SessionId.create "s-fold"
    let rid = RunId.create "r-fold"
    let parent = SessionId.create "p-fold"

    let proj1 =
        projectEvent
            emptyProjection
            (RunStarted
                { RunId = rid
                  ParentSessionId = parent
                  SessionId = sid })

    match Map.tryFind sid proj1 with
    | Some(ActiveRun _) -> check "active run added" true
    | _ -> fail "expected ActiveRun"

    let proj2 =
        projectEvent proj1 (SessionPoisoned(sid, SessionStateUnknownAfterRestart))

    match Map.tryFind sid proj2 with
    | Some(PersistentlyPoisoned _) -> check "poisoned persists in map" true
    | None -> fail "SessionPoisoned must NOT remove from projection"
    | _ -> fail "expected PersistentlyPoisoned"

    let proj3 = projectEvent proj2 (PhysicalSessionClosed sid)

    match Map.tryFind sid proj3 with
    | None -> check "PhysicalSessionClosed removes" true
    | _ -> fail "PhysicalSessionClosed must remove"

// ── 15: RunFinished does not remove PersistentlyPoisoned ──
let private foldRunFinishedDoesNotRemovePoison () =
    let sid = SessionId.create "s-fold2"
    let rid = RunId.create "r-fold2"
    let parent = SessionId.create "p-fold2"

    let proj1 =
        projectEvent
            emptyProjection
            (RunStarted
                { RunId = rid
                  ParentSessionId = parent
                  SessionId = sid })

    let proj2 = projectEvent proj1 (SessionPoisoned(sid, HostProtocolBroken "test"))
    let proj3 = projectEvent proj2 (RunFinished(rid, Cancelled))

    match Map.tryFind sid proj3 with
    | Some(PersistentlyPoisoned _) -> check "poison survives RunFinished" true
    | None -> fail "RunFinished must not remove PersistentlyPoisoned"
    | _ -> fail "expected PersistentlyPoisoned"

let private dispatchingAcceptanceUnknownEntersReconcile () =
    let ctx =
        { RunId = runId
          ParentSessionId = parent
          SessionId = sid
          Policy = policy0
          FallbackConfig = cfg
          Chain = [ model0 ]
          NextTurnOrdinal = TurnOrdinal.next TurnOrdinal.first }

    let plan =
        { TurnId = turn0
          Ordinal = TurnOrdinal.first
          Model = model0
          Prompt = "go" }

    let state = Dispatching(ctx, plan)

    match decide state (DispatchRejected(turn0, HostAcceptanceUnknown err)) with
    | Ok(Decided d) ->
        match d.NextState with
        | ReconcilingUnknownDispatch(ctx', plan', cancelCtx') ->
            equal "ctx preserved" ctx.RunId ctx'.RunId
            equal "plan preserved" plan.TurnId plan'.TurnId
            equal "reason matches" AcceptanceUnknownAfterDispatch cancelCtx'.Reason
        | other -> fail ("expected ReconcilingUnknownDispatch, got " + string other)

        let hasQuery =
            d.Effects
            |> List.exists (function
                | QueryDispatchStatus _ -> true
                | _ -> false)

        check "emits QueryDispatchStatus" hasQuery
    | other -> fail ("expected Decided, got " + string other)

let private issuingAbortIgnoresIdleBoth () =
    let abortCtx =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    // Case A: NotYetStarted plan
    let stateNotStarted = IssuingAbort(ctx, NotYetStarted plan, abortCtx)

    match decide stateNotStarted SessionIdleObserved with
    | Ok(NoChange IdleBeforeAbortBarrier) -> check "NotYetStarted ignores idle before barrier" true
    | other -> fail ("expected NoChange IdleBeforeAbortBarrier, got " + string other)

    // Case B: Started
    let stateStarted = IssuingAbort(ctx, Started started, abortCtx)

    match decide stateStarted SessionIdleObserved with
    | Ok(NoChange IdleBeforeAbortBarrier) -> check "Started ignores idle before barrier" true
    | other -> fail ("expected NoChange IdleBeforeAbortBarrier, got " + string other)

let private runningEvidenceUpdatedMerges () =
    let state = Running(ctx, started, CurrentTurnEvidence.empty)

    let obs =
        { TurnId = turn0
          Evidence =
            { CurrentTurnEvidence.empty with
                Tool = HasToolResult } }

    match decide state (EvidenceUpdated obs) with
    | Ok(Decided d) ->
        match d.NextState with
        | Running(_, _, evidence) -> equal "Tool is merged to HasToolResult" HasToolResult evidence.Tool
        | other -> fail ("expected Running, got " + string other)
    | other -> fail ("expected Decided, got " + string other)

let private decisionReplayAtomicityCheck () =
    // Case 1: Corrupted JSON syntax in events payload
    let e1 =
        { V = 1
          Session = "s-replay"
          Kind = "subsession_decision_committed"
          At = "1"
          Payload = Map [ "events", "{invalid-json}" ] }

    match tryDecodeWanEventBatch e1 with
    | [ SessionPoisoned(_, EventStoreCorrupt _) ] -> check "atomicity poison ok" true
    | other -> fail ("expected atomic poison event, got " + string other)

    // Case 2: Valid JSON but inner event is corrupted (missing fields)
    let e2 =
        { V = 1
          Session = "s-replay"
          Kind = "subsession_decision_committed"
          At = "2"
          Payload = Map [ "events", "[{\"Kind\":\"subsession_run_started\",\"Payload\":{}}]" ] }

    match tryDecodeWanEventBatch e2 with
    | [ SessionPoisoned(_, EventStoreCorrupt _) ] -> check "atomicity poison ok" true
    | other -> fail ("expected atomic poison event, got " + string other)

let private awaitingAbortSettleIdleEntersReconcile () =
    let abortCtx =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = AwaitingAbortSettle(ctx, Started started, abortCtx)

    match decide state SessionIdleObserved with
    | Ok(Decided d) ->
        match d.NextState with
        | ReconcilingAbortSettle _ -> check "transitioned to ReconcilingAbortSettle" true
        | other -> fail ("expected ReconcilingAbortSettle, got " + string other)

        check
            "emits QueryDispatchStatus"
            (hasEffect
                (function
                | QueryDispatchStatus _ -> true
                | _ -> false)
                d.Effects)
    | other -> fail ("expected Decided, got " + string other)

let private reconcilingAbortSettleResolvedAcceptedSucceeds () =
    let abortCtx =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = ReconcilingAbortSettle(ctx, Started started, abortCtx)

    match decide state (DispatchStatusResolved(DispatchStatus.Accepted OrderedTurnMarkerObserved)) with
    | Ok(Decided d) ->
        match d.NextState with
        | Available _ -> check "Available after stop" true
        | other -> fail ("expected Available, got " + string other)

        check
            "CompleteCaller Cancelled"
            (hasEffect
                (function
                | CompleteCaller(_, Cancelled) -> true
                | _ -> false)
                d.Effects)
    | other -> fail ("expected Decided, got " + string other)

let private reconcilingAbortSettleResolvedPendingAwaits () =
    let abortCtx =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = ReconcilingAbortSettle(ctx, Started started, abortCtx)

    match decide state (DispatchStatusResolved DispatchStatus.StillPending) with
    | Ok(Decided d) ->
        match d.NextState with
        | AwaitingAbortSettle _ -> check "returns to AwaitingAbortSettle" true
        | other -> fail ("expected AwaitingAbortSettle, got " + string other)
    | other -> fail ("expected Decided, got " + string other)

let private reconcilingAbortSettleResolvedUnknownPoisons () =
    let abortCtx =
        { Reason = UserRequested
          AfterStop = FinishCancelled }

    let state = ReconcilingAbortSettle(ctx, Started started, abortCtx)

    match decide state (DispatchStatusResolved DispatchStatus.Unknown) with
    | Ok(Decided d) ->
        match d.NextState with
        | Poisoned _ -> check "poisoned on Unknown" true
        | other -> fail ("expected Poisoned, got " + string other)
    | other -> fail ("expected Decided, got " + string other)

let private ompOrderedBarrierTimingPreserved () =
    let state0 = Dispatching(ctx, plan)

    match decide state0 (DispatchAccepted(turn0, receipt)) with
    | Ok(Decided d1) ->
        match d1.NextState with
        | Running _ ->
            let evidence =
                { CurrentTurnEvidence.empty with
                    Assistant = AssistantContent("success-output", Some NormalFinish) }

            match decide d1.NextState (EvidenceUpdated { TurnId = turn0; Evidence = evidence }) with
            | Ok(Decided d2) ->
                match d2.NextState with
                | Running _ ->
                    match decide d2.NextState SessionIdleObserved with
                    | Ok(Decided d3) ->
                        match d3.NextState with
                        | Available _ -> check "Natural complete under normal order timing" true
                        | other -> fail ("expected Available, got " + string other)
                    | other -> fail ("expected Decided on idle, got " + string other)
                | other -> fail ("expected Running after evidence merge, got " + string other)
            | other -> fail ("expected Decided on EvidenceUpdated, got " + string other)
        | other -> fail ("expected Running, got " + string other)
    | other -> fail ("expected Decided on DispatchAccepted, got " + string other)

let run () =
    dispatchingCancelProducesCancellingDispatch ()
    cancellingDispatchAcceptedInitiatesAbort ()
    cancellingDispatchRejectedDefinitelyCancels ()
    issuingAbortDispatchAcceptedUpgradesTurn ()
    issuingAbortBuffersIdleThenSettlesOnBarrier ()
    reconcilePersistsPoisonEvents ()
    cancellingDispatchAcceptanceUnknownReconciles ()
    reconcilingDispatchStatusNotConfirmedPoisons ()
    reconcilingDispatchStatusConfirmedAborts ()
    turnEvidenceMarkerOnlyUsesLastUserMessage ()
    turnEvidenceHostRunIdUsesLastUserMessage ()
    turnEvidenceEmptyMsgsReturnsNoAssistant ()
    turnEvidenceCurrentTurnHasAssistant ()
    foldSessionPoisonedPersists ()
    foldRunFinishedDoesNotRemovePoison ()
    dispatchingAcceptanceUnknownEntersReconcile ()
    issuingAbortIgnoresIdleBoth ()
    runningEvidenceUpdatedMerges ()
    decisionReplayAtomicityCheck ()
    awaitingAbortSettleIdleEntersReconcile ()
    reconcilingAbortSettleResolvedAcceptedSucceeds ()
    reconcilingAbortSettleResolvedPendingAwaits ()
    reconcilingAbortSettleResolvedUnknownPoisons ()
    ompOrderedBarrierTimingPreserved ()
