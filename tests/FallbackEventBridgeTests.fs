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
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Runtime.Fallback.FallbackEventBridge
open Wanxiangshu.Runtime.Fallback.FallbackBridgePorts
open Wanxiangshu.Runtime.Fallback.FallbackBridgeContinuation
open Wanxiangshu.Runtime.Fallback.FallbackBridgeLease
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Tests.FallbackEventBridgeTestsPart2
open Wanxiangshu.Tests.FallbackEventBridgeTestsPendingReview


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
      MaxRecoveries = 5
      LegacyZeroWidthContinue = false }

let defaultCfgLookup (_agent: string) : FallbackConfig = mkConfig ()


let legacyCfgLookup (_agent: string) : FallbackConfig =
    { mkConfig () with
        LegacyZeroWidthContinue = true }

let handleEvent_retrySame_legacyContinueTrue_executesSendContinue () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let chain = [ model ]
        let rt = FallbackRuntimeStore()
        let sid = "sess-1"
        rt.SetChain sid chain
        rt.SetAgentName sid "reviewer"

        let translator =
            FakeTranslator(sid, FallbackEvent.SessionError(mkRetryableErr ())) :> IEventTranslator

        let executor = FakeExecutor()

        let! result, intentOpt = handleEvent translator rt legacyCfgLookup executor "" (box ()) None

        match intentOpt with
        | Some intent -> do! executeContinuationIntent rt executor "" sid intent
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
        rt.SetChain sid chain
        rt.SetAgentName sid "reviewer"

        let s0 = rt.GetOrCreateState sid

        rt.UpdateState
            sid
            { s0 with
                Phase = FallbackPhase.Exhausted }

        let translator =
            FakeTranslator(sid, FallbackEvent.SessionError(mkRetryableErr ())) :> IEventTranslator

        let executor = FakeExecutor()

        let! result, _ = handleEvent translator rt defaultCfgLookup executor "" (box ()) None

        equal "not consumed when exhausted" false result.Consumed
    }

let handleEvent_retrySame_legacyContinueFalse_gatesSendContinue () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let chain = [ model ]
        let cfg = mkConfig ()
        let rt = FallbackRuntimeStore()
        let sid = "sess-1"
        rt.SetChain sid chain
        rt.SetAgentName sid "reviewer"

        let translator =
            FakeTranslator(sid, FallbackEvent.SessionError(mkRetryableErr ())) :> IEventTranslator

        let executor = FakeExecutor()

        let! result, _ = handleEvent translator rt defaultCfgLookup executor "" (box ()) None

        equal "consumed" true result.Consumed
        equal "phase Retrying 1" (FallbackPhase.Retrying 1) result.State.Phase
        equal "continueCount 1" 1 result.State.ContinueCount
        equal "executor not called" 0 (executor.ContinueCalls.Length)
    }

let checkContinuationMatches_emptyContinuationId_matchesActiveLease () =
    let rt = FallbackRuntimeStore()
    let sid = "sess-empty-cont"
    let model = mkModel "oai" "gpt-5"
    let turnId = rt.IncrementHumanTurnId sid
    let gen = rt.GetSessionGeneration sid
    let cancelGen = rt.GetCancelGeneration sid
    rt.SetActiveContinuationGeneration sid gen
    rt.SetActiveContinuationCancelGeneration sid cancelGen

    let lease =
        { ContinuationID = "lease-1"
          ContinuationOrdinal = 1
          SessionGeneration = gen
          HumanTurnID = turnId
          CancelGeneration = cancelGen
          Owner = SessionOwner.Fallback
          Model = model
          PromptText = None
          Status = LeaseStatus.Requested }

    rt.SetPendingLease(sid, lease)

    let isMatched, isEventMatch = checkContinuationMatches rt sid ""
    equal "empty continuationId matches active lease" true isMatched
    equal "empty continuationId is event match" true isEventMatch

let handleEvent_noChain_notConsumed () =
    promise {
        let rt = FallbackRuntimeStore()
        let sid = "sess-1"
        rt.SetAgentName sid "reviewer"

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
        rt.SetChain sid chain
        rt.SetAgentName sid "reviewer"

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
        rt.SetChain sid chain
        rt.SetAgentName sid "reviewer"

        let s0 = rt.GetOrCreateState sid

        rt.UpdateState
            sid
            { s0 with
                Phase = FallbackPhase.Retrying 3
                ContinueCount = 3
                FailureCount = 5 }

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
        rt.SetChain sid chain
        rt.SetAgentName sid "reviewer"

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
        rt1.SetChain sid1 chain
        rt1.SetAgentName sid1 "r1"
        rt2.SetChain sid2 chain
        rt2.SetAgentName sid2 "r2"

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


let run () =
    promise {
        do! handleEvent_retrySame_legacyContinueTrue_executesSendContinue ()
        do! handleEvent_retrySame_legacyContinueFalse_gatesSendContinue ()
        do! Promise.lift (checkContinuationMatches_emptyContinuationId_matchesActiveLease ())
        do! handleEvent_exhausted_notConsumed ()
        do! handleEvent_noChain_notConsumed ()
        do! handleEvent_sessionAborted_setsCancelled ()
        do! handleEvent_newUserMessage_resetsState ()
        do! createHandler_returnsCallable ()
        do! createHandler_twoSessionsIndependent ()
        do! FallbackEventBridgeTestsPart2.run ()
        do! FallbackEventBridgeTestsPendingReview.run ()
    }
