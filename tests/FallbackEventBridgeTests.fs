module Wanxiangshu.Tests.FallbackEventBridgeTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackEventBridge

// ---------------------------------------------------------------------------
// Fake implementations
// ---------------------------------------------------------------------------

type FakeExecutor(?messages: obj array) =
    let mutable continueCalls  : ResizeArray<string * FallbackModel> = ResizeArray()
    let mutable abortCalls     : ResizeArray<string> = ResizeArray()
    let mutable propagateCalls : ResizeArray<string> = ResizeArray()
    let msgs = defaultArg messages [||]

    interface IActionExecutor with
        member _.SendContinue (sessionID, model) : JS.Promise<unit> =
            continueCalls.Add(sessionID, model)
            Promise.lift ()
        member _.AbortSession (sessionID: string) : JS.Promise<unit> =
            abortCalls.Add sessionID
            Promise.lift ()
        member _.FetchMessages (_sessionID: string) : JS.Promise<obj array> =
            Promise.lift msgs
        member _.PropagateFailure (sessionID: string) : JS.Promise<unit> =
            propagateCalls.Add(sessionID)
            Promise.lift ()
        member _.CaptureCurrentModel (_sessionID: string) : JS.Promise<FallbackModel option> =
            Promise.lift None

    member _.ContinueCalls  = continueCalls |> Seq.toList
    member _.AbortCalls     = abortCalls |> Seq.toList
    member _.PropagateCalls = propagateCalls |> Seq.toList

type FakeTranslator(sessionID: string, evt: FallbackEvent) =
    let _sid = sessionID
    let _ev  = evt

    interface IEventTranslator with
        member _.TranslateError (_raw: obj) : FallbackEvent option =
            match _ev with
            | FallbackEvent.SessionError _ -> Some _ev
            | _ -> None
        member _.ExtractSessionID (_raw: obj) : string = _sid
        member _.IsSessionError (_raw: obj) : bool =
            match _ev with
            | FallbackEvent.SessionError _ -> true
            | _ -> false
        member _.IsSessionIdle (_raw: obj) : bool =
            match _ev with
            | FallbackEvent.SessionIdle -> true
            | _ -> false
        member _.IsSessionBusy (_raw: obj) : bool =
            match _ev with
            | FallbackEvent.SessionBusy -> true
            | _ -> false
        member _.IsNewUserMessage (_raw: obj) : bool =
            match _ev with
            | FallbackEvent.NewUserMessage -> true
            | _ -> false

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

let mkModel (pid: string) (mid: string) : FallbackModel =
    { ProviderID      = pid
      ModelID         = mid
      Variant         = None
      Temperature     = None
      TopP            = None
      MaxTokens       = None
      ReasoningEffort = None
      Thinking        = false }

let mkRetryableErr () : ErrorInput =
    { ErrorName   = "err"
      Message     = "fail"
      StatusCode  = None
      IsRetryable = Some true }

let mkAbortErr () : ErrorInput =
    { ErrorName   = "MessageAbortedError"
      Message     = "abort"
      StatusCode  = None
      IsRetryable = None }

let mkConfig () : FallbackConfig =
    { DefaultChain     = []
      AgentChains      = Map.empty
      MaxRetries       = 2
      LoopMaxContinues = 3 }

let defaultCfgLookup (_agent: string) : FallbackConfig = mkConfig ()

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

let handleEvent_retrySame_consumedAndSendContinue () = promise {
    let model  = mkModel "oai" "gpt-5"
    let chain  = [ model ]
    let cfg    = mkConfig ()
    let rt     = FallbackRuntimeState()
    let sid    = "sess-1"
    rt.SetChain sid chain
    rt.SetAgentName sid "reviewer"

    let translator = FakeTranslator(sid, FallbackEvent.SessionError (mkRetryableErr())) :> IEventTranslator
    let executor   = FakeExecutor()

    let! result = handleEvent translator rt defaultCfgLookup executor (box ())

    equal "consumed" true result.Consumed
    equal "phase Retrying 1" (FallbackPhase.Retrying 1) result.State.Phase
    equal "continueCount 1" 1 result.State.ContinueCount
    equal "executor called once" 1 (executor.ContinueCalls.Length)
}

let handleEvent_exhausted_notConsumed () = promise {
    let model  = mkModel "oai" "gpt-5"
    let chain  = [ model ]
    let rt     = FallbackRuntimeState()
    let sid    = "sess-1"
    rt.SetChain sid chain
    rt.SetAgentName sid "reviewer"

    let s0 = rt.GetOrCreateState sid
    rt.UpdateState sid { s0 with Phase = FallbackPhase.Exhausted }

    let translator = FakeTranslator(sid, FallbackEvent.SessionError (mkRetryableErr())) :> IEventTranslator
    let executor   = FakeExecutor()

    let! result = handleEvent translator rt defaultCfgLookup executor (box ())

    equal "not consumed when exhausted" false result.Consumed
}

