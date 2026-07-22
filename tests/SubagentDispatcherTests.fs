module Wanxiangshu.Tests.SubagentDispatcherTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Runtime.HostAdapter
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.SubagentDispatcher
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.Dyn

let fakeAdapter (workspaceRoot: string) (response: SubagentResponse) : IHostAdapter =
    { new IHostAdapter with
        member _.WorkspaceRoot = workspaceRoot
        member _.SessionId = "test-session"
        member _.SpawnSubagent(_) = Promise.lift response
        member _.ContinueSubagent(childID, agent, prompt) = Promise.lift response
        member _.RegisterTempFiles(_, _) = ()
        member _.TryGetTempFiles(_) = None }

let sampleCoderArgs =
    box
        {| intents =
            box
                [| box
                       {| objective = "fix bug"
                          background = "there is a bug"
                          targets =
                           box
                               [| box
                                      {| file = "src/Code.fs"
                                         guide = "fix the bug" |} |]
                          do_not_touch = [||] |} |]
           tdd = "red" |}

let dispatchReturnsSuccessText () =
    promise {
        let! tempDir = mkdtempAsync "subagent-dispatcher-success-"
        let adapter = fakeAdapter tempDir (Success "report")
        let scope = create ()
        let registry = ChildAgentRegistry.Create()
        let! result = dispatch Opencode adapter "coder" sampleCoderArgs scope (Some registry)
        let containsReport = result.Contains "report"
        check "success returns text" containsReport
        do! rmAsync tempDir
    }

let dispatchReturnsFailureMessage () =
    promise {
        let! tempDir = mkdtempAsync "subagent-dispatcher-failure-"
        let err = FileSystemFault("test.fs", "ENOENT", "not found")
        let adapter = fakeAdapter tempDir (Failure err)
        let scope = create ()
        let registry = ChildAgentRegistry.Create()
        let! result = dispatch Opencode adapter "coder" sampleCoderArgs scope (Some registry)
        let hasFsFault = result.Contains "file system fault"
        check "failure has error text" hasFsFault
        do! rmAsync tempDir
    }

let dispatchReturnsAbortMessage () =
    promise {
        let! tempDir = mkdtempAsync "subagent-dispatcher-abort-"
        let adapter = fakeAdapter tempDir Aborted
        let scope = create ()
        let registry = ChildAgentRegistry.Create()
        let! result = dispatch Opencode adapter "coder" sampleCoderArgs scope (Some registry)
        let hasAborted = result.Contains "aborted"
        check "abort contains 'aborted'" hasAborted
        do! rmAsync tempDir
    }

let testContinueFlow () =
    promise {
        let! tempDir = mkdtempAsync "subagent-dispatcher-continue-"
        let lastReceivedChildID = ref ""
        let lastReceivedAgent = ref ""

        let fakeAdapterWithSpy =
            { new IHostAdapter with
                member _.WorkspaceRoot = tempDir
                member _.SessionId = "test-session"
                member _.SpawnSubagent(_) = Promise.lift (Success "spawned")

                member _.ContinueSubagent(childID, agent, prompt) =
                    lastReceivedChildID.Value <- childID
                    lastReceivedAgent.Value <- agent
                    Promise.lift (Success("next report: " + prompt))

                member _.RegisterTempFiles(_, _) = ()
                member _.TryGetTempFiles(_) = None }

        let scope = create ()
        let registry = ChildAgentRegistry.Create()
        registry.RegisterChildAgent("session-1", "coder", None)

        let runFlow () =
            promise {
                let! result = dispatch Opencode fakeAdapterWithSpy "coder" sampleCoderArgs scope (Some registry)
                let containsSpawned = result.Contains "spawned"
                check "spawned has output" containsSpawned
                let hasIter1 = result.Contains "iter-" || result.Contains "iterator"
                check "spawned has iterator" hasIter1

                let nextIter =
                    let m = System.Text.RegularExpressions.Regex.Match(result, @"iter-[a-zA-Z0-9_-]+")
                    if m.Success then m.Value else "failed"

                let args =
                    box
                        {| iterator = nextIter
                           prompt = "continue standard" |}

                let! continueResult = dispatch Opencode fakeAdapterWithSpy "continue" args scope (Some registry)
                let hasNext = continueResult.Contains "next report: continue standard"
                check "continue returns next report" hasNext
                let agentMatches = lastReceivedAgent.Value = "coder"
                check "continue receives correct agent role" agentMatches
                let hasIter2 = continueResult.Contains "iter-" || continueResult.Contains "iterator"
                check "continue has iterator" hasIter2

                let nextIter2 =
                    let m =
                        System.Text.RegularExpressions.Regex.Match(continueResult, @"iter-[a-zA-Z0-9_-]+")

                    if m.Success then m.Value else "failed"

                let nextArgs =
                    box
                        {| iterator = nextIter2
                           prompt = "second continue" |}

                let! nextResult = dispatch Opencode fakeAdapterWithSpy "continue" nextArgs scope (Some registry)
                let hasNext2 = nextResult.Contains "next report: second continue"
                check "continue again returns next report" hasNext2
                let hasIter3 = nextResult.Contains "iter-" || nextResult.Contains "iterator"
                check "continue again has iterator" hasIter3

                // Verify event log contains expected spawned and continued events
                let! events = getStore(tempDir).ReadAllEvents()

                let spawnedEvents =
                    events |> List.filter (fun e -> e.Kind = eventKindSubagentSpawned)

                check "has 1 subagent_spawned event" (spawnedEvents.Length = 1)
                let firstSpawned = spawnedEvents.[0]
                equal "spawned childId matches" "session-1" (firstSpawned.Payload |> Map.find "childId")
                equal "spawned agent matches" "coder" (firstSpawned.Payload |> Map.find "agent")
                equal "spawned title matches" "Coder" (firstSpawned.Payload |> Map.find "title")

                let continuedEvents =
                    events |> List.filter (fun e -> e.Kind = eventKindSubagentContinued)

                check "has 2 subagent_continued events" (continuedEvents.Length = 2)

                equal
                    "first continued prompt matches"
                    "continue standard"
                    (continuedEvents.[0].Payload |> Map.find "prompt")

                equal
                    "second continued prompt matches"
                    "second continue"
                    (continuedEvents.[1].Payload |> Map.find "prompt")

                // Assert invalid iterator still fails (since parts length will be 5, failing validation)
                let invalidArgs =
                    box
                        {| iterator = "sci_s:session-invalid:coder:Opencode:extra"
                           prompt = "should fail" |}

                let! failedResult = dispatch Opencode fakeAdapterWithSpy "continue" invalidArgs scope (Some registry)
                let hasErrorMsg = failedResult.Contains "continue failed"
                check "continue with invalid iterator fails" hasErrorMsg
            }

        try
            do! runFlow ()
            do! rmAsync tempDir
        with ex ->
            do! rmAsync tempDir
            return! Promise.reject ex
    }

let run () =
    promise {
        do! dispatchReturnsSuccessText ()
        do! dispatchReturnsFailureMessage ()
        do! dispatchReturnsAbortMessage ()
        do! testContinueFlow ()
    }
