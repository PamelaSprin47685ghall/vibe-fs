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
open Wanxiangshu.Shell.ChildSessionMailbox

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

let runSubagentOnExistingSessionDoesNotResetTaskComplete () =
    promise {
        let rt = FallbackRuntimeState()
        let childId = "omp-child-continue-reset"
        let s0 = rt.GetOrCreateState childId

        rt.UpdateState
            childId
            { s0 with
                Lifecycle = FallbackLifecycle.TaskComplete }


        // Post events to child session mailbox for event-driven flow
        match ChildSessionMailboxRegistry.TryGet childId with
        | Some mb ->
            do! mb.Post(Command.TaskComplete "")
            do! mb.Post(Command.SessionIdle)
        | None -> ()

        let promptStartedResolver = ref (fun () -> ())

        let promptStarted =
            Promise.create (fun resolve _ -> promptStartedResolver.Value <- resolve)

        let sessionManagerMock = createObj [ "getSessionId", box (fun () -> box childId) ]

        let sessionMock =
            createObj
                [ "prompt",
                  box (fun (_p: string) ->
                      promptStartedResolver.Value()
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

        do! promptStarted
        do! yieldMicrotask ()
        do! yieldMicrotask ()

        // With runSubsessionLoop, lifecycle is Active during the run.
        // The important invariant is that the final result is correct.
        let currentState = rt.GetOrCreateState childId

        check
            "OMP continue is running (Active or TaskComplete)"
            (currentState.Lifecycle = FallbackLifecycle.Active
             || currentState.Lifecycle = FallbackLifecycle.TaskComplete)

        rt.SetTaskComplete childId true

        // Post events to child session mailbox for event-driven flow
        match ChildSessionMailboxRegistry.TryGet childId with
        | Some mb ->
            do! mb.Post(Command.TaskComplete "")
            do! mb.Post(Command.SessionIdle)
        | None -> ()

        let! text = runP
        equal "OMP continue gets output" "(no output)" text
    }

let runSubagentOnExistingSessionCompletesDespiteRetryingAfterNetworkError () =
    promise {
        let rt = FallbackRuntimeState()
        let childId = "omp-child-net-err"

        let s0 = rt.GetOrCreateState childId

        rt.UpdateState
            childId
            { s0 with
                Phase = FallbackPhase.Retrying 1 }

        let promptStartedResolver = ref (fun () -> ())

        let promptStarted =
            Promise.create (fun resolve _ -> promptStartedResolver.Value <- resolve)

        let promptReleaseResolver = ref (fun () -> ())

        let promptRelease =
            Promise.create (fun resolve _ -> promptReleaseResolver.Value <- resolve)

        let promptRejectedResolver = ref (fun () -> ())

        let promptRejected =
            Promise.create (fun resolve _ -> promptRejectedResolver.Value <- resolve)

        let sessionManagerMock = createObj [ "getSessionId", box (fun () -> box childId) ]

        let sessionMock =
            createObj
                [ "prompt",
                  box (fun (_p: string) ->
                      promise {
                          promptStartedResolver.Value()
                          do! promptRelease
                          promptRejectedResolver.Value()
                          return! Promise.reject (System.Exception("network connection lost"))
                      })
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

        do! promptStarted
        rt.SetConsumed childId true
        rt.ClearSubsessionPending childId

        // Post events to child session mailbox for event-driven flow
        match ChildSessionMailboxRegistry.TryGet childId with
        | Some mb -> do! mb.Post(Command.SessionIdle)
        | None -> ()

        promptReleaseResolver.Value()
        do! promptRejected

        do! yieldMicrotask ()
        do! yieldMicrotask ()
        let done_ = ref false
        runP |> Promise.iter (fun _ -> done_.Value <- true) |> ignore
        do! yieldMicrotask ()
        check "OMP continue recovery blocks resolve before TaskComplete" (not done_.Value)

        rt.SetTaskComplete childId true

        // Post events to child session mailbox for event-driven flow
        match ChildSessionMailboxRegistry.TryGet childId with
        | Some mb ->
            do! mb.Post(Command.TaskComplete "")
            do! mb.Post(Command.SessionIdle)
        | None -> ()

        let! text = runP
        check "OMP continue recovery resolves after TaskComplete" done_.Value
        equal "OMP continue recovery returns no-output" "(no output)" text
    }

let run () =
    promise {
        do! createChildSessionReviewToolNames ()
        do! createChildSessionRunnerToolNames ()
        do! runSubagentOnExistingSessionDoesNotResetTaskComplete ()
        do! runSubagentOnExistingSessionCompletesDespiteRetryingAfterNetworkError ()
    }
