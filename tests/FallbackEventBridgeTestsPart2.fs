module Wanxiangshu.Tests.FallbackEventBridgeTestsPart2

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackEventBridge
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

let handleEvent_sessionIdle_idle_emitsScanToolCallAsText () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let chain = [ model ]
        let cfg = mkConfig ()
        let rt = FallbackRuntimeState()
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
        let rt = FallbackRuntimeState()
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
        let rt = FallbackRuntimeState()
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
        let rt = FallbackRuntimeState()
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
        let rt = FallbackRuntimeState()
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
        let rt = FallbackRuntimeState()
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

        let rt2 = FallbackRuntimeState()
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
        let rt = FallbackRuntimeState()
        let sid = "sess-abort"
        rt.SetChain sid [ model ]
        rt.SetAgentName sid "reviewer"

        // Setup initial state
        let turnId = rt.IncrementHumanTurnId sid
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

let handleEvent_subRunAttemptOrdinal_incrementsOnContinuationDispatch () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let rt = FallbackRuntimeState()
        let sid = "sess-subrun-incr"
        rt.SetChain sid [ model ]
        rt.SetAgentName sid "reviewer"
        rt.SetSessionOwner sid "Fallback"

        // Setup generations
        let turnId = rt.IncrementHumanTurnId sid
        let gen = rt.GetSessionGeneration sid
        let cancelGen = rt.GetCancelGeneration sid
        rt.SetActiveContinuationGeneration sid gen
        rt.SetActiveContinuationCancelGeneration sid cancelGen

        // Start subsession run
        let runId = "subrun-incr-1"
        let started = rt.StartSubsessionRun(sid, "parent-session", runId)
        equal "subsession run started" true started

        // Under initial run, attempt is 0
        match rt.GetSubsessionRun(sid, runId) with
        | Some subRun -> equal "initial attempt is 0" 0 subRun.ActiveAttemptOrdinal
        | None -> failwith "subRun not found"

        // Setup first lease for cont-1
        let continuationID = "cont-1"

        let lease =
            { ContinuationID = continuationID
              ContinuationOrdinal = 1
              SessionGeneration = gen
              HumanTurnID = turnId
              CancelGeneration = cancelGen
              Owner = "Fallback"
              Model = model
              PromptText = None
              Status = "requested" }

        rt.SetPendingLease(sid, lease)

        let intent1 =
            SendContinueIntent(model, "reviewer", turnId, gen, cancelGen, continuationID, 1)

        let executor = FakeExecutor()

        // First dispatch activation
        rt.ActivateAttempt(sid, continuationID, 1, None)

        // Setup second lease for cont-2
        let continuationID2 = "cont-2"

        let lease2 =
            { ContinuationID = continuationID2
              ContinuationOrdinal = 2
              SessionGeneration = gen
              HumanTurnID = turnId
              CancelGeneration = cancelGen
              Owner = "Fallback"
              Model = model
              PromptText = None
              Status = "requested" }

        rt.SetPendingLease(sid, lease2)

        let intent2 =
            SendContinueIntent(model, "reviewer", turnId, gen, cancelGen, continuationID2, 2)

        // Second dispatch activation
        rt.ActivateAttempt(sid, continuationID2, 2, None)

        // The attempt ordinal must be incremented.
        match rt.GetSubsessionRun(sid, runId) with
        | Some subRun ->
            // In the expected behavior, after 2 dispatches, attempt ordinal must be 2.
            // Currently (before fix) it is still 0, so this check will fail.
            equal "attempt ordinal incremented to 2" 2 subRun.ActiveAttemptOrdinal
        | None -> failwith "subRun not found"

    }

