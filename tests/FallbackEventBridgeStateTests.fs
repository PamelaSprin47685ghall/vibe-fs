module Wanxiangshu.Tests.FallbackEventBridgeStateTests

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
open Wanxiangshu.Runtime.Fallback.GateFlagTransitions
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.OrdinalTransitions
open Wanxiangshu.Runtime.Fallback.CompactionTransitions
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.Fallback.Coordinator
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.ContinuationExecution
open Wanxiangshu.Runtime

module Dyn = Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Tests.TempWorkspace


type FakeExecutor(?messages: obj array, ?currentModel: FallbackModel) =
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

        member _.CaptureCurrentModel(_sessionID: string) : JS.Promise<FallbackModel option> = Promise.lift currentModel

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

        member _.IsNewUserMessage(_sid, _raw: obj) : bool =
            match _ev with
            | FallbackEvent.NewUserMessage -> true
            | _ -> false

        member _.ExtractNewUserMessageId(_raw) = None

        member _.ExtractRoutingContext(_raw: obj) = None, None

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

let handleEvent_sessionIdle_idle_emitsScanToolCallAsText () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let chain = [ model ]
        let cfg = mkConfig ()
        let rt = FallbackRuntimeStore()
        let sid = "sess-1"
        rt.SetChain sid chain
        rt.SetAgentName sid "reviewer"

        let translator = FakeTranslator(sid, FallbackEvent.SessionIdle) :> IEventTranslator
        let executor = FakeExecutor()

        let! result, _ = handleEvent translator rt defaultCfgLookup executor "" (box ()) None

        equal "consumed" false result.Consumed
        equal "phase Idle" FallbackPhase.Idle result.State.Phase
        equal "lifecycle TaskComplete" FallbackLifecycle.TaskComplete result.State.Lifecycle
        equal "no continue calls" 0 (executor.ContinueCalls.Length)
        equal "no recover calls" 0 (executor.RecoverCalls.Length)
    }

let handleEvent_sessionIdle_idle_toolText_sendsPrompt () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let chain = [ model ]
        let cfg = mkConfig ()
        let rt = FallbackRuntimeStore()
        let sid = "sess-1"
        rt.SetChain sid chain
        rt.SetAgentName sid "reviewer"

        let toolMsg =
            createObj
                [ "info", box (createObj [ "role", box "assistant" ])
                  "parts",
                  box
                      [| createObj
                             [ "type", box "text"
                               "text", box "<function=read>\n<parameter=filePath>\n/foo\n</parameter>\n</function>" ] |] ]

        let translator = FakeTranslator(sid, FallbackEvent.SessionIdle) :> IEventTranslator
        let executor = FakeExecutor(messages = [| toolMsg |])

        let! result, intentOpt = handleEvent translator rt defaultCfgLookup executor "" (box ()) None

        match intentOpt with
        | Some intent -> do! executeContinuationIntent rt executor "" sid intent
        | None -> ()

        equal "phase RecoveringToolCallText" FallbackPhase.RecoveringToolCallText result.State.Phase
        equal "consumed true (blocks nudge)" true result.Consumed
        equal "recover called once" 1 (executor.RecoverCalls.Length)
        let (_, _, promptText) = executor.RecoverCalls.[0]
        equal "prompt contains recovery text" true (promptText.Contains "raw text")
    }

let handleEvent_sessionIdle_idle_todosComplete_setsTaskComplete () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let chain = [ model ]
        let cfg = mkConfig ()
        let rt = FallbackRuntimeStore()
        let sid = "sess-1"
        rt.SetChain sid chain
        rt.SetAgentName sid "reviewer"

        let todoPart =
            createObj
                [ "type", box "tool"
                  "tool", box "task"
                  "state",
                  box (
                      createObj
                          [ "input", box (createObj [ "todos", box [| createObj [ "status", box "completed" ] |] ]) ]
                  ) ]

        let todoMsg = createObj [ "parts", box [| todoPart |] ]

        let translator = FakeTranslator(sid, FallbackEvent.SessionIdle) :> IEventTranslator
        let executor = FakeExecutor(messages = [| todoMsg |])

        let! result, _ = handleEvent translator rt defaultCfgLookup executor "" (box ()) None

        equal "phase Idle" FallbackPhase.Idle result.State.Phase
        equal "lifecycle TaskComplete" FallbackLifecycle.TaskComplete result.State.Lifecycle
        equal "no recover calls" 0 (executor.RecoverCalls.Length)
    }

