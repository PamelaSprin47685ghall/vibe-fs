module Wanxiangshu.Tests.OmpReviewLoopTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Hosts.Omp.ReviewLoop
open Wanxiangshu.Hosts.Omp
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn

let private testScope = RuntimeScope()

let private installCodingAgentModule () =
    testScope.Remove "omp.coding_agent_module"

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

let runReviewLoopChildToolNames () =
    promise {
        installCodingAgentModule ()
        let captured = ref [||]
        let store = createReviewStore ()
        let childId = "review-child-tools"

        let promptAcceptOnFirst =
            box (fun (_: obj) ->
                store.resolvePendingReview (childId, Accepted []) |> ignore
                emitJsExpr () "Promise.resolve()" |> unbox<JS.Promise<unit>>)

        let createAgentSession =
            box (fun (body: obj) ->
                captured.Value <- unbox<string array> (Dyn.get body "toolNames")

                emitJsExpr
                    promptAcceptOnFirst
                    """Promise.resolve({
                    session: {
                        sessionManager: { getSessionId: () => 'review-child-tools' },
                        prompt: (msg) => $0(msg),
                        waitForIdle: () => Promise.resolve(),
                        abort: () => {}
                    },
                    dispose: null
                })"""
                |> unbox<JS.Promise<obj>>)

        let pi =
            createObj [ "pi", box (createObj [ "createAgentSession", createAgentSession ]) ]

        let ctx = createObj [ "cwd", box "/tmp/ws" ]
        let! _ = runReviewLoop testScope pi ctx store "parent-tools" "report" [||] (Some "task")
        equal "runReviewLoop tool count" ompReviewChildToolNames.Length captured.Value.Length

        for i in 0 .. ompReviewChildToolNames.Length - 1 do
            equal ("runReviewLoop tool " + string i) ompReviewChildToolNames.[i] captured.Value.[i]
    }

let runReviewLoopAcceptsWhenPendingResolved () =
    promise {
        installCodingAgentModule ()
        let childId = "review-child-accept"
        let store = createReviewStore ()

        let promptResolve =
            box (fun (_: obj) ->
                store.resolvePendingReview (childId, Accepted []) |> ignore
                emitJsExpr () "Promise.resolve()" |> unbox<JS.Promise<unit>>)

        let session =
            createObj
                [ "sessionManager", box (createObj [ "getSessionId", box (fun () -> box childId) ])
                  "prompt", promptResolve
                  "waitForIdle", box (fun () -> emitJsExpr () "Promise.resolve()" |> unbox<JS.Promise<unit>>)
                  "abort", box (fun () -> ()) ]

        let createAgentSession =
            box (fun (_body: obj) ->
                emitJsExpr session """Promise.resolve({ session: $0, dispose: null })"""
                |> unbox<JS.Promise<obj>>)

        let pi =
            createObj [ "pi", box (createObj [ "createAgentSession", createAgentSession ]) ]

        let ctx = createObj [ "cwd", box "/tmp/ws" ]
        let! outcome = runReviewLoop testScope pi ctx store "parent-accept" "report body" [| "src/a.fs" |] (Some "fix")

        match outcome with
        | Accepted fb -> check "accepted no feedback" (fb = [])
        | _ -> check "accepted outcome" false
    }
