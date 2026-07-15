module Wanxiangshu.Tests.FallbackAgentAndModelPart2Tests

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

let opencodeCaptureCurrentModelDecodesStringModelSpec () =
    promise {
        let rt = FallbackRuntimeState()

        let mockClient =
            createObj [ "session", box (createObj [ "model", box "openai/gpt-4o" ]) ]

        let executor = opencodeActionExecutor rt mockClient
        let! modelOpt = executor.CaptureCurrentModel "opencode-string-model"
        check "model is captured" modelOpt.IsSome
        equal "provider is openai" "openai" modelOpt.Value.ProviderID
        equal "modelId is gpt-4o" "gpt-4o" modelOpt.Value.ModelID
    }

let ompCaptureCurrentModelDecodesStringModelSpec () =
    promise {
        let rt = FallbackRuntimeState()
        let sid = "omp-string-model"
        let mockSession = createObj [ "model", box "anthropic/claude-3-5" ]
        Wanxiangshu.Omp.ExecutorTools.ompScope.Add("omp_session_" + sid, mockSession)
        let executor = ompActionExecutor rt null
        let! modelOpt = executor.CaptureCurrentModel sid
        check "model is captured" modelOpt.IsSome
        equal "provider is anthropic" "anthropic" modelOpt.Value.ProviderID
        equal "modelId is claude-3-5" "claude-3-5" modelOpt.Value.ModelID
        Wanxiangshu.Omp.ExecutorTools.ompScope.Remove("omp_session_" + sid)
    }

let opencodeCaptureCurrentModelDecodesFromUserMessageSpec () =
    promise {
        let rt = FallbackRuntimeState()

        let mockClient =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "messages",
                            box (fun _ ->
                                Promise.lift (
                                    box
                                        {| data =
                                            [| box
                                                   {| info =
                                                       box
                                                           {| role = "user"
                                                              model =
                                                               box
                                                                   {| providerID = "openai"
                                                                      modelID = "gpt-4o" |} |}
                                                      parts = [||] |} |] |}
                                )) ]
                  ) ]

        let executor = opencodeActionExecutor rt mockClient
        let! modelOpt = executor.CaptureCurrentModel "opencode-user-message-model"
        check "model is captured from user message" modelOpt.IsSome
        equal "provider is openai" "openai" modelOpt.Value.ProviderID
        equal "modelId is gpt-4o" "gpt-4o" modelOpt.Value.ModelID
    }

let opencodeSessionStatusCapturesActiveModelSpec () =
    promise {
        let rt = FallbackRuntimeState()
        let sid = "session-status-model"

        let statusObj =
            createObj [ "type", box "busy"; "agent", box "reviewer"; "model", box "openai/gpt-4o" ]

        let rawEvent =
            createObj
                [ "event",
                  box (
                      createObj
                          [ "type", box "session.status"
                            "properties",
                            box (
                                createObj [ "info", box (createObj [ "sessionID", box sid ]); "status", box statusObj ]
                            ) ]
                  ) ]

        let registry = ChildAgentRegistry.Create()

        let observer =
            createSessionLifecycleObserver (
                Opencode,
                null,
                createReviewStore (),
                registry,
                None,
                rt,
                BacklogSession(Opencode, create ())
            )

        do! observer.handleEvent rawEvent
        equal "agent is captured" "reviewer" (rt.GetAgentName sid)
        let modelOpt = rt.GetModel sid
        check "model is captured from runtime" modelOpt.IsSome
        equal "provider is openai" "openai" modelOpt.Value.ProviderID
        equal "modelId is gpt-4o" "gpt-4o" modelOpt.Value.ModelID
    }

