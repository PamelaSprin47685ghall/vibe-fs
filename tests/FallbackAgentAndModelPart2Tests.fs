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

let run () =
    promise {
        do! opencodeCaptureCurrentModelDecodesStringModelSpec ()
        do! ompCaptureCurrentModelDecodesStringModelSpec ()
        do! opencodeCaptureCurrentModelDecodesFromUserMessageSpec ()
        do! opencodeSessionStatusCapturesActiveModelSpec ()
    }
