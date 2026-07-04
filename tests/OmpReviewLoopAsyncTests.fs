module Wanxiangshu.Tests.OmpReviewLoopAsyncTests
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Omp.ReviewLoop
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.RuntimeScope

let private testScope = RuntimeScope()

/// Regression: review loop must complete via async callback wakeup, not 50ms polling.
///
/// Construct a child session where `prompt` returns immediately WITHOUT
/// resolving the pending review. The resolve happens asynchronously (via
/// `store.resolvePendingReview` called from the test after a short delay).
/// The test asserts:
///   1. The loop completes with the correct verdict.
///   2. `waitForIdle` was never called — proving the loop depends on
///      event-driven callback wakeup, not polling + waitForIdle fallback.
///
/// Under the current polling implementation, `waitForIdle` IS called
/// (at least once) before the async resolve fires, so assertion 2 fails.
/// Under the event-driven implementation, the callback directly wakes the
/// loop without ever calling `waitForIdle`, so both assertions pass.
let runReviewLoopResolvesViaAsyncCallbackNotPolling () = promise {
    testScope.Remove "omp.coding_agent_module"
    let childId = "review-child-async"
    let store = createReviewStore ()
    let waitForIdleCalls = ref 0
    let promptDefer =
        box(fun (_: obj) ->
            emitJsExpr () "Promise.resolve()"
            |> unbox<JS.Promise<unit>>)
    let session =
        createObj [
            "sessionManager", box(createObj [ "getSessionId", box(fun () -> box childId) ])
            "prompt", promptDefer
            "waitForIdle", box(fun () ->
                waitForIdleCalls.Value <- waitForIdleCalls.Value + 1
                emitJsExpr () "new Promise(() => {})"
                |> unbox<JS.Promise<unit>>)
            "abort", box(fun () -> ())
        ]
    let createAgentSession =
        box(fun (_body: obj) ->
            emitJsExpr session
                """Promise.resolve({ session: $0, dispose: null })"""
            |> unbox<JS.Promise<obj>>)
    let pi = createObj [ "pi", box(createObj [ "createAgentSession", createAgentSession ]) ]
    testScope.Add("omp.coding_agent_module",
        box (createObj [
            "SessionManager",
                box(
                    createObj [
                        "create", box(fun (_cwd: string) -> createObj [ "getSessionId", box(fun () -> box "sm-1") ])
                    ])
        ]))
    let ctx = createObj [ "cwd", box "/tmp/ws" ]
    let! outcome =
        promise {
            let loop = runReviewLoop testScope pi ctx store "parent-async" "report body" [| "src/a.fs" |] (Some "fix")
            let! _ = Promise.sleep 50
            store.resolvePendingReview(childId, Accepted "") |> ignore
            return! loop
        }
    check "async-resolve accepted" (defaultArg outcome.accepted false)
    check "async-resolve not terminated" (not (defaultArg outcome.terminated false))
    check "async-resolve no waitForIdle polling" (waitForIdleCalls.Value = 0)
}

/// Regression: nudge loop must fire nudge prompt on timeout and stop when resolved.
///
/// Set grace periods to short values (initial=10ms, subsequent=50ms).
/// The child session prompt returns immediately without resolving.
/// After initial prompt, the first timeout fires a nudge prompt.
/// Then resolve the review asynchronously.
/// Assert: total prompt calls = 2 (initial + 1 nudge), final outcome accepted.
let runReviewLoopSendsNudgeOnTimeoutThenStopsOnResolve () = promise {
    testScope.Remove "omp.coding_agent_module"
    let childId = "review-child-nudge"
    let store = createReviewStore ()
    let promptCalls = ref 0
    let promptDefer =
        box(fun (_: obj) ->
            promptCalls.Value <- promptCalls.Value + 1
            emitJsExpr () "Promise.resolve()"
            |> unbox<JS.Promise<unit>>)
    let session =
        createObj [
            "sessionManager", box(createObj [ "getSessionId", box(fun () -> box childId) ])
            "prompt", promptDefer
            "waitForIdle", box(fun () -> emitJsExpr () "Promise.resolve()" |> unbox<JS.Promise<unit>>)
            "abort", box(fun () -> ())
        ]
    let createAgentSession =
        box(fun (_body: obj) ->
            emitJsExpr session
                """Promise.resolve({ session: $0, dispose: null })"""
            |> unbox<JS.Promise<obj>>)
    let pi = createObj [ "pi", box(createObj [ "createAgentSession", createAgentSession ]) ]
    testScope.Add("omp.coding_agent_module", box (
        createObj [
            "SessionManager",
                box(
                    createObj [
                        "create", box(fun (_cwd: string) -> createObj [ "getSessionId", box(fun () -> box "sm-1") ])
                    ])
        ]))
    let ctx = createObj [ "cwd", box "/tmp/ws" ]
    testScope.Add("review.grace_initial_ms", box 10)
    testScope.Add("review.grace_subsequent_ms", box 50)
    let! outcome =
        promise {
            let loop = runReviewLoop testScope pi ctx store "parent-nudge" "report body" [| "src/a.fs" |] (Some "fix")
            let! _ = Promise.sleep 30
            store.resolvePendingReview(childId, Accepted "") |> ignore
            return! loop
        }
    resetReviewLoopGracePeriods testScope
    check "nudge prompt calls = 2 (initial + 1 nudge)" (promptCalls.Value = 2)
    check "nudge outcome accepted" (defaultArg outcome.accepted false)
    check "nudge outcome not terminated" (not (defaultArg outcome.terminated false))
}
