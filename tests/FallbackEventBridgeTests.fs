module Wanxiangshu.Tests.FallbackEventBridgeTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure

open Wanxiangshu.Runtime.Fallback.Coordinator
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.ContinuationExecution
open Wanxiangshu.Runtime.Fallback.ContinuationExecutionCore
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchOps
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.LeaseValidation
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Runtime

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Tests.FallbackEventBridgeStateTests
open Wanxiangshu.Tests.FallbackEventBridgeReviewInProgressTests


type FakeExecutor(?messages: obj array) =
    let mutable continueCalls: ResizeArray<string * FallbackModel> = ResizeArray()

    let mutable recoverCalls: ResizeArray<string * FallbackModel * string> =
        ResizeArray()

    let mutable propagateCalls: ResizeArray<string> = ResizeArray()
    let msgs = defaultArg messages [||]

    interface IActionExecutor with
        member _.SendContinue(sessionID, model, continuationID) : JS.Promise<unit> =
            continueCalls.Add(sessionID, model)
            Promise.lift ()

        member _.RecoverWithPrompt(sessionID, model, promptText, continuationID) : JS.Promise<unit> =
            recoverCalls.Add(sessionID, model, promptText)
            Promise.lift ()

        member _.FetchMessages(_sessionID: string) : JS.Promise<obj array> = Promise.lift msgs

        member _.PropagateFailure(sessionID: string) : JS.Promise<unit> =
            propagateCalls.Add(sessionID)
            Promise.lift ()

        member _.CaptureCurrentModel(_sessionID: string) : JS.Promise<FallbackModel option> = Promise.lift None

        member _.AbortRun(_sessionID: string) : JS.Promise<unit> = Promise.lift ()

    member _.ContinueCalls = continueCalls |> Seq.toList
    member _.RecoverCalls = recoverCalls |> Seq.toList
    member _.PropagateCalls = propagateCalls |> Seq.toList

type FakeTranslator(sessionID: string, evt: FallbackEvent) =
    let _sid = sessionID
    let _ev = evt

    interface IEventTranslator with
        member _.TranslateError(_raw: obj) : FallbackEvent option =
            match _ev with
            | FallbackEvent.SessionError _ -> Some _ev
            | _ -> None

        member _.ExtractSessionID(_raw: obj) : string = _sid

        member _.IsSessionError(_raw: obj) : bool =
            match _ev with
            | FallbackEvent.SessionError _ -> true
            | _ -> false

        member _.IsSessionIdle(_raw: obj) : bool =
            match _ev with
            | FallbackEvent.SessionIdle -> true
            | _ -> false

        member _.IsSessionBusy(_raw: obj) : bool =
            match _ev with
            | FallbackEvent.SessionBusy -> true
            | _ -> false

        member _.IsNewUserMessage(_sid, _rawEvent: obj) : bool =
            match _ev with
            | FallbackEvent.NewUserMessage -> true
            | _ -> false

        member _.ExtractNewUserMessageId(_rawEvent) = None

        member _.ExtractRoutingContext(_rawEvent: obj) = None, None

        member _.IsAssistantMessage(rawEvent: obj) =
            let props = Dyn.get rawEvent "properties"

            if Dyn.isNullish props then
                false
            else
                let info = Dyn.get props "info"
                not (Dyn.isNullish info) && Dyn.str info "role" = "assistant"

        member _.ExtractAssistantMessageId(rawEvent: obj) =
            let props = Dyn.get rawEvent "properties"

            if Dyn.isNullish props then
                None
            else
                let info = Dyn.get props "info"
                let info = if Dyn.isNullish info then props else info
                let id = Dyn.str info "id"
                if id <> "" then Some id else None

        member _.ExtractAssistantParentId(rawEvent: obj) =
            let props = Dyn.get rawEvent "properties"

            if Dyn.isNullish props then
                None
            else
                let info = Dyn.get props "info"
                let info = if Dyn.isNullish info then props else info
                let pid = Dyn.str info "parentID"
                let pid = if pid <> "" then pid else Dyn.str info "parentId"
                if pid <> "" then Some pid else None

        member _.ExtractContinuationIdentity(rawEvent: obj) =
            let props = Dyn.get rawEvent "properties"
            let props = if Dyn.isNullish props then rawEvent else props
            let cid = Dyn.str props "continuationId"
            let cid = if cid <> "" then cid else Dyn.str props "continuationID"
            let cid = if cid <> "" then cid else Dyn.str rawEvent "continuationId"
            let cid = if cid <> "" then cid else Dyn.str rawEvent "continuationID"
            let o = Dyn.get props "continuationOrdinal"

            let o =
                if Dyn.isNullish o then
                    Dyn.get rawEvent "continuationOrdinal"
                else
                    o

            let ord = getOrdinal o
            if cid <> "" then Some(cid, ord) else None

        member _.ExtractHostRunId(rawEvent: obj) =
            let props = Dyn.get rawEvent "properties"
            let props = if Dyn.isNullish props then rawEvent else props
            let tid = Dyn.str props "turnId"
            let tid = if tid <> "" then tid else Dyn.str props "turnID"
            let tid = if tid <> "" then tid else Dyn.str props "runId"
            let tid = if tid <> "" then tid else Dyn.str props "runID"
            if tid <> "" then Some tid else None

        member _.ExtractTurnObservation(rawEvent: obj) : TurnObservation option = None


