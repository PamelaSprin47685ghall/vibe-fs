module Wanxiangshu.Tests.OpencodeSessionLifecycleTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Hosts.Opencode.SessionLifecycleObserver
open Wanxiangshu.Runtime.Dyn

let childIdleDoesNotAbortParent () =
    promise {
        let! directory = mkdtempAsync "opencode-child-idle-"
        let abortCalls = ResizeArray<obj>()
        let promptCalls = ResizeArray<obj>()

        let sessionApi =
            createObj
                [ "abort", box (System.Func<obj, JS.Promise<unit>>(fun arg -> promise { abortCalls.Add arg }))
                  "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg -> promise { promptCalls.Add arg })) ]

        let client = createObj [ "session", box sessionApi ]
        let ctx = createObj [ "client", box client; "directory", box directory ]
        let registry = ChildAgentRegistry.Create()
        registry.RegisterChildAgent("child-1", "coder", Some "parent-1")

        let observer =
            createSessionLifecycleObserver (opencode, ctx, createReviewStore (), registry, None, FallbackRuntimeStore())

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
        do! rmAsync directory
    }

let childCompactionIdleSettlesAfterFallbackConsumes () =
    promise {
        let! directory = mkdtempAsync "opencode-child-compaction-"
        let sessionID = "child-compaction-1"
        let runtime = FallbackRuntimeStore()
        runtime.UpdateSession(sessionID, transferOwnership SessionOwner.Compaction)
        runtime.UpdateSession(sessionID, setActiveCompactionId "compact-1" 1 "" 0)
        runtime.Update(sessionID, setCompacted true)
        runtime.Update(sessionID, setCompactionContinuationObserved true)

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
                runtime
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

        equal "child compaction owner is released" SessionOwner.NoOwner ((runtime.GetSession sessionID).Owner)
        check "compaction identity is cleared" ((runtime.GetSession sessionID).CompactionActiveId = "")
        do! rmAsync directory
    }

let run () =
    promise {
        do! childIdleDoesNotAbortParent ()
        do! childCompactionIdleSettlesAfterFallbackConsumes ()
    }
