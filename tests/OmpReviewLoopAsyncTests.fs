module Wanxiangshu.Tests.OmpReviewLoopAsyncTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Hosts.Omp.ReviewLoop
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.RuntimeScope

let private testScope = RuntimeScope()

/// Regression: review loop must complete via async callback wakeup, not polling.
///
/// Construct a child session where `prompt` returns immediately WITHOUT
/// resolving the pending review. The resolve happens asynchronously (via
/// `store.resolvePendingReview` called from the test after a microtask yield).
/// The test asserts:
///   1. The loop completes with the correct verdict.
///   2. `waitForIdle` was never called - proving the loop depends on
///      event-driven callback wakeup, not polling.
let runReviewLoopResolvesViaAsyncCallbackNotPolling () =
    promise {
        testScope.Remove "omp.coding_agent_module"
        let childId = "review-child-async"
        let store = createReviewStore ()
        let waitForIdleCalls = ref 0

        let promptDefer =
            box (fun (_: obj) -> emitJsExpr () "Promise.resolve()" |> unbox<JS.Promise<unit>>)

        let session =
            createObj
                [ "sessionManager", box (createObj [ "getSessionId", box (fun () -> box childId) ])
                  "prompt", promptDefer
                  "waitForIdle",
                  box (fun () ->
                      waitForIdleCalls.Value <- waitForIdleCalls.Value + 1
                      emitJsExpr () "new Promise(() => {})" |> unbox<JS.Promise<unit>>)
                  "abort", box (fun () -> ()) ]

        let createAgentSession =
            box (fun (_body: obj) ->
                emitJsExpr session """Promise.resolve({ session: $0, dispose: null })"""
                |> unbox<JS.Promise<obj>>)

        let pi =
            createObj [ "pi", box (createObj [ "createAgentSession", createAgentSession ]) ]

        testScope.Add(
            "omp.coding_agent_module",
            box (
                createObj
                    [ "SessionManager",
                      box (
                          createObj
                              [ "create",
                                box (fun (_cwd: string) -> createObj [ "getSessionId", box (fun () -> box "sm-1") ]) ]
                      ) ]
            )
        )

        let ctx = createObj [ "cwd", box "/tmp/ws" ]

        let waitForPending () =
            Promise.create (fun resolve _ ->
                let checkPendingRef = ref (fun () -> ())

                checkPendingRef.Value <-
                    fun () ->
                        let ids = store.getPendingReviewIds ()

                        if ids |> List.contains childId then
                            resolve ()
                        else
                            let cb = checkPendingRef.Value
                            emitJsStatement cb "queueMicrotask($0)"

                checkPendingRef.Value())

        let! outcome =
            promise {
                let loop =
                    runReviewLoop testScope pi ctx store "parent-async" "report body" [| "src/a.fs" |] (Some "fix")

                do! waitForPending ()
                store.resolvePendingReview (childId, Accepted "") |> ignore
                return! loop
            }

        match outcome with
        | Accepted _ -> check "async-resolve accepted" true
        | _ -> check "async-resolve accepted" false

        check "async-resolve no waitForIdle polling" (waitForIdleCalls.Value = 0)
    }

