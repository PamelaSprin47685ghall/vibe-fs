module VibeFs.Tests.OmpChildSessionTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.OmpSessionTools
open VibeFs.Omp.ChildSession
open VibeFs.Omp.PiResolve
open VibeFs.Shell.Dyn
module Dyn = VibeFs.Shell.Dyn

let private reset () = clearCodingAgentModuleForTest ()

let private mockPi (captureToolNames: string array ref) : obj =
    setCodingAgentModuleForTest (
        createObj [
            "SessionManager",
                box(
                    createObj [
                        "create", box(fun (_cwd: string) -> createObj [ "getSessionId", box(fun () -> box "sm-1") ])
                    ])
        ])
    let createAgentSession =
        box(fun (body: obj) ->
            let names = unbox<string array> (Dyn.get body "toolNames")
            captureToolNames.Value <- names
            emitJsExpr ()
                """Promise.resolve({
                    session: { sessionManager: { getSessionId: () => 'child-1' } },
                    dispose: null
                })"""
            |> unbox<JS.Promise<obj>>)
    let inner = createObj [ "createAgentSession", createAgentSession ]
    createObj [ "pi", box inner ]

let createChildSessionReviewToolNames () = promise {
    reset ()
    let captured = ref [||]
    let pi = mockPi captured
    let ctx = createObj [ "cwd", box "/tmp/ws" ]
    let! _ = createChildSession pi ctx ompReviewChildToolNames None [||]
    equal "review child tool count" ompReviewChildToolNames.Length captured.Value.Length
    for i in 0 .. ompReviewChildToolNames.Length - 1 do
        equal ("review child tool " + string i) ompReviewChildToolNames.[i] captured.Value.[i]
}

let createChildSessionRunnerToolNames () = promise {
    reset ()
    let captured = ref [||]
    let pi = mockPi captured
    let ctx = createObj [ "cwd", box "/tmp/ws" ]
    let! _ = createChildSession pi ctx ompRunnerChildToolNames None [||]
    equal "runner child tool count" ompRunnerChildToolNames.Length captured.Value.Length
    for i in 0 .. ompRunnerChildToolNames.Length - 1 do
        equal ("runner child tool " + string i) ompRunnerChildToolNames.[i] captured.Value.[i]
}