let mkModel (pid: string) (mid: string) : FallbackModel =
    { ProviderID = pid
      ModelID = mid
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let mkRetryableErr () : ErrorInput =
    { ErrorName = "err"
      DomainError = Some(UnknownJsError "fail")
      Message = "fail"
      StatusCode = None
      IsRetryable = Some true }

let mkAbortErr () : ErrorInput =
    { ErrorName = "MessageAbortedError"
      DomainError = Some MessageAborted
      Message = "abort"
      StatusCode = None
      IsRetryable = None }

let mkConfig () : FallbackConfig =
    { DefaultChain = []
      AgentChains = Map.empty
      MaxRetries = 2
      LoopMaxContinues = 3
      MaxRecoveries = 5 }

let defaultCfgLookup (_agent: string) : FallbackConfig = mkConfig ()

type SwitchingTranslator(sessionID: string) =
    let errorTranslator =
        FakeTranslator(sessionID, FallbackEvent.SessionError(mkRetryableErr ())) :> IEventTranslator

    let humanTranslator = FakeTranslator(sessionID, FallbackEvent.NewUserMessage) :> IEventTranslator
    let select rawEvent = if Dyn.str rawEvent "kind" = "human" then humanTranslator else errorTranslator

    interface IEventTranslator with
        member _.TranslateError(rawEvent) = (select rawEvent).TranslateError rawEvent
        member _.ExtractSessionID(rawEvent) = (select rawEvent).ExtractSessionID rawEvent
        member _.IsSessionError(rawEvent) = (select rawEvent).IsSessionError rawEvent
        member _.IsSessionIdle(rawEvent) = (select rawEvent).IsSessionIdle rawEvent
        member _.IsSessionBusy(rawEvent) = (select rawEvent).IsSessionBusy rawEvent

        member _.IsNewUserMessage(sessionID, rawEvent) =
            (select rawEvent).IsNewUserMessage(sessionID, rawEvent)

        member _.ExtractNewUserMessageId(rawEvent) = (select rawEvent).ExtractNewUserMessageId rawEvent
        member _.ExtractRoutingContext(rawEvent) = (select rawEvent).ExtractRoutingContext rawEvent
        member _.IsAssistantMessage(rawEvent) = (select rawEvent).IsAssistantMessage rawEvent
        member _.ExtractAssistantMessageId(rawEvent) = (select rawEvent).ExtractAssistantMessageId rawEvent
        member _.ExtractAssistantParentId(rawEvent) = (select rawEvent).ExtractAssistantParentId rawEvent
        member _.ExtractContinuationIdentity(rawEvent) = (select rawEvent).ExtractContinuationIdentity rawEvent
        member _.ExtractHostRunId(rawEvent) = (select rawEvent).ExtractHostRunId rawEvent
        member _.ExtractTurnObservation(rawEvent) = (select rawEvent).ExtractTurnObservation rawEvent

type BlockingExecutor() =
    let mutable resolveStarted = fun () -> ()
    let started = Promise.create (fun resolve _ -> resolveStarted <- resolve)
    let mutable resolveDispatch = fun () -> ()
    let dispatchCompletion = Promise.create (fun resolve _ -> resolveDispatch <- resolve)
    let mutable didStart = false

    interface IActionExecutor with
        member _.SendContinue(_sessionID, _model, _continuationID) =
            if not didStart then
                didStart <- true
                resolveStarted ()

            dispatchCompletion

        member _.RecoverWithPrompt(_sessionID, _model, _promptText, _continuationID) = dispatchCompletion
        member _.FetchMessages _ = Promise.lift [||]
        member _.PropagateFailure _ = Promise.lift ()
        member _.CaptureCurrentModel _ = Promise.lift None
        member _.AbortRun _ = Promise.lift ()

    member _.Started = started
    member _.CompleteDispatch() = resolveDispatch ()


let handleEvent_retrySame_consumedAndSendContinue () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let chain = [ model ]
        let cfg = mkConfig ()
        let rt = FallbackRuntimeStore()
        let sid = "sess-1"
        rt.UpdateSession(sid, selectChain chain)
        rt.UpdateSession(sid, recordAgentName "reviewer")

        let translator =
            FakeTranslator(sid, FallbackEvent.SessionError(mkRetryableErr ())) :> IEventTranslator

        let executor = FakeExecutor()

        let! result, intentOpt = handleEvent translator rt defaultCfgLookup executor "" (box ()) None

        match intentOpt with
        | Some intent -> do! executeContinuationIntent rt executor "" sid intent inlineReenter
        | None -> ()

        equal "consumed" true result.Consumed
        equal "phase Retrying 1" (FallbackPhase.Retrying 1) result.State.Phase
        equal "continueCount 1" 1 result.State.ContinueCount
        equal "executor called once" 1 (executor.ContinueCalls.Length)
    }