/// Regression: nudge loop must fire nudge prompt on idle and stop when resolved.
///
/// The child session prompt returns immediately without resolving.
/// After initial prompt completes, the first nudge fires (event-driven).
/// Then resolve the review asynchronously.
/// Assert: total prompt calls = 2 (initial + 1 nudge), final outcome accepted.
let runReviewLoopSendsNudgeOnTimeoutThenStopsOnResolve () =
    promise {
        testScope.Remove "omp.coding_agent_module"
        let childId = "review-child-nudge"
        let store = createReviewStore ()
        let promptCalls = ref 0

        let promptDefer =
            box (fun (_: obj) ->
                promptCalls.Value <- promptCalls.Value + 1
                emitJsExpr () "Promise.resolve()" |> unbox<JS.Promise<unit>>)

        let session =
            createObj
                [ "sessionManager", box (createObj [ "getSessionId", box (fun () -> box childId) ])
                  "prompt", promptDefer
                  "waitForIdle", box (fun () -> emitJsExpr () "Promise.resolve()" |> unbox<JS.Promise<unit>>)
                  "abort", box (fun () -> ()) ]

        let createAgentSession =
            box (fun (_body: obj) ->
                emitJsExpr session """Promise.resolve({ session: $0, dispose: null })"""
                |> unbox<JS.Promise<obj>>)

        let pi =
            createObj [ "pi", box (createObj [ "createAgentSession", createAgentSession ]) ]

        testScope.Add(
            "omp.coding_agent_module",
            box (
                createObj
                    [ "SessionManager",
                      box (
                          createObj
                              [ "create",
                                box (fun (_cwd: string) -> createObj [ "getSessionId", box (fun () -> box "sm-1") ]) ]
                      ) ]
            )
        )

        let ctx = createObj [ "cwd", box "/tmp/ws" ]

        let waitForPending () =
            Promise.create (fun resolve _ ->
                let checkPendingRef = ref (fun () -> ())

                checkPendingRef.Value <-
                    fun () ->
                        let ids = store.getPendingReviewIds ()

                        if ids |> List.contains childId then
                            resolve ()
                        else
                            let cb = checkPendingRef.Value
                            emitJsStatement cb "queueMicrotask($0)"

                checkPendingRef.Value())

        testScope.Add("review.grace_initial_ms", box 10) |> ignore // deprecated but keep for structure
        testScope.Add("review.grace_subsequent_ms", box 50) |> ignore

        let! outcome =
            promise {
                let loop =
                    runReviewLoop testScope pi ctx store "parent-nudge" "report body" [| "src/a.fs" |] (Some "fix")

                do! waitForPending ()
                store.resolvePendingReview (childId, Accepted "") |> ignore
                return! loop
            }

        check "nudge prompt calls = 2 (initial + 1 nudge)" (promptCalls.Value = 2)

        match outcome with
        | Accepted _ -> check "nudge outcome accepted" true
        | _ -> check "nudge outcome accepted" false
    }

/// R-03: exception during nudge loop still aborts+disposes the child.
let runReviewLoop_finallyCleansUpOnPromptError () =
    promise {
        testScope.Remove "omp.coding_agent_module"
        let childId = "review-child-cleanup-err"
        let store = createReviewStore ()
        let abortCalls = ref 0
        let disposeCalls = ref 0

        let session =
            createObj
                [ "sessionManager", box (createObj [ "getSessionId", box (fun () -> box childId) ])
                  "prompt", box (fun (_: obj) -> Promise.reject (exn "omp prompt boom"))
                  "waitForIdle", box (fun () -> emitJsExpr () "Promise.resolve()" |> unbox<JS.Promise<unit>>)
                  "abort", box (fun () -> abortCalls.Value <- abortCalls.Value + 1) ]

        let disposeFn = box (fun () -> disposeCalls.Value <- disposeCalls.Value + 1)

        let createAgentSession =
            box (fun (_body: obj) ->
                Promise.lift (createObj [ "session", session; "dispose", disposeFn ]))

        let pi =
            createObj [ "pi", box (createObj [ "createAgentSession", createAgentSession ]) ]

        testScope.Add(
            "omp.coding_agent_module",
            box (
                createObj
                    [ "SessionManager",
                      box (
                          createObj
                              [ "create",
                                box (fun (_cwd: string) -> createObj [ "getSessionId", box (fun () -> box "sm-1") ]) ]
                      ) ]
            )
        )

        let ctx = createObj [ "cwd", box "/tmp/ws" ]

        // Prompt rejects immediately; nudge race treats rejection as unhandled unless
        // mapped. Attach pending so attach path runs, then force loop error via reject.
        let mutable sawError = false

        try
            let! _ = runReviewLoop testScope pi ctx store "parent-cleanup" "report" [| "a.fs" |] (Some "task")
            ()
        with _ ->
            sawError <- true

        // Even if loop maps error to Terminated without throw, cleanup must run.
        check "omp child abort on error path" (abortCalls.Value >= 1)
        check "omp child dispose on error path" (disposeCalls.Value >= 1)
        check "omp pending cleared" (store.getPendingReviewIds () |> List.contains childId |> not)
        ignore sawError
    }