let handleEvent_oldAttemptEventWithoutContinuationId_isIgnored () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let rt = FallbackRuntimeState()
        let sid = "sess-old-event"
        rt.SetChain sid [ model ]
        rt.SetAgentName sid "reviewer"
        rt.SetSessionOwner sid "Fallback"

        // Setup initial generations
        let turnId = rt.IncrementHumanTurnId sid
        let gen = rt.GetSessionGeneration sid
        let cancelGen = rt.GetCancelGeneration sid
        rt.SetActiveContinuationGeneration sid gen
        rt.SetActiveContinuationCancelGeneration sid cancelGen

        // Start subsession run
        let runId = "subrun-old-event-1"
        let started = rt.StartSubsessionRun(sid, "parent-session", runId)
        equal "subsession run started" true started

        // Set active continuation ordinal and ID on the subsession run to 2
        match rt.GetSubsessionRun(sid, runId) with
        | Some subRun ->
            subRun.ActiveContinuationId <- "cont-2"
            subRun.ActiveContinuationOrdinal <- 2
        | None -> failwith "subRun not found"

        // Set pending lease status to dispatched or similar for cont-2
        let lease =
            { ContinuationID = "cont-2"
              ContinuationOrdinal = 2
              SessionGeneration = gen
              HumanTurnID = turnId
              CancelGeneration = cancelGen
              Owner = "Fallback"
              Model = model
              PromptText = None
              Status = "dispatched" }

        rt.SetPendingLease(sid, lease)
        rt.SetActiveContinuationGeneration sid gen
        rt.SetActiveContinuationCancelGeneration sid cancelGen
        rt.SetAwaitingBusy sid true
        rt.SetBusyObserved sid false

        // Construct a raw event that has continuationOrdinal = 1 but no continuationId (empty string)
        let props = createObj [ "continuationOrdinal", box 1; "continuationId", box "" ]
        let rawEvent = createObj [ "type", box "session.idle"; "properties", box props ]

        let translator = FakeTranslator(sid, FallbackEvent.SessionIdle) :> IEventTranslator
        let executor = FakeExecutor()

        let! result, _ = handleEvent translator rt defaultCfgLookup executor "" rawEvent None

        // Under the expected behavior, since this event has continuationOrdinal = 1 (mismatching current active ordinal = 2),
        // it must be treated as stale/ignored, so Consumed should be false.
        // Currently (before fix) it checks only human turn generation and because continuationId is empty,
        // it accepts the event and Consumed will be true (or at least it won't reject it as stale).
        // Let's assert Consumed is false.
        equal "consumed is false because event is from old attempt" false result.Consumed
    }

let handleEvent_subRunAttempt_withoutId_oldBusyDoesNotOpenAttempt () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let rt = FallbackRuntimeState()
        let sid = "sess-subrun-without-id"
        rt.SetChain sid [ model ]
        rt.SetAgentName sid "reviewer"
        rt.SetSessionOwner sid "Fallback"

        // Setup generations
        let turnId = rt.IncrementHumanTurnId sid
        let gen = rt.GetSessionGeneration sid
        let cancelGen = rt.GetCancelGeneration sid
        rt.SetActiveContinuationGeneration sid gen
        rt.SetActiveContinuationCancelGeneration sid cancelGen

        // Start subsession run
        let runId = "subrun-without-id-1"
        let started = rt.StartSubsessionRun(sid, "parent-session", runId)
        equal "subsession run started" true started

        // Setup first lease for cont-1
        let continuationID = "cont-1"

        let lease =
            { ContinuationID = continuationID
              ContinuationOrdinal = 1
              SessionGeneration = gen
              HumanTurnID = turnId
              CancelGeneration = cancelGen
              Owner = "Fallback"
              Model = model
              PromptText = None
              Status = "requested" }

        rt.SetPendingLease(sid, lease)

        let intent1 =
            SendContinueIntent(model, "reviewer", turnId, gen, cancelGen, continuationID, 1)

        let executor = FakeExecutor(messages = [| createObj [ "id", box "msg-1" ] |])

        // First dispatch sets DispatchMessageBoundary = "msg-1"
        rt.ActivateAttempt(sid, continuationID, 1, Some "msg-1")
        rt.SetAwaitingBusy sid true

        match rt.GetSubsessionRun(sid, runId) with
        | Some subRun -> equal "DispatchMessageBoundary is msg-1" (Some "msg-1") subRun.DispatchMessageBoundary
        | None -> failwith "subRun not found"

        // Simulated late busy event with boundary msg-1 (not strictly greater)
        let props = createObj [ "message", box (createObj [ "id", box "msg-1" ]) ]
        let rawEvent = createObj [ "type", box "session.busy"; "properties", box props ]

        let translator = FakeTranslator(sid, FallbackEvent.SessionBusy) :> IEventTranslator
        let! result, _ = handleEvent translator rt defaultCfgLookup executor "" rawEvent None

        equal "consumed is false for old busy" false result.Consumed
        equal "AwaitingBusy is still true" true (rt.IsAwaitingBusy sid)

        // Late busy event with no boundary evidence at all
        let rawEventNoEvidence = createObj [ "type", box "session.busy" ]
        let! resultNoEvidence, _ = handleEvent translator rt defaultCfgLookup executor "" rawEventNoEvidence None
        equal "consumed is false for no evidence" false resultNoEvidence.Consumed
        equal "AwaitingBusy is still true" true (rt.IsAwaitingBusy sid)
    }

