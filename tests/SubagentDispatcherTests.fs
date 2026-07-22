module Wanxiangshu.Tests.SubagentDispatcherTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Runtime.HostAdapter
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.SubagentDispatcher
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.RuntimeScope

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
        check "success returns text" (result.Contains "report")
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
        check "failure has error text" (result.Contains "file system fault")
        do! rmAsync tempDir
    }

let dispatchReturnsAbortMessage () =
    promise {
        let! tempDir = mkdtempAsync "subagent-dispatcher-abort-"
        let adapter = fakeAdapter tempDir Aborted
        let scope = create ()
        let registry = ChildAgentRegistry.Create()
        let! result = dispatch Opencode adapter "coder" sampleCoderArgs scope (Some registry)
        check "abort contains 'aborted'" (result.Contains "aborted")
        do! rmAsync tempDir
    }

let private parseIterator (text: string) : string =
    let m =
        System.Text.RegularExpressions.Regex.Match(text, @"(?:sci_s:[^\s""]+|iter-[a-zA-Z0-9_-]+)")

    if m.Success then m.Value else "failed"

let private assertEvents (tempDir: string) =
    promise {
        let! events = getStore(tempDir).ReadAllEvents()

        let spawnedEvents =
            events |> List.filter (fun e -> e.Kind = eventKindSubagentSpawned)

        check "has 1 subagent_spawned event" (spawnedEvents.Length = 1)
        equal "spawned childId matches" "session-1" (spawnedEvents.[0].Payload |> Map.find "childId")
        equal "spawned agent matches" "coder" (spawnedEvents.[0].Payload |> Map.find "agent")

        let continuedEvents =
            events |> List.filter (fun e -> e.Kind = eventKindSubagentContinued)

        check "has 2 subagent_continued events" (continuedEvents.Length = 2)
        equal "first continued prompt matches" "continue standard" (continuedEvents.[0].Payload |> Map.find "prompt")
        equal "second continued prompt matches" "second continue" (continuedEvents.[1].Payload |> Map.find "prompt")
    }

let private runSpawnAndFirstContinue
    spyAdapter
    scope
    registry
    (lastReceivedChildID: string ref)
    (lastReceivedAgent: string ref)
    =
    promise {
        let! res1 = dispatch Opencode spyAdapter "coder" sampleCoderArgs scope (Some registry)
        check "spawned output" (res1.Contains "spawned")
        let iter1 = parseIterator res1

        let! res2 =
            dispatch
                Opencode
                spyAdapter
                "continue"
                (box
                    {| iterator = iter1
                       prompt = "continue standard" |})
                scope
                (Some registry)

        check "continue next report" (res2.Contains "next report: continue standard")
        equal "coder childId match" "session-1" lastReceivedChildID.Value
        equal "coder agent match" "coder" lastReceivedAgent.Value
        return parseIterator res2
    }

let private runSecondContinueAndFailure spyAdapter scope registry tempDir iter2 =
    promise {
        let! res3 =
            dispatch
                Opencode
                spyAdapter
                "continue"
                (box
                    {| iterator = iter2
                       prompt = "second continue" |})
                scope
                (Some registry)

        check "second continue report" (res3.Contains "next report: second continue")
        do! assertEvents tempDir

        let invalidArgs =
            box
                {| iterator = "sci_s:session-invalid:coder:Opencode:extra"
                   prompt = "should fail" |}

        let! failedRes = dispatch Opencode spyAdapter "continue" invalidArgs scope (Some registry)
        check "invalid iterator fails" (failedRes.Contains "continue failed")
    }

let testContinueFlow () =
    promise {
        let! tempDir = mkdtempAsync "subagent-dispatcher-continue-"
        let lastReceivedChildID = ref ""
        let lastReceivedAgent = ref ""

        let spyAdapter =
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

        try
            let! iter2 = runSpawnAndFirstContinue spyAdapter scope registry lastReceivedChildID lastReceivedAgent
            do! runSecondContinueAndFailure spyAdapter scope registry tempDir iter2
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
