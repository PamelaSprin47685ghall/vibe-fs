module Wanxiangshu.Tests.FallbackAgentAndModelTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeTransitions
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Hosts.Omp.Fallback.ActionExecutor
open Wanxiangshu.Hosts.Opencode.Fallback.ActionExecutor
open Wanxiangshu.Runtime.BacklogSession
open Wanxiangshu.Hosts.Opencode.SessionLifecycleObserver
open Wanxiangshu.Hosts.Omp.Fallback.Hook
open Wanxiangshu.Hosts.Opencode.Fallback.Hook
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Kernel.Subsession.Types

module Dyn = Wanxiangshu.Runtime.Dyn

/// Override OpenCode chat.message receipt timeout for unit tests.
[<Emit("process.env.WANXIANGSHU_OPENCODE_RECEIPT_TIMEOUT_MS = $0")>]
let private setReceiptTimeout (ms: string) : unit = jsNative

let ompFallbackHooksPreservesAgentAndModelSpec () =
    promise {
        let rt = FallbackRuntimeStore()
        let sid = "omp-subagent-sess"
        rt.UpdateSession(sid, recordAgentName "coder")

        let mutable lastPromptArg = null

        let sessionApiMock =
            createObj
                [ "sessionPrompt",
                  box (fun (arg: obj) ->
                      lastPromptArg <- arg
                      Promise.lift (box null)) ]

        let executor = ompActionExecutor rt sessionApiMock

        let model =
            { ProviderID = "openai"
              ModelID = "gpt-5"
              Variant = None
              Temperature = None
              TopP = None
              MaxTokens = None
              ReasoningEffort = None
              Thinking = false }

        do! executor.SendContinue(sid, model, "dummy")

        check "sessionPrompt was called" (not (isNull lastPromptArg))
        let body = Dyn.get lastPromptArg "body"
        let prompt = Dyn.get body "prompt"
        equal "prompt has text zero-width" "​" (Dyn.str prompt "text")
        equal "prompt has agent coder" "coder" (Dyn.str prompt "agent")
        equal "prompt has model gpt-5" "openai/gpt-5" (Dyn.str prompt "model")
    }

let ompCaptureCurrentModelReturnsModelWhenSessionHasModelSpec () =
    promise {
        let rt = FallbackRuntimeStore()
        let sid = "omp-sess-with-model"

        let mockSession =
            createObj [ "model", box (createObj [ "provider", box "anthropic"; "id", box "claude-4" ]) ]

        Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope.Add("omp_session_" + sid, mockSession)

        let executor = ompActionExecutor rt null
        let! modelOpt = executor.CaptureCurrentModel sid

        check "model is captured" modelOpt.IsSome
        let m = modelOpt.Value
        equal "provider is anthropic" "anthropic" m.ProviderID
        equal "modelId is claude-4" "claude-4" m.ModelID

        Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope.Remove("omp_session_" + sid)
    }

let opencodeExecutorUsesRuntimeAgentWhenNoAssistantMessageSpec () =
    promise {
        let rt = FallbackRuntimeStore()
        let sid = "opencode-no-assistant"
        rt.UpdateSession(sid, recordAgentName "inspector")

        let mutable lastPromptArg = null

        let mockClient =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "messages", box (fun (_arg: obj) -> Promise.lift (box {| data = [||] |}))
                            "prompt",
                            box (fun (arg: obj) ->
                                lastPromptArg <- arg
                                let parts = Dyn.get (Dyn.get arg "body") "parts" |> unbox<obj array>
                                let metadata = Dyn.get parts.[0] "metadata"
                                let wanxiangshu = Dyn.get metadata "wanxiangshu"
                                let continuationId = Dyn.str wanxiangshu "continuationId"
                                HostReceiptWaiterRegistry.tryResolve
                                    (Id.workspaceIdQuick "opencode-default")
                                    sid
                                    continuationId
                                    OrderedTurnMarkerObserved
                                |> ignore
                                Promise.lift (box null)) ]
                  ) ]

        let executor = opencodeActionExecutor rt mockClient

        let model =
            { ProviderID = "openai"
              ModelID = "gpt-5"
              Variant = None
              Temperature = None
              TopP = None
              MaxTokens = None
              ReasoningEffort = None
              Thinking = false }

        rt.UpdateSession(sid, startDispatch model None)
        let continuationId = (rt.GetSession sid).PendingLease.Value.ContinuationID
        do! executor.SendContinue(sid, model, continuationId)

        check "prompt was called" (not (isNull lastPromptArg))
        let body = Dyn.get lastPromptArg "body"
        equal "body has agent inspector" "inspector" (Dyn.str body "agent")
        let parts = unbox<obj array> (Dyn.get body "parts")
        equal "body prompt text is zero-width" "​" (Dyn.str parts.[0] "text")
        let wanxiangshu = Dyn.get (Dyn.get parts.[0] "metadata") "wanxiangshu"
        equal "body carries fallback continuation kind" "fallback_continuation" (Dyn.str wanxiangshu "kind")
        equal "body carries active continuation id" continuationId (Dyn.str wanxiangshu "continuationId")
    }

