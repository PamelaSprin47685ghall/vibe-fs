module Wanxiangshu.Tests.OmpChildSessionCommonTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Omp.ChildSessionCommon
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Shell.ChildSessionMailbox

let runOmpSubagentCore_concurrentRunRejected_leavesOriginalLifecycleIntact () =
    promise {
        let rt = FallbackRuntimeState()
        let childId = "omp-child-lifecycle-test"
        let s0 = rt.GetOrCreateState childId

        // Set initial state to TaskComplete
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

        // Start the first run via StartSubsessionRun to make it busy/running
        let started1 = rt.StartSubsessionRun(childId, "parent-session-id", "run-1")
        check "first run started successfully" started1

        // Mock session objects
        let sessionManagerMock = createObj [ "messages", box [||] ]

        let sessionMock =
            createObj
                [ "prompt", box (fun (_p: string) -> Promise.lift ())
                  "waitForIdle", box (fun () -> Promise.lift ())
                  "sessionManager", box sessionManagerMock ]

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

        // The preclaimed run represents the original in-flight invocation.
        let activeRunId = rt.TryGetActiveRunId childId
        equal "active run is run-1" (Some "run-1") activeRunId

        // Now trigger the second run with ResetToActive policy.
        // It should try to register "run-2" on childId which is already occupied by "run-1".
        // It must throw/fail with "Subagent session already running".
        let mutable secondRunRejected = false

        let piMock =
            createObj [ "session", box (createObj [ "sessionPrompt", box (fun () -> Promise.lift (box null)) ]) ]

        let! _ =
            runOmpSubagentCore
                rt
                (Some config)
                childId
                sessionMock
                "second prompt"
                SubagentResetPolicy.ResetToActive
                "parent-session-id"
                piMock
            |> Promise.catch (fun ex ->
                secondRunRejected <- ex.Message.Contains "already running"
                abortedPrefix)

        check "second run was rejected because subsession already running" secondRunRejected

        // Check if the original lifecycle was left intact (i.e. remains TaskComplete, not mutated to Active)
        let finalState = rt.GetOrCreateState childId
        equal "original lifecycle kept intact" FallbackLifecycle.TaskComplete finalState.Lifecycle

        rt.ClearSubsessionRun(childId, "run-1")
    }