let handleEvent_sessionIdle_retryToIdle_emitsScanToolCallAsText () =
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
                Phase = FallbackPhase.Retrying 1 }

        let toolMsg =
            createObj
                [ "info", box (createObj [ "role", box "assistant" ])
                  "parts", box [| createObj [ "type", box "text"; "text", box "<invoke name=\"write\">" ] |] ]

        let translator = FakeTranslator(sid, FallbackEvent.SessionIdle) :> IEventTranslator
        let executor = FakeExecutor(messages = [| toolMsg |])

        let! result, intentOpt = handleEvent translator rt defaultCfgLookup executor "" (box ()) None

        match intentOpt with
        | Some intent -> do! executeContinuationIntent rt executor "" sid intent
        | None -> ()

        equal "phase RecoveringToolCallText" FallbackPhase.RecoveringToolCallText result.State.Phase
        equal "consumed true" true result.Consumed
        equal "recover called once" 1 (executor.RecoverCalls.Length)
    }

let handleEvent_sessionBusy_duringRetrying_consumedTrue () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let rt = FallbackRuntimeStore()
        let sid = "sess-busy-retrying"
        rt.SetChain sid [ model ]
        rt.SetAgentName sid "reviewer"
        let s0 = rt.GetOrCreateState sid

        rt.UpdateState
            sid
            { s0 with
                Phase = FallbackPhase.Retrying 1 }

        rt.SetConsumed sid true
        let tr = FakeTranslator(sid, FallbackEvent.SessionBusy) :> IEventTranslator
        let! r, _ = handleEvent tr rt defaultCfgLookup (FakeExecutor()) "" (box ()) None
        equal "consumed true during retrying" true r.Consumed
        equal "phase Idle after busy" FallbackPhase.Idle r.State.Phase
        equal "lifecycle Active" FallbackLifecycle.Active r.State.Lifecycle
    }

let handleEvent_chainPrependsCurrentModel () =
    promise {
        let rt = FallbackRuntimeStore()
        let sid = "sess-prepend"
        rt.SetAgentName sid "coder"

        let m0 = mkModel "anthropic" "claude-4"
        let m1 = mkModel "openai" "gpt-5"
        let m2 = mkModel "zai" "glm-5"

        let executor = FakeExecutor(currentModel = m0)

        let tr =
            FakeTranslator(sid, FallbackEvent.SessionError(mkRetryableErr ())) :> IEventTranslator

        let customLookup (agent: string) =
            { mkConfig () with
                DefaultChain = [ m1; m2 ] }

        let handler = createHandler tr rt customLookup executor "" None
        let! res = handler (box ())

        let chain = rt.GetChain sid
        equal "chain has 3 models" 3 chain.Length
        equal "first is current model m0" m0 chain.[0]
        equal "second is m1" m1 chain.[1]
        equal "third is m2" m2 chain.[2]

        let rt2 = FallbackRuntimeStore()
        let sid2 = "sess-prepend-exists"
        rt2.SetAgentName sid2 "coder"
        let executor2 = FakeExecutor(currentModel = m1)

        let tr2 =
            FakeTranslator(sid2, FallbackEvent.SessionError(mkRetryableErr ())) :> IEventTranslator

        let handler2 = createHandler tr2 rt2 customLookup executor2 "" None
        let! res2 = handler2 (box ())

        let chain2 = rt2.GetChain sid2
        equal "chain2 has 2 models" 2 chain2.Length
        equal "first is m1" m1 chain2.[0]
        equal "second is m2" m2 chain2.[1]
    }

