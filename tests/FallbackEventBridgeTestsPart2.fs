module Wanxiangshu.Tests.FallbackEventBridgeTestsPart2

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackEventBridge


type FakeExecutor(?messages: obj array) =
    let mutable continueCalls  : ResizeArray<string * FallbackModel> = ResizeArray()
    let mutable recoverCalls   : ResizeArray<string * FallbackModel * string> = ResizeArray()
    let mutable propagateCalls : ResizeArray<string> = ResizeArray()
    let msgs = defaultArg messages [||]

    interface IActionExecutor with
        member _.SendContinue (sessionID, model) : JS.Promise<unit> =
            continueCalls.Add(sessionID, model)
            Promise.lift ()
        member _.RecoverWithPrompt (sessionID, model, promptText) : JS.Promise<unit> =
            recoverCalls.Add(sessionID, model, promptText)
            Promise.lift ()
        member _.FetchMessages (_sessionID: string) : JS.Promise<obj array> =
            Promise.lift msgs
        member _.PropagateFailure (sessionID: string) : JS.Promise<unit> =
            propagateCalls.Add(sessionID)
            Promise.lift ()
        member _.CaptureCurrentModel (_sessionID: string) : JS.Promise<FallbackModel option> =
            Promise.lift None

    member _.ContinueCalls  = continueCalls |> Seq.toList
    member _.RecoverCalls   = recoverCalls |> Seq.toList
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
      DomainError = Some (UnknownJsError "fail")
      Message     = "fail"
      StatusCode  = None
      IsRetryable = Some true }

let mkConfig () : FallbackConfig =
    { DefaultChain     = []
      AgentChains      = Map.empty
      MaxRetries       = 2
      LoopMaxContinues = 3 }

let defaultCfgLookup (_agent: string) : FallbackConfig = mkConfig ()

let handleEvent_sessionIdle_idle_emitsScanToolCallAsText () = promise {
    let model  = mkModel "oai" "gpt-5"
    let chain  = [ model ]
    let cfg    = mkConfig ()
    let rt     = FallbackRuntimeState()
    let sid    = "sess-1"
    rt.SetChain sid chain
    rt.SetAgentName sid "reviewer"

    let translator = FakeTranslator(sid, FallbackEvent.SessionIdle) :> IEventTranslator
    let executor   = FakeExecutor()

    let! result = handleEvent translator rt defaultCfgLookup executor (box ())

    equal "consumed" false result.Consumed
    equal "phase Idle" FallbackPhase.Idle result.State.Phase
    equal "taskComplete false" false result.State.TaskComplete
    equal "no continue calls" 0 (executor.ContinueCalls.Length)
    equal "no recover calls" 0 (executor.RecoverCalls.Length)
}

let handleEvent_sessionIdle_idle_toolText_sendsPrompt () = promise {
    let model  = mkModel "oai" "gpt-5"
    let chain  = [ model ]
    let cfg    = mkConfig ()
    let rt     = FallbackRuntimeState()
    let sid    = "sess-1"
    rt.SetChain sid chain
    rt.SetAgentName sid "reviewer"

    let toolMsg = createObj [
        "info", box (createObj [ "role", box "assistant" ])
        "parts", box [| createObj [ "type", box "text"; "text", box "<tool_call>name=\"read\"</tool_call>" ] |]
    ]

    let translator = FakeTranslator(sid, FallbackEvent.SessionIdle) :> IEventTranslator
    let executor   = FakeExecutor(messages = [| toolMsg |])

    let! result = handleEvent translator rt defaultCfgLookup executor (box ())

    equal "phase Idle" FallbackPhase.Idle result.State.Phase
    equal "recover called once" 1 (executor.RecoverCalls.Length)
    let (_, _, promptText) = executor.RecoverCalls.[0]
    equal "prompt contains recovery text" true (promptText.Contains "raw text")
}

let handleEvent_sessionIdle_idle_todosComplete_setsTaskComplete () = promise {
    let model  = mkModel "oai" "gpt-5"
    let chain  = [ model ]
    let cfg    = mkConfig ()
    let rt     = FallbackRuntimeState()
    let sid    = "sess-1"
    rt.SetChain sid chain
    rt.SetAgentName sid "reviewer"

    let todoPart = createObj [
        "type", box "tool"
        "tool", box "task"
        "state", box (createObj [
            "input", box (createObj [
                "todos", box [| createObj [ "status", box "completed" ] |]
            ])
        ])
    ]
    let todoMsg = createObj [ "parts", box [| todoPart |] ]

    let translator = FakeTranslator(sid, FallbackEvent.SessionIdle) :> IEventTranslator
    let executor   = FakeExecutor(messages = [| todoMsg |])

    let! result = handleEvent translator rt defaultCfgLookup executor (box ())

    equal "phase Idle" FallbackPhase.Idle result.State.Phase
    equal "taskComplete true" true result.State.TaskComplete
    equal "no recover calls" 0 (executor.RecoverCalls.Length)
}

let handleEvent_sessionIdle_retryToIdle_emitsScanToolCallAsText () = promise {
    let model  = mkModel "oai" "gpt-5"
    let chain  = [ model ]
    let cfg    = mkConfig ()
    let rt     = FallbackRuntimeState()
    let sid    = "sess-1"
    rt.SetChain sid chain
    rt.SetAgentName sid "reviewer"

    let s0 = rt.GetOrCreateState sid
    rt.UpdateState sid { s0 with Phase = FallbackPhase.Retrying 1 }

    let toolMsg = createObj [
        "info", box (createObj [ "role", box "assistant" ])
        "parts", box [| createObj [ "type", box "text"; "text", box "<invoke name=\"write\">" ] |]
    ]

    let translator = FakeTranslator(sid, FallbackEvent.SessionIdle) :> IEventTranslator
    let executor   = FakeExecutor(messages = [| toolMsg |])

    let! result = handleEvent translator rt defaultCfgLookup executor (box ())

    equal "phase Idle" FallbackPhase.Idle result.State.Phase
    equal "recover called once" 1 (executor.RecoverCalls.Length)
}


let run () = promise {
    do! handleEvent_sessionIdle_idle_emitsScanToolCallAsText ()
    do! handleEvent_sessionIdle_idle_toolText_sendsPrompt ()
    do! handleEvent_sessionIdle_idle_todosComplete_setsTaskComplete ()
    do! handleEvent_sessionIdle_retryToIdle_emitsScanToolCallAsText ()
}