let handleEvent_subRunAttempt_withoutId_newerAssistantOpensAttempt () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let rt = FallbackRuntimeState()
        let sid = "sess-subrun-newer"
        rt.SetChain sid [ model ]
        rt.SetAgentName sid "reviewer"
        rt.SetSessionOwner sid "Fallback"

        // Setup generations
        let turnId = rt.IncrementHumanTurnId sid
        let gen = rt.GetSessionGeneration sid
        let cancelGen = rt.GetCancelGeneration sid
        rt.SetActiveContinuationGeneration sid gen
        rt.SetActiveContinuationCancelGeneration sid cancelGen

        // Start subsession run
        let runId = "subrun-newer-1"
        let started = rt.StartSubsessionRun(sid, "parent-session", runId)
        equal "subsession run started" true started

        // Setup lease
        let continuationID = "cont-1"

        let lease =
            { ContinuationID = continuationID
              ContinuationOrdinal = 1
              SessionGeneration = gen
              HumanTurnID = turnId
              CancelGeneration = cancelGen
              Owner = "Fallback"
              Model = model
              PromptText = None
              Status = "requested" }

        rt.SetPendingLease(sid, lease)

        let intent1 =
            SendContinueIntent(model, "reviewer", turnId, gen, cancelGen, continuationID, 1)

        let executor = FakeExecutor(messages = [| createObj [ "id", box "msg-1" ] |])

        // First dispatch sets DispatchMessageBoundary = "msg-1"
        rt.ActivateAttempt(sid, continuationID, 1, Some "msg-1")

        // Send assistant message event with boundary "msg-2"
        let infoObj = createObj [ "role", box "assistant"; "id", box "msg-2" ]
        let props = createObj [ "info", box infoObj ]

        let rawEventMsg =
            createObj [ "type", box "message.updated"; "properties", box props ]

        let translator = FakeTranslator(sid, FallbackEvent.SessionIdle) :> IEventTranslator
        let! resultMsg, _ = handleEvent translator rt defaultCfgLookup executor "" rawEventMsg None

        match rt.GetSubsessionRun(sid, runId) with
        | Some subRun ->
            equal "ObservedCurrentAttemptBoundary is msg-2" (Some "msg-2") subRun.ObservedCurrentAttemptBoundary
        | None -> failwith "subRun not found"

        // Now send matching session.idle event with boundary "msg-2"
        let propsIdle = createObj [ "message", box (createObj [ "id", box "msg-2" ]) ]

        let rawEventIdle =
            createObj [ "type", box "session.idle"; "properties", box propsIdle ]

        let! resultIdle, _ = handleEvent translator rt defaultCfgLookup executor "" rawEventIdle None

        equal "AwaitingBusy is false" false (rt.IsAwaitingBusy sid)
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
        do! handleEvent_subRunAttemptOrdinal_incrementsOnContinuationDispatch ()
        do! handleEvent_oldAttemptEventWithoutContinuationId_isIgnored ()
        do! handleEvent_subRunAttempt_withoutId_oldBusyDoesNotOpenAttempt ()
        do! handleEvent_subRunAttempt_withoutId_newerAssistantOpensAttempt ()
    }