let opencodeCaptureCurrentModelPrioritizesLatestUserMessageModelSpec () =
    promise {
        let rt = FallbackRuntimeState()
        let sid = "priority-user-model"

        rt.SetModel
            sid
            { ProviderID = "openai"
              ModelID = "gpt-4-busy"
              Variant = None
              Temperature = None
              TopP = None
              MaxTokens = None
              ReasoningEffort = None
              Thinking = false }

        let mockClient =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "messages",
                            box (fun _ ->
                                Promise.lift (
                                    box
                                        {| data =
                                            [| box
                                                   {| info =
                                                       box
                                                           {| role = "user"
                                                              model =
                                                               box
                                                                   {| providerID = "anthropic"
                                                                      modelID = "claude-3-5" |} |}
                                                      parts = [||] |} |] |}
                                )) ]
                  ) ]

        let executor = opencodeActionExecutor rt mockClient
        let! modelOpt = executor.CaptureCurrentModel sid
        check "model is captured" modelOpt.IsSome
        equal "should prioritize user message model" "anthropic" modelOpt.Value.ProviderID
        equal "should prioritize user message model id" "claude-3-5" modelOpt.Value.ModelID
    }

let opencodeNewUserMessageResetsChainAndModelSpec () =
    promise {
        let rt = FallbackRuntimeState()
        let sid = "reset-chain-model"

        let testModel =
            { ProviderID = "openai"
              ModelID = "gpt-4"
              Variant = None
              Temperature = None
              TopP = None
              MaxTokens = None
              ReasoningEffort = None
              Thinking = false }

        rt.SetChain sid [ testModel ]
        rt.SetModel sid testModel

        let rawEvent =
            createObj
                [ "event",
                  box (
                      createObj
                          [ "type", box "message.updated"
                            "properties",
                            box (createObj [ "info", box (createObj [ "sessionID", box sid; "role", box "user" ]) ]) ]
                  ) ]

        let registry = ChildAgentRegistry.Create()

        let configLookup =
            fun _ ->
                { LoopMaxContinues = 3
                  MaxRetries = 3
                  MaxRecoveries = 3
                  AgentChains = Map.empty
                  DefaultChain = [] }

        let mockClient =
            createObj [ "session", box (createObj [ "messages", box (fun _ -> Promise.lift (box {| data = [||] |})) ]) ]

        let handler =
            createOpencodeFallbackHandler mockClient rt configLookup "" registry (createReviewStore ()) None

        let observer =
            createSessionLifecycleObserver (
                Opencode,
                null,
                createReviewStore (),
                registry,
                Some handler,
                rt,
                BacklogSession(Opencode, create ())
            )

        do! observer.OnNewHumanMessage(sid, "manager", None, "msg-1")

        let chain = rt.GetChain sid
        let modelOpt = rt.GetModel sid
        equal "chain should be cleared" 0 chain.Length
        check "model should be cleared" modelOpt.IsNone
    }

let opencodeCaptureCurrentModelPrioritizesSessionGetAsyncSpec () =
    promise {
        let rt = FallbackRuntimeState()
        let sid = "manual-model-async-priority"

        // Mock client: client.session.get({ sessionID }) returns { data: { model: "openai/gpt-4o" } }
        let mockSessionGet =
            System.Func<obj, JS.Promise<obj>>(fun _ ->
                Promise.lift (box {| data = box {| model = box "openai/gpt-4o" |} |}))

        let mockClient =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "get", box mockSessionGet
                            "model", box null
                            "messages", box (fun _ -> Promise.lift (box {| data = [||] |})) ]
                  ) ]

        let executor = opencodeActionExecutor rt mockClient
        let! modelOpt = executor.CaptureCurrentModel sid

        check "model captured from session.get() async" modelOpt.IsSome
        equal "provider openai" "openai" modelOpt.Value.ProviderID
        equal "modelId gpt-4o" "gpt-4o" modelOpt.Value.ModelID
    }

let run () =
    promise {
        do! opencodeCaptureCurrentModelDecodesStringModelSpec ()
        do! ompCaptureCurrentModelDecodesStringModelSpec ()
        do! opencodeCaptureCurrentModelDecodesFromUserMessageSpec ()
        do! opencodeSessionStatusCapturesActiveModelSpec ()
        do! opencodeCaptureCurrentModelPrioritizesLatestUserMessageModelSpec ()
        do! opencodeNewUserMessageResetsChainAndModelSpec ()
        do! opencodeCaptureCurrentModelPrioritizesSessionGetAsyncSpec ()
    }
