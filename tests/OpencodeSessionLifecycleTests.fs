module Wanxiangshu.Tests.OpencodeSessionLifecycleTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Opencode.BacklogSession
open Wanxiangshu.Opencode.SessionLifecycleObserver
open Wanxiangshu.Shell.Dyn

let childIdleDoesNotAbortParent () = promise {
    let abortCalls = ResizeArray<obj>()
    let promptCalls = ResizeArray<obj>()
    let sessionApi = createObj [
        "abort", box (System.Func<obj, JS.Promise<unit>>(fun arg -> promise { abortCalls.Add arg }))
        "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg -> promise { promptCalls.Add arg }))
    ]
    let client = createObj [ "session", box sessionApi ]
    let ctx = createObj [ "client", box client; "directory", box "/proj" ]
    let registry = ChildAgentRegistry.Create()
    registry.RegisterChildAgent("child-1", "coder", Some "parent-1")
    let observer = createSessionLifecycleObserver(opencode, ctx, createReviewStore(), registry, None, FallbackRuntimeState(), BacklogSession(opencode, create ()))

    do! observer.handleEvent(createObj [
        "event", box (createObj [
            "type", box "session.status"
            "properties", box (createObj [
                "info", box (createObj [ "sessionID", box "child-1" ])
                "status", box (createObj [ "type", box "idle" ])
            ])
        ])
    ])

    check "child idle must not abort parent" (abortCalls.Count = 0)
    check "child idle must not continue parent" (promptCalls.Count = 0)
}

let run () = promise {
    do! childIdleDoesNotAbortParent ()
}
