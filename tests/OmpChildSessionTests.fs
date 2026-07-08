module Wanxiangshu.Tests.OmpChildSessionTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.RuntimeScope

module Dyn = Wanxiangshu.Shell.Dyn

let private testScope = RuntimeScope()

let private reset () =
    testScope.Remove "omp.coding_agent_module"

let private mockPi (captureToolNames: string array ref) : obj =
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

    let createAgentSession =
        box (fun (body: obj) ->
            let names = unbox<string array> (Dyn.get body "toolNames")
            captureToolNames.Value <- names

            emitJsExpr
                ()
                """Promise.resolve({
                    session: { sessionManager: { getSessionId: () => 'child-1' } },
                    dispose: null
                })"""
            |> unbox<JS.Promise<obj>>)

    let inner = createObj [ "createAgentSession", createAgentSession ]
    createObj [ "pi", box inner ]

let createChildSessionReviewToolNames () =
    promise {
        reset ()
        let captured = ref [||]
        let pi = mockPi captured
        let ctx = createObj [ "cwd", box "/tmp/ws" ]
        let! _ = createChildSession testScope pi ctx ompReviewChildToolNames None [||] None
        equal "review child tool count" ompReviewChildToolNames.Length captured.Value.Length

        for i in 0 .. ompReviewChildToolNames.Length - 1 do
            equal ("review child tool " + string i) ompReviewChildToolNames.[i] captured.Value.[i]
    }

let createChildSessionRunnerToolNames () =
    promise {
        reset ()
        let captured = ref [||]
        let pi = mockPi captured
        let ctx = createObj [ "cwd", box "/tmp/ws" ]
        let! _ = createChildSession testScope pi ctx ompRunnerChildToolNames None [||] None
        equal "runner child tool count" ompRunnerChildToolNames.Length captured.Value.Length

        for i in 0 .. ompRunnerChildToolNames.Length - 1 do
            equal ("runner child tool " + string i) ompRunnerChildToolNames.[i] captured.Value.[i]
    }
