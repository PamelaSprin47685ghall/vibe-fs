module Wanxiangshu.Tests.OpencodeSessionLifecycleTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Hosts.Opencode.BacklogSession
open Wanxiangshu.Hosts.Opencode.SessionLifecycleObserver
open Wanxiangshu.Runtime.Dyn

let childIdleDoesNotAbortParent () =
    promise {
        let abortCalls = ResizeArray<obj>()
        let promptCalls = ResizeArray<obj>()

        let sessionApi =
            createObj
                [ "abort", box (System.Func<obj, JS.Promise<unit>>(fun arg -> promise { abortCalls.Add arg }))
                  "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg -> promise { promptCalls.Add arg })) ]

        let client = createObj [ "session", box sessionApi ]
        let ctx = createObj [ "client", box client; "directory", box "/proj" ]
        let registry = ChildAgentRegistry.Create()
        registry.RegisterChildAgent("child-1", "coder", Some "parent-1")

        let observer =
            createSessionLifecycleObserver (
                opencode,
                ctx,
                createReviewStore (),
                registry,
                None,
                FallbackRuntimeStore(),
                BacklogSession(opencode, create ())
            )

        do!
            observer.handleEvent (
                createObj
                    [ "event",
                      box (
                          createObj
                              [ "type", box "session.status"
                                "properties",
                                box (
                                    createObj
                                        [ "info", box (createObj [ "sessionID", box "child-1" ])
                                          "status", box (createObj [ "type", box "idle" ]) ]
                                ) ]
                      ) ]
            )

        check "child idle must not abort parent" (abortCalls.Count = 0)
        check "child idle must not continue parent" (promptCalls.Count = 0)
    }

let run () =
    promise { do! childIdleDoesNotAbortParent () }