let opencodeExecutorRespectsUserSelectedModelAndAgentSpec () =
    promise {
        let rt = FallbackRuntimeStore()
        let sid = "opencode-user-selected"
        rt.UpdateSession(sid, recordAgentName "inspector")

        let mutable lastPromptArg = null

        let mockClient =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "model", box (createObj [ "providerID", box "anthropic"; "modelID", box "claude-4" ])
                            "agent", box "reviewer"
                            "messages", box (fun (_arg: obj) -> Promise.lift (box {| data = [||] |}))
                            "prompt",
                            box (fun (arg: obj) ->
                                lastPromptArg <- arg
                                let parts = Dyn.get (Dyn.get arg "body") "parts" |> unbox<obj array>
                                let metadata = Dyn.get parts.[0] "metadata"
                                let wanxiangshu = Dyn.get metadata "wanxiangshu"
                                let continuationId = Dyn.str wanxiangshu "continuationId"
                                HostReceiptWaiterRegistry.tryResolve
                                    (Id.workspaceIdQuick "opencode-default")
                                    sid
                                    continuationId
                                    OrderedTurnMarkerObserved
                                |> ignore
                                Promise.lift (box null)) ]
                  ) ]

        let executor = opencodeActionExecutor rt mockClient

        let model =
            { ProviderID = "openai"
              ModelID = "gpt-5"
              Variant = None
              Temperature = None
              TopP = None
              MaxTokens = None
              ReasoningEffort = None
              Thinking = false }

        rt.UpdateSession(sid, startDispatch model None)
        let continuationId = (rt.GetSession sid).PendingLease.Value.ContinuationID
        do! executor.SendContinue(sid, model, continuationId)

        check "prompt was called" (not (isNull lastPromptArg))
        let body = Dyn.get lastPromptArg "body"
        equal "body has user selected agent reviewer" "reviewer" (Dyn.str body "agent")
        let modelObj = Dyn.get body "model"
        equal "body has fallback model provider" "openai" (Dyn.str modelObj "providerID")
        equal "body has fallback model ID" "gpt-5" (Dyn.str modelObj "modelID")
        let parts = unbox<obj array> (Dyn.get body "parts")
        let wanxiangshu = Dyn.get (Dyn.get parts.[0] "metadata") "wanxiangshu"
        equal "body carries fallback continuation kind" "fallback_continuation" (Dyn.str wanxiangshu "kind")
        equal "body carries active continuation id" continuationId (Dyn.str wanxiangshu "continuationId")
    }

let ompExecutorRespectsUserSelectedModelAndAgentSpec () =
    promise {
        let rt = FallbackRuntimeStore()
        let sid = "omp-user-selected"
        rt.UpdateSession(sid, recordAgentName "coder")

        let mockSession =
            createObj
                [ "model", box (createObj [ "provider", box "google"; "id", box "gemini-2" ])
                  "agent", box "reviewer" ]

        Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope.Add("omp_session_" + sid, mockSession)

        let mutable lastPromptArg = null

        let sessionApiMock =
            createObj
                [ "sessionPrompt",
                  box (fun (arg: obj) ->
                      lastPromptArg <- arg
                      Promise.lift (box null)) ]

        let executor = ompActionExecutor rt sessionApiMock

        let model =
            { ProviderID = "openai"
              ModelID = "gpt-5"
              Variant = None
              Temperature = None
              TopP = None
              MaxTokens = None
              ReasoningEffort = None
              Thinking = false }

        do! executor.SendContinue(sid, model, "dummy")

        check "sessionPrompt was called" (not (isNull lastPromptArg))
        let body = Dyn.get lastPromptArg "body"
        let prompt = Dyn.get body "prompt"
        equal "prompt has user selected agent reviewer" "reviewer" (Dyn.str prompt "agent")
        equal "prompt has fallback model" "openai/gpt-5" (Dyn.str prompt "model")

        Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope.Remove("omp_session_" + sid)
    }

let opencodeExecutorReceiptTimeoutFreesDispatcherSlot () =
    promise {
        setReceiptTimeout "50"
        try
            let rt = FallbackRuntimeStore()
            let sid = "opencode-receipt-timeout"
            rt.UpdateSession(sid, recordAgentName "coder")

            let mutable promptCallCount = 0

            let mockClient =
                createObj
                    [ "session",
                      box (
                          createObj
                              [ "messages", box (fun (_arg: obj) -> Promise.lift (box {| data = [||] |}))
                                "prompt",
                                box (fun (_arg: obj) ->
                                    promptCallCount <- promptCallCount + 1
                                    // Simulate missing chat.message: never resolve the host receipt waiter.
                                    Promise.lift (box null)) ]
                      ) ]

            let executor = opencodeActionExecutor rt mockClient

            let model =
                { ProviderID = "openai"
                  ModelID = "gpt-5"
                  Variant = None
                  Temperature = None
                  TopP = None
                  MaxTokens = None
                  ReasoningEffort = None
                  Thinking = false }

            rt.UpdateSession(sid, startDispatch model None)
            let continuationId = (rt.GetSession sid).PendingLease.Value.ContinuationID

            let! caught = executor.SendContinue(sid, model, continuationId) |> Promise.result

            match caught with
            | Ok() -> check "expected timeout to throw" false
            | Error ex -> check "timeout error is reported" (ex.Message.Contains("Fallback continuation dispatch failed"))

            check "prompt was called once" (promptCallCount = 1)
        finally
            setReceiptTimeout ""
    }

let run () =
    promise {
        do! opencodeExecutorReceiptTimeoutFreesDispatcherSlot ()
        do! ompFallbackHooksPreservesAgentAndModelSpec ()
        do! ompCaptureCurrentModelReturnsModelWhenSessionHasModelSpec ()
        do! opencodeExecutorUsesRuntimeAgentWhenNoAssistantMessageSpec ()
        do! opencodeExecutorRespectsUserSelectedModelAndAgentSpec ()
        do! ompExecutorRespectsUserSelectedModelAndAgentSpec ()
    }