let handleEvent_exhausted_notConsumed () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let chain = [ model ]
        let rt = FallbackRuntimeStore()
        let sid = "sess-1"
        rt.UpdateSession(sid, selectChain chain)
        rt.UpdateSession(sid, recordAgentName "reviewer")

        let s0 = rt.GetOrCreateState sid

        rt.Update(
            sid,
            setCore
                { s0 with
                    Phase = FallbackPhase.Exhausted }
        )

        let translator =
            FakeTranslator(sid, FallbackEvent.SessionError(mkRetryableErr ())) :> IEventTranslator

        let executor = FakeExecutor()

        let! result, _ = handleEvent translator rt defaultCfgLookup executor "" (box ()) None

        equal "not consumed when exhausted" false result.Consumed
    }

let handleEvent_noChain_notConsumed () =
    promise {
        let rt = FallbackRuntimeStore()
        let sid = "sess-1"
        rt.UpdateSession(sid, recordAgentName "reviewer")

        let translator =
            FakeTranslator(sid, FallbackEvent.SessionError(mkRetryableErr ())) :> IEventTranslator

        let executor = FakeExecutor()

        let! result, _ = handleEvent translator rt defaultCfgLookup executor "" (box ()) None

        equal "no chain → not consumed" false result.Consumed
    }

let handleEvent_sessionAborted_setsCancelled () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let chain = [ model ]
        let cfg = mkConfig ()
        let rt = FallbackRuntimeStore()
        let sid = "sess-1"
        rt.UpdateSession(sid, selectChain chain)
        rt.UpdateSession(sid, recordAgentName "reviewer")

        let translator =
            FakeTranslator(sid, FallbackEvent.SessionError(mkAbortErr ())) :> IEventTranslator

        let executor = FakeExecutor()

        let! result, _ = handleEvent translator rt defaultCfgLookup executor "" (box ()) None

        equal "consumed" true result.Consumed
        equal "lifecycle Cancelled" FallbackLifecycle.Cancelled result.State.Lifecycle
    }

