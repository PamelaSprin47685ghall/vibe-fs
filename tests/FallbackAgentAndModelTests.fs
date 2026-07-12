module Wanxiangshu.Tests.FallbackAgentAndModelTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Opencode.BacklogSession
open Wanxiangshu.Opencode.SessionLifecycleObserver
open Wanxiangshu.Omp.FallbackHooks
open Wanxiangshu.Opencode.FallbackHooks
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

let ompFallbackHooksPreservesAgentAndModelSpec () =
    promise {
        let rt = FallbackRuntimeState()
        let sid = "omp-subagent-sess"
        rt.SetAgentName sid "coder"

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
        let rt = FallbackRuntimeState()
        let sid = "omp-sess-with-model"

        let mockSession =
            createObj [ "model", box (createObj [ "provider", box "anthropic"; "id", box "claude-4" ]) ]

        Wanxiangshu.Omp.ExecutorTools.ompScope.Add("omp_session_" + sid, mockSession)

        let executor = ompActionExecutor rt null
        let! modelOpt = executor.CaptureCurrentModel sid

        check "model is captured" modelOpt.IsSome
        let m = modelOpt.Value
        equal "provider is anthropic" "anthropic" m.ProviderID
        equal "modelId is claude-4" "claude-4" m.ModelID

        Wanxiangshu.Omp.ExecutorTools.ompScope.Remove("omp_session_" + sid)
    }

let opencodeExecutorUsesRuntimeAgentWhenNoAssistantMessageSpec () =
    promise {
        let rt = FallbackRuntimeState()
        let sid = "opencode-no-assistant"
        rt.SetAgentName sid "investigator"

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
        equal "body has agent investigator" "investigator" (Dyn.str body "agent")
        let parts = unbox<obj array> (Dyn.get body "parts")
        equal "body prompt text is zero-width" "​" (Dyn.str parts.[0] "text")
    }

let opencodeExecutorRespectsUserSelectedModelAndAgentSpec () =
    promise {
        let rt = FallbackRuntimeState()
        let sid = "opencode-user-selected"
        rt.SetAgentName sid "investigator"

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
        let rt = FallbackRuntimeState()
        let sid = "omp-user-selected"
        rt.SetAgentName sid "coder"

        let mockSession =
            createObj
                [ "model", box (createObj [ "provider", box "google"; "id", box "gemini-2" ])
                  "agent", box "reviewer" ]

        Wanxiangshu.Omp.ExecutorTools.ompScope.Add("omp_session_" + sid, mockSession)

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

        Wanxiangshu.Omp.ExecutorTools.ompScope.Remove("omp_session_" + sid)
    }

let run () =
    promise {
        do! ompFallbackHooksPreservesAgentAndModelSpec ()
        do! ompCaptureCurrentModelReturnsModelWhenSessionHasModelSpec ()
        do! opencodeExecutorUsesRuntimeAgentWhenNoAssistantMessageSpec ()
        do! opencodeExecutorRespectsUserSelectedModelAndAgentSpec ()
        do! ompExecutorRespectsUserSelectedModelAndAgentSpec ()
    }