let handleEvent_noChain_notConsumed () = promise {
    let rt     = FallbackRuntimeState()
    let sid    = "sess-1"
    rt.SetAgentName sid "reviewer"

    let translator = FakeTranslator(sid, FallbackEvent.SessionError (mkRetryableErr())) :> IEventTranslator
    let executor   = FakeExecutor()

    let! result = handleEvent translator rt defaultCfgLookup executor (box ())

    equal "no chain → not consumed" false result.Consumed
}

let handleEvent_sessionAborted_setsCancelled () = promise {
    let model  = mkModel "oai" "gpt-5"
    let chain  = [ model ]
    let cfg    = mkConfig ()
    let rt     = FallbackRuntimeState()
    let sid    = "sess-1"
    rt.SetChain sid chain
    rt.SetAgentName sid "reviewer"

    let translator = FakeTranslator(sid, FallbackEvent.SessionError (mkAbortErr())) :> IEventTranslator
    let executor   = FakeExecutor()

    let! result = handleEvent translator rt defaultCfgLookup executor (box ())

    equal "consumed" true result.Consumed
    equal "cancelled true" true result.State.Cancelled
}

let handleEvent_newUserMessage_resetsState () = promise {
    let model  = mkModel "oai" "gpt-5"
    let chain  = [ model ]
    let cfg    = mkConfig ()
    let rt     = FallbackRuntimeState()
    let sid    = "sess-1"
    rt.SetChain sid chain
    rt.SetAgentName sid "reviewer"

    let s0 = rt.GetOrCreateState sid
    rt.UpdateState sid { s0 with Phase = FallbackPhase.Retrying 3; ContinueCount = 3; FailureCount = 5 }

    let translator = FakeTranslator(sid, FallbackEvent.NewUserMessage) :> IEventTranslator
    let executor   = FakeExecutor()

    let! result = handleEvent translator rt defaultCfgLookup executor (box ())

    equal "consumed" false result.Consumed
    equal "phase Idle" FallbackPhase.Idle result.State.Phase
    equal "continueCount 0" 0 result.State.ContinueCount
    equal "failureCount 0" 0 result.State.FailureCount
    equal "cancelled false" false result.State.Cancelled
}

let createHandler_returnsCallable () = promise {
    let model  = mkModel "oai" "gpt-5"
    let chain  = [ model ]
    let cfg    = mkConfig ()
    let rt     = FallbackRuntimeState()
    let sid    = "sess-1"
    rt.SetChain sid chain
    rt.SetAgentName sid "reviewer"

    let translator = FakeTranslator(sid, FallbackEvent.SessionError (mkRetryableErr())) :> IEventTranslator
    let executor   = FakeExecutor()

    let handler = createHandler translator rt defaultCfgLookup executor
    check "handler is non-null" (not (isNull (box handler)))
}

let createHandler_twoSessionsIndependent () = promise {
    let model  = mkModel "oai" "gpt-5"
    let chain  = [ model ]
    let cfg    = mkConfig ()

    let rt1 = FallbackRuntimeState()
    let rt2 = FallbackRuntimeState()
    let sid1 = "sess-1"
    let sid2 = "sess-2"
    rt1.SetChain sid1 chain; rt1.SetAgentName sid1 "r1"
    rt2.SetChain sid2 chain; rt2.SetAgentName sid2 "r2"

    let tr1 = FakeTranslator(sid1, FallbackEvent.SessionError (mkRetryableErr())) :> IEventTranslator
    let tr2 = FakeTranslator(sid2, FallbackEvent.SessionError (mkRetryableErr())) :> IEventTranslator
    let ex1 = FakeExecutor()
    let ex2 = FakeExecutor()

    let h1 = createHandler tr1 rt1 defaultCfgLookup ex1
    let h2 = createHandler tr2 rt2 defaultCfgLookup ex2

    check "handlers non-null" (not (isNull (box h1)) && not (isNull (box h2)))

    let! r1 = h1 (box ())
    let! r2 = h2 (box ())
    equal "sess-1 consumed" true r1.Consumed
    equal "sess-2 consumed" true r2.Consumed
}

// ---------------------------------------------------------------------------
// Suite entry
// ---------------------------------------------------------------------------

let run () = promise {
    do! handleEvent_retrySame_consumedAndSendContinue ()
    do! handleEvent_exhausted_notConsumed ()
    do! handleEvent_noChain_notConsumed ()
    do! handleEvent_sessionAborted_setsCancelled ()
    do! handleEvent_newUserMessage_resetsState ()
    do! createHandler_returnsCallable ()
    do! createHandler_twoSessionsIndependent ()
}