let handleEvent_newUserMessage_resetsState () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let chain = [ model ]
        let cfg = mkConfig ()
        let rt = FallbackRuntimeStore()
        let sid = "sess-1"
        rt.UpdateSession(sid, selectChain chain)
        rt.UpdateSession(sid, recordAgentName "reviewer")

        let s0 = rt.GetOrCreateState sid

        let updated =
            { s0 with
                Phase = FallbackPhase.Retrying 3
                ContinueCount = 3
                FailureCount = 5 }

        rt.Update(sid, setCore updated)

        let translator =
            FakeTranslator(sid, FallbackEvent.NewUserMessage) :> IEventTranslator

        let executor = FakeExecutor()

        let! result, _ = handleEvent translator rt defaultCfgLookup executor "" (box ()) None

        equal "consumed" false result.Consumed
        equal "phase Idle" FallbackPhase.Idle result.State.Phase
        equal "continueCount 0" 0 result.State.ContinueCount
        equal "failureCount 0" 0 result.State.FailureCount
        equal "lifecycle Active" FallbackLifecycle.Active result.State.Lifecycle
    }

let createHandler_returnsCallable () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let chain = [ model ]
        let cfg = mkConfig ()
        let rt = FallbackRuntimeStore()
        let sid = "sess-1"
        rt.UpdateSession(sid, selectChain chain)
        rt.UpdateSession(sid, recordAgentName "reviewer")

        let translator =
            FakeTranslator(sid, FallbackEvent.SessionError(mkRetryableErr ())) :> IEventTranslator

        let executor = FakeExecutor()

        let handler = createHandler translator rt defaultCfgLookup executor "" None
        check "handler is non-null" (not (isNull (box handler)))
    }

let createHandler_twoSessionsIndependent () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let chain = [ model ]
        let cfg = mkConfig ()

        let rt1 = FallbackRuntimeStore()
        let rt2 = FallbackRuntimeStore()
        let sid1 = "sess-1"
        let sid2 = "sess-2"
        rt1.UpdateSession(sid1, selectChain chain)
        rt1.UpdateSession(sid1, recordAgentName "r1")
        rt2.UpdateSession(sid2, selectChain chain)
        rt2.UpdateSession(sid2, recordAgentName "r2")

        let tr1 =
            FakeTranslator(sid1, FallbackEvent.SessionError(mkRetryableErr ())) :> IEventTranslator

        let tr2 =
            FakeTranslator(sid2, FallbackEvent.SessionError(mkRetryableErr ())) :> IEventTranslator

        let ex1 = FakeExecutor()
        let ex2 = FakeExecutor()

        let h1 = createHandler tr1 rt1 defaultCfgLookup ex1 "" None
        let h2 = createHandler tr2 rt2 defaultCfgLookup ex2 "" None

        check "handlers non-null" (not (isNull (box h1)) && not (isNull (box h2)))

        let! r1 = h1 (box ())
        let! r2 = h2 (box ())
        equal "sess-1 consumed" true r1.Consumed
        equal "sess-2 consumed" true r2.Consumed
    }

let handleEvent_humanPreemptsIntent_executorNotCalled () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let rt = FallbackRuntimeStore()
        let sid = "sess-preempt"
        rt.UpdateSession(sid, selectChain [ model ])
        rt.UpdateSession(sid, recordAgentName "reviewer")

        // Setup active continuation lease
        let turnId = rt.UpdateSessionReturning(sid, advanceHumanTurn)
        let gen = (rt.GetSession sid).SessionGeneration
        let cancelGen = (rt.GetSession sid).CancelGeneration
        rt.UpdateSession(sid, setActiveContinuationGeneration gen)
        rt.UpdateSession(sid, setActiveContinuationCancelGeneration cancelGen)
        let continuationID = "cont-preempt"

        let intent =
            SendContinueIntent(model, "reviewer", turnId, gen, cancelGen, continuationID, 1)

        let executor = FakeExecutor()

        // Human message arrives in the meantime (e.g. before the intent execution is complete, but after it was returned/prepared)
        let tr = FakeTranslator(sid, FallbackEvent.NewUserMessage) :> IEventTranslator
        let handler = createHandler tr rt defaultCfgLookup executor "" None

        // Call the handler with the human message. This transitions state to Idle/Active, increments cancel gen/turn
        let! res = handler (box ())
        equal "consumed is false for human message" false res.Consumed

        // Execute the prepared intent
        do! executeContinuationIntent rt executor "" sid intent inlineReenter

        equal "executor is not called" 0 executor.ContinueCalls.Length
    }

