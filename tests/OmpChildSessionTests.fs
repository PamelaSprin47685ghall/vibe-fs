module Wanxiangshu.Tests.OmpChildSessionTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackRecoveryWait
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Tests.AsyncFlush

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

let runSubagentOnExistingSessionResetsTaskComplete () =
    promise {
        let rt = FallbackRuntimeState()
        let childId = "omp-child-continue-reset"
        let s0 = rt.GetOrCreateState childId
        rt.UpdateState childId { s0 with TaskComplete = true }

        let promptCalled = ref false
        let sessionManagerMock = createObj [ "getSessionId", box (fun () -> box childId) ]

        let sessionMock =
            createObj
                [ "prompt",
                  box (fun (_p: string) ->
                      promptCalled.Value <- true
                      Promise.lift ())
                  "waitForIdle", box (fun () -> Promise.lift ())
                  "sessionManager", box sessionManagerMock ]

        let scope = RuntimeScope()
        scope.Add("omp_session_" + childId, sessionMock)

        let pi = createObj []
        let ctx = createObj [ "sessionId", box "parent-omp-session" ]

        let config =
            { DefaultChain =
                [ { ProviderID = "test"
                    ModelID = "test-model"
                    Variant = None
                    Temperature = None
                    TopP = None
                    MaxTokens = None
                    ReasoningEffort = None
                    Thinking = false } ]
              AgentChains = Map.empty
              MaxRetries = 2
              LoopMaxContinues = 3
              MaxRecoveries = 5 }

        let runP =
            Wanxiangshu.Omp.ChildSessionRegistry.runSubagentOnExistingSession
                scope
                pi
                ctx
                childId
                "continue prompt"
                None
                rt
                (Some config)

        do! yieldMicrotask ()
        check "OMP continue resets TaskComplete to false" (not (rt.GetOrCreateState childId).TaskComplete)

        rt.SetTaskComplete childId true
        let! text = runP
        equal "OMP continue gets output" "(no output)" text
    }

let run () =
    promise {
        do! createChildSessionReviewToolNames ()
        do! createChildSessionRunnerToolNames ()
        do! runSubagentOnExistingSessionResetsTaskComplete ()
    }
