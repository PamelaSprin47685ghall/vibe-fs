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

module Dyn = Wanxiangshu.Runtime.Dyn

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

        do! executor.SendContinue(sid, model, "dummy")

        check "prompt was called" (not (isNull lastPromptArg))
        let body = Dyn.get lastPromptArg "body"
        equal "body has agent inspector" "inspector" (Dyn.str body "agent")
        let parts = unbox<obj array> (Dyn.get body "parts")
        equal "body prompt text is zero-width" "​" (Dyn.str parts.[0] "text")
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

        do! executor.SendContinue(sid, model, "dummy")

        check "prompt was called" (not (isNull lastPromptArg))
        let body = Dyn.get lastPromptArg "body"
        equal "body has user selected agent reviewer" "reviewer" (Dyn.str body "agent")
        let modelObj = Dyn.get body "model"
        equal "body has fallback model provider" "openai" (Dyn.str modelObj "providerID")
        equal "body has fallback model ID" "gpt-5" (Dyn.str modelObj "modelID")
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

let run () =
    promise {
        do! ompFallbackHooksPreservesAgentAndModelSpec ()
        do! ompCaptureCurrentModelReturnsModelWhenSessionHasModelSpec ()
        do! opencodeExecutorUsesRuntimeAgentWhenNoAssistantMessageSpec ()
        do! opencodeExecutorRespectsUserSelectedModelAndAgentSpec ()
        do! ompExecutorRespectsUserSelectedModelAndAgentSpec ()
    }