let handleEvent_intentDelayed_leaseExpires_executorNotCalled () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let rt = FallbackRuntimeStore()
        let sid = "sess-delayed"
        rt.UpdateSession(sid, selectChain [ model ])
        rt.UpdateSession(sid, recordAgentName "reviewer")

        // Setup active continuation lease
        let turnId = rt.UpdateSessionReturning(sid, advanceHumanTurn)
        let gen = (rt.GetSession sid).SessionGeneration
        let cancelGen = (rt.GetSession sid).CancelGeneration
        rt.UpdateSession(sid, setActiveContinuationGeneration gen)
        rt.UpdateSession(sid, setActiveContinuationCancelGeneration cancelGen)
        let continuationID = "cont-delayed"

        let intent =
            SendContinueIntent(model, "reviewer", turnId, gen, cancelGen, continuationID, 1)

        let executor = FakeExecutor()

        // Start continuation execution. It will enqueue in the governor and defer to the event loop.
        let p = executeContinuationIntent rt executor "" sid intent inlineReenter

        // Immediately invalidate the lease by advancing cancel generation
        rt.UpdateSession(sid, fun s -> setCancelGeneration (cancelGen + 1) s)

        // Await the continuation promise
        do! p

        // Verify that executor is not called
        equal "executor is not called" 0 executor.ContinueCalls.Length
    }

let createHandler_transportDoesNotBlockHumanCancellation () =
    promise {
        let! workspaceRoot = mkdtempAsync "fallback-actor-effect-"
        let model = mkModel "actor-provider" "actor-model"
        let runtime = FallbackRuntimeStore()
        let sessionID = "actor-effect-session"
        runtime.UpdateSession(sessionID, selectChain [ model ])
        runtime.UpdateSession(sessionID, recordAgentName "reviewer")

        let translator = SwitchingTranslator(sessionID) :> IEventTranslator
        let executor = BlockingExecutor()
        let handler = createHandler translator runtime defaultCfgLookup executor workspaceRoot None
        let firstHook = handler (createObj [ "kind", box "error" ])
        let! firstResult = withTimeout 1000 firstHook

        match firstResult with
        | None ->
            executor.CompleteDispatch()
            do! rmAsync workspaceRoot
            failwith "fallback hook remained blocked by physical transport"
        | Some result -> equal "retry event consumed" true result.Consumed

        let! transportStarted = withTimeout 1000 executor.Started

        match transportStarted with
        | None ->
            executor.CompleteDispatch()
            do! rmAsync workspaceRoot
            failwith "fallback transport did not start"
        | Some() -> ()

        let humanHook = handler (createObj [ "kind", box "human" ])
        let! humanResult = withTimeout 1000 humanHook

        match humanResult with
        | None ->
            executor.CompleteDispatch()
            do! rmAsync workspaceRoot
            failwith "human turn could not enter fallback actor while transport was pending"
        | Some result -> equal "human event is not consumed" false result.Consumed

        let session = runtime.GetSession sessionID
        equal "human event clears pending continuation" None session.PendingLease
        equal "human event transfers ownership" SessionOwner.Human session.Owner

        executor.CompleteDispatch()
        do! Promise.sleep 0
        do! rmAsync workspaceRoot
    }

