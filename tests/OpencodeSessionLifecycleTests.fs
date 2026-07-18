module Wanxiangshu.Tests.OpencodeSessionLifecycleTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.CompactionTransitions
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Tests.TempWorkspace
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

        equal "child idle must not abort parent" 0 abortCalls.Count
        equal "child idle must not continue parent" 0 promptCalls.Count
    }

let childCompactionIdleSettlesAfterFallbackConsumes () =
    promise {
        let! directory = mkdtempAsync "opencode-child-compaction-"
        let sessionID = "child-compaction-1"
        let runtime = FallbackRuntimeStore()
        runtime.SetSessionOwner sessionID SessionOwner.Compaction
        runtime.SetActiveCompactionId(sessionID, "compact-1", 1)
        runtime.SetCompacted(sessionID, true)
        runtime.SetCompactionContinuationObserved(sessionID, true)

        let fallbackHandler (_: obj) : JS.Promise<FallbackHookResult> =
            Promise.lift
                { Consumed = true
                  State = runtime.GetOrCreateState sessionID }

        let ctx = createObj [ "client", box (createObj []); "directory", box directory ]
        let registry = ChildAgentRegistry.Create()
        registry.RegisterChildAgent(sessionID, "coder", Some "parent-1")

        let observer =
            createSessionLifecycleObserver (
                opencode,
                ctx,
                createReviewStore (),
                registry,
                Some fallbackHandler,
                runtime,
                BacklogSession(opencode, create ())
            )

        do!
            observer.handleEvent (
                createObj
                    [ "event",
                      box (
                          createObj
                              [ "type", box "session.idle"
                                "properties", box (createObj [ "sessionID", box sessionID ]) ]
                      ) ]
            )

        equal "child compaction owner is released" SessionOwner.NoOwner (runtime.GetSessionOwner sessionID)
        check "compaction identity is cleared" (runtime.GetActiveCompactionId sessionID = "")
        do! rmAsync directory
    }

let run () =
    promise {
        do! childIdleDoesNotAbortParent ()
        do! childCompactionIdleSettlesAfterFallbackConsumes ()
    }