let handleEvent_userAbort_invalidatesLease () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let rt = FallbackRuntimeStore()
        let sid = "sess-abort"
        rt.SetChain sid [ model ]
        rt.SetAgentName sid "reviewer"

        // Setup initial state
        let turnId = rt.UpdateSessionReturning(sid, advanceHumanTurn)
        let gen = rt.GetSessionGeneration sid
        let cancelGen = rt.GetCancelGeneration sid
        rt.SetActiveContinuationGeneration sid gen
        rt.SetActiveContinuationCancelGeneration sid cancelGen
        let continuationID = "cont-1"

        let intent =
            SendContinueIntent(model, "reviewer", turnId, gen, cancelGen, continuationID, 1)

        let executor = FakeExecutor()

        let translator =
            FakeTranslator(sid, FallbackEvent.SessionError(mkAbortErr ())) :> IEventTranslator

        // Start dispatch
        let! _ = handleEvent translator rt defaultCfgLookup executor "" (box ()) None

        // Ensure state is Cancelled
        equal "lifecycle Cancelled" FallbackLifecycle.Cancelled (rt.GetOrCreateState sid).Lifecycle

        // Simulate late dispatch intent execution
        do! executeContinuationIntent rt executor "" sid intent

        equal "executor called 0 times" 0 executor.ContinueCalls.Length
    }

let handleEvent_newUserMessage_doesNotClearMainContinuationAwaitingStart () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let rt = FallbackRuntimeStore()
        let sid = "sess-new-user-test"
        rt.SetChain sid [ model ]
        rt.SetAgentName sid "reviewer"
        rt.SetMainContinuationAwaitingStart sid true

        let rawEvent =
            createObj
                [ "type", box "message.updated"
                  "properties", box (createObj [ "info", box (createObj [ "role", box "user" ]) ]) ]

        let translator =
            FakeTranslator(sid, FallbackEvent.NewUserMessage) :> IEventTranslator

        let executor = FakeExecutor()

        let! result, _ = handleEvent translator rt defaultCfgLookup executor "" rawEvent None

        equal
            "MainContinuationAwaitingStart is still true on new user message"
            true
            (rt.IsMainContinuationAwaitingStart sid)
    }

let handleEvent_emptyFallbackChain_isNotBypassed () =
    promise {
        let rt = FallbackRuntimeStore()
        let sid = "sess-empty-chain-test"
        rt.SetAgentName sid "reviewer"
        rt.SetChain sid []

        let translator =
            FakeTranslator(sid, FallbackEvent.SessionError(mkRetryableErr ())) :> IEventTranslator

        let executor = FakeExecutor()

        let! result, _ = handleEvent translator rt defaultCfgLookup executor "" (box ()) None
        equal "Consumed is false when fallback chain is empty" false result.Consumed
    }

let ompEventTranslator_correctlyExtractsContinuationIdAndOrdinal () =
    promise {
        let rt = FallbackRuntimeStore()

        let translator =
            Wanxiangshu.Hosts.Omp.Fallback.EventTranslator.ompEventTranslator rt

        let props =
            createObj [ "continuationID", box "cont-omp-1"; "continuationOrdinal", box 3 ]

        let evObj = createObj [ "type", box "session.busy"; "info", box props ]

        let rawEvent =
            createObj [ "event", box evObj; "props", box (createObj [ "sessionID", box "s-1" ]) ]

        let identity = translator.ExtractContinuationIdentity rawEvent
        check "identity extracted" identity.IsSome
        let cid, ord = identity.Value
        equal "extracted continuationID" "cont-omp-1" cid
        equal "extracted ordinal" 3 ord
    }

let run () =
    promise {
        do! handleEvent_sessionIdle_idle_emitsScanToolCallAsText ()
        do! handleEvent_sessionIdle_idle_toolText_sendsPrompt ()
        do! handleEvent_sessionIdle_idle_todosComplete_setsTaskComplete ()
        do! handleEvent_sessionIdle_retryToIdle_emitsScanToolCallAsText ()
        do! handleEvent_sessionBusy_duringRetrying_consumedTrue ()
        do! handleEvent_chainPrependsCurrentModel ()
        do! handleEvent_userAbort_invalidatesLease ()
        do! handleEvent_newUserMessage_doesNotClearMainContinuationAwaitingStart ()
        do! handleEvent_emptyFallbackChain_isNotBypassed ()
        do! ompEventTranslator_correctlyExtractsContinuationIdAndOrdinal ()
    }