/// F-01: after SendContinue is decided, concurrent NewUserMessage on the same
/// session must prevent the physical prompt. Deterministic via gated executor.
type GatedCountingExecutor() =
    let mutable resolveGate = fun () -> ()
    let gate = Promise.create (fun resolve _ -> resolveGate <- resolve)
    let mutable continueCount = 0
    let mutable startedCount = 0

    interface IActionExecutor with
        member _.SendContinue(_sessionID, _model, _continuationID) =
            promise {
                startedCount <- startedCount + 1
                do! gate
                continueCount <- continueCount + 1
            }

        member _.RecoverWithPrompt(_sessionID, _model, _promptText, _continuationID) = Promise.lift ()
        member _.FetchMessages _ = Promise.lift [||]
        member _.PropagateFailure _ = Promise.lift ()
        member _.CaptureCurrentModel _ = Promise.lift None
        member _.AbortRun _ = Promise.lift ()

    member _.ContinueCount = continueCount
    member _.StartedCount = startedCount
    member _.Release() = resolveGate ()

let createHandler_humanCancelPreventsPhysicalSendContinue () =
    promise {
        let! workspaceRoot = mkdtempAsync "fallback-f01-queue-"
        let model = mkModel "f01-provider" "f01-model"
        let runtime = FallbackRuntimeStore()
        let sessionID = "f01-queue-session"
        runtime.UpdateSession(sessionID, selectChain [ model ])
        runtime.UpdateSession(sessionID, recordAgentName "reviewer")

        let translator = SwitchingTranslator(sessionID) :> IEventTranslator
        let executor = GatedCountingExecutor()
        let handler = createHandler translator runtime defaultCfgLookup executor workspaceRoot None

        // Decision path: SessionError → SendContinue intent + lease Requested.
        let! errorResult = handler (createObj [ "kind", box "error" ])
        equal "retry event consumed" true errorResult.Consumed

        // Lease must exist after decision (DispatchRequested persisted as PendingLease).
        let leaseAfterDecision = (runtime.GetSession sessionID).PendingLease
        check "pending lease after decision" leaseAfterDecision.IsSome

        // Concurrent human cancel on the same session queue before effect claim/send.
        let! humanResult = handler (createObj [ "kind", box "human" ])
        equal "human event is not consumed" false humanResult.Consumed

        let sessionAfterHuman = runtime.GetSession sessionID
        equal "human clears pending lease" None sessionAfterHuman.PendingLease
        equal "human owns session" SessionOwner.Human sessionAfterHuman.Owner

        // Release any transport that might have raced past cancel; it must still
        // not count as a successful physical prompt after ownership change.
        executor.Release()
        do! Promise.sleep 20

        equal "physical SendContinue never ran" 0 executor.ContinueCount
        // Effect may have observed cancel before claim (started=0) or after
        // claim but before complete; either way ContinueCount stays 0 only if
        // we gate after entry. Require that post-cancel release does not produce
        // a counted continue when lease was cleared before claim.
        check
            "no late physical prompt after cancel"
            (executor.ContinueCount = 0
             && sessionAfterHuman.PendingLease.IsNone)

        // Stale effect must not re-create lease or flip ownership.
        let sessionFinal = runtime.GetSession sessionID
        equal "stale effect does not restore lease" None sessionFinal.PendingLease
        equal "stale effect does not steal ownership" SessionOwner.Human sessionFinal.Owner

        do! rmAsync workspaceRoot
    }

let run () =
    promise {
        do! handleEvent_retrySame_consumedAndSendContinue ()
        do! handleEvent_exhausted_notConsumed ()
        do! handleEvent_noChain_notConsumed ()
        do! handleEvent_sessionAborted_setsCancelled ()
        do! handleEvent_newUserMessage_resetsState ()
        do! createHandler_returnsCallable ()
        do! createHandler_twoSessionsIndependent ()
        do! handleEvent_humanPreemptsIntent_executorNotCalled ()
        do! handleEvent_intentDelayed_leaseExpires_executorNotCalled ()
        do! createHandler_transportDoesNotBlockHumanCancellation ()
        do! createHandler_humanCancelPreventsPhysicalSendContinue ()
        do! FallbackEventBridgeStateTests.run ()
        do! FallbackEventBridgeReviewInProgressTests.run ()
    }
