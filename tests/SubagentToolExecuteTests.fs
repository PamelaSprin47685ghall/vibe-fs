module Wanxiangshu.Tests.SubagentToolExecuteTests

open Wanxiangshu.Hosts.Opencode.SubagentIoRun
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.MuxSubagentToolExecute
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.SubagentIteratorStore
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.SubsessionEventStore
open Wanxiangshu.Hosts.Opencode.SubagentIo
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Kernel.Subsession.Types

module Dyn = Wanxiangshu.Runtime.Dyn

let private stubMuxSpawn role =
    { ToolNames = [||]
      AgentId = "coder-agent"
      Title = "Coder"
      AiSettingsAgentId = "coder"
      Role = role
      ToolOptions = None }

let private validMuxConfig () =
    createObj [ "workspaceId", box "ws-1"; "directory", box "."; "sessionID", box "s-mux" ]

let executeMuxDecodeFailureNeverCallsRunMux () =
    promise {
        let mutable runMuxCalls = 0

        let runMuxWithTaskId _ _ _ _ _ _ = Promise.lift (Ok("", "should not run"))

        let runMux _ _ _ _ _ _ =
            runMuxCalls <- runMuxCalls + 1
            Promise.lift "should not run"

        let args = createObj [ "intents", box [||] ]

        let! out =
            executeMuxSubagentTool
                runMuxWithTaskId
                runMux
                runMux
                (createObj [])
                (stubMuxSpawn "coder")
                args
                (validMuxConfig ())
                (Wanxiangshu.Runtime.RuntimeScope.create ())

        check "mux empty intents rejects before runMux" (runMuxCalls = 0)
        check "mux decode failure mentions non-empty" (out.Contains "non-empty")
    }

let executeMuxInvalidConfigNeverCallsRunMux () =
    promise {
        let mutable runMuxCalls = 0

        let runMuxWithTaskId _ _ _ _ _ _ = Promise.lift (Ok("", "should not run"))

        let runMux _ _ _ _ _ _ =
            runMuxCalls <- runMuxCalls + 1
            Promise.lift "should not run"

        let args =
            createObj
                [ "intents", box [| createObj [ "objective", box "x"; "background", box "b"; "targets", box [||] ] |] ]

        let badConfig = createObj [ "directory", box "/proj" ]

        let! out =
            executeMuxSubagentTool
                runMuxWithTaskId
                runMux
                runMux
                (createObj [])
                (stubMuxSpawn "coder")
                args
                badConfig
                (Wanxiangshu.Runtime.RuntimeScope.create ())

        check "mux missing workspaceId rejects before runMux" (runMuxCalls = 0)
        check "mux config failure mentions workspaceId" (out.Contains "workspaceId")
    }

let executeMuxDecodeInvalidIntentNeverCallsRunMux () =
    promise {
        let mutable runMuxCalls = 0

        let runMuxWithTaskId _ _ _ _ _ _ = Promise.lift (Ok("", "should not run"))

        let runMux _ _ _ _ _ _ =
            runMuxCalls <- runMuxCalls + 1
            Promise.lift "should not run"

        let args =
            createObj
                [ "intents", box [| createObj [ "objective", box ""; "background", box "b"; "targets", box [||] ] |] ]

        let! out =
            executeMuxSubagentTool
                runMuxWithTaskId
                runMux
                runMux
                (createObj [])
                (stubMuxSpawn "coder")
                args
                (validMuxConfig ())
                (Wanxiangshu.Runtime.RuntimeScope.create ())

        check "mux invalid intent shape rejects before runMux" (runMuxCalls = 0)
        check "mux invalid intent uses subagentToolFailed" (out.Contains "coder failed:")
    }

let executeMuxSubagentSpawnPreservesPhysicalTaskId () =
    promise {
        let runMuxWithTaskId _ _ _ _ _ _ =
            Promise.lift (Ok("task-physical-1", "task completed successfully"))

        let runMux _ _ _ _ _ _ =
            Promise.lift "task completed successfully"

        let continueMux _ _ _ _ _ _ = Promise.lift "continue completed"

        let args = createObj [ "intent", box "Do spawn task" ]

        let config = validMuxConfig ()

        let sessionScope = Wanxiangshu.Runtime.RuntimeScope.create ()

        let! out =
            executeMuxSubagentTool
                runMuxWithTaskId
                runMux
                continueMux
                (createObj [])
                (stubMuxSpawn "browser")
                args
                config
                sessionScope

        let msgOpt = tryParse out
        check "output has frontmatter info" (Option.isSome msgOpt)
        let msg = Option.get msgOpt

        let iterOpt =
            msg.info
            |> List.tryPick (function
                | InfoItem.Iterator iter -> Some iter
                | _ -> None)

        check "iterator is found in output" (Option.isSome iterOpt)
        let iter = Option.get iterOpt

        let itemOpt = consumeSubagentIterator sessionScope.SubagentIteratorStore iter
        check "iterator can be consumed" (Option.isSome itemOpt)
        let item = Option.get itemOpt

        equal "iterator childID is physical task id" "task-physical-1" item.childID
    }

let executeMuxContinuationUsesPhysicalTaskId () =
    promise {
        let mutable continuedId = ""

        let adapter =
            MuxHostAdapter(
                (fun _ _ _ _ _ _ -> Promise.lift (Ok("task-physical-2", "spawned"))),
                (fun _ _ childId _ _ _ ->
                    continuedId <- childId
                    Promise.lift "continued"),
                createObj [],
                validMuxConfig (),
                stubMuxSpawn "coder",
                ".",
                "parent-session",
                Wanxiangshu.Runtime.RuntimeScope.create ()
            )

        let! _ =
            (adapter :> Wanxiangshu.Runtime.HostAdapter.IHostAdapter)
                .ContinueSubagent("task-physical-2", "coder", "continue")

        equal "Mux continuation preserves physical child id" "task-physical-2" continuedId
    }

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = jsNative

let executeOpencodeCleanupSuccessSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "opencode-cleanup-success-"
        let registry = ChildAgentRegistry.Create()
        let runtime = FallbackRuntimeStore()

        let sequence = ResizeArray<string>()

        let mockClient =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "create",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise { return box {| data = box {| id = "child-session-1" |} |} })
                            )
                            "messages",
                            box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box {| data = [||] |} }))
                            "abort",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise {
                                        sequence.Add("abort")
                                        return box null
                                    })
                            )
                            "delete",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise {
                                        sequence.Add("delete")
                                        return box null
                                    })
                            )
                            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun _ -> promise { return () })) ]
                  ) ]

        let abortedSignal = createObj [ "aborted", box true ]
        let context = createObj [ "abort", box abortedSignal ]

        // Pre-create actor in registry to verify its deletion
        let dummyHost =
            Wanxiangshu.Hosts.Opencode.SubsessionHostAdapter.createHost mockClient "coder" workspaceDir

        let dummyStore = Wanxiangshu.Runtime.SubsessionEventStore.create workspaceDir

        let actor =
            SubsessionActorRegistry.GetOrCreate workspaceDir "child-session-1" dummyHost dummyStore

        // Run the agent. Because context signal is aborted, it will trigger early abort & cleanup.
        let! result =
            runSubagentWithCleanup
                runtime
                registry
                mockClient
                "coder"
                "Coder"
                "prompt"
                workspaceDir
                "parent-session-1"
                context

        check
            "cleanup success result is aborted"
            (match result with
             | Ok res -> res = "(aborted)"
             | _ -> false)

        check "abort was called before delete" (sequence.Count = 2 && sequence.[0] = "abort" && sequence.[1] = "delete")

        // Verify that actor registry and registry have removed it
        check "actor is removed" (SubsessionActorRegistry.TryGet workspaceDir "child-session-1" |> Option.isNone)
        check "child agent registry is unregistered" (registry.LookupChildAgent "child-session-1" |> Option.isNone)

        // Verify that PhysicalSessionClosed event is written to file
        let ndjsonFile = System.IO.Path.Combine(workspaceDir, ".wanxiangshu.ndjson")
        check "ndjson file exists" (System.IO.File.Exists(ndjsonFile))
        let content = readFileSync ndjsonFile "utf8"
        check "ndjson contains physical session closed event" (content.Contains("subsession_physical_session_closed"))

        do! rmAsync workspaceDir
    }

let executeOpencodeCleanupFailureSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "opencode-cleanup-failure-"
        let registry = ChildAgentRegistry.Create()
        let runtime = FallbackRuntimeStore()

        let sequence = ResizeArray<string>()

        let mockClient =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "create",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise { return box {| data = box {| id = "child-session-2" |} |} })
                            )
                            "messages",
                            box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box {| data = [||] |} }))
                            "abort",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise {
                                        sequence.Add("abort")
                                        return box null
                                    })
                            )
                            "delete",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise {
                                        sequence.Add("delete")
                                        failwith "mock delete failed"
                                        return box null
                                    })
                            )
                            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun _ -> promise { return () })) ]
                  ) ]

        let abortedSignal = createObj [ "aborted", box true ]
        let context = createObj [ "abort", box abortedSignal ]

        // Pre-create actor in registry to verify it is NOT deleted
        let dummyHost =
            Wanxiangshu.Hosts.Opencode.SubsessionHostAdapter.createHost mockClient "coder" workspaceDir

        let dummyStore = Wanxiangshu.Runtime.SubsessionEventStore.create workspaceDir

        let actor =
            SubsessionActorRegistry.GetOrCreate workspaceDir "child-session-2" dummyHost dummyStore

        // Run the agent. Because context signal is aborted, it will trigger early abort & cleanup.
        let! result =
            runSubagentWithCleanup
                runtime
                registry
                mockClient
                "coder"
                "Coder"
                "prompt"
                workspaceDir
                "parent-session-2"
                context

        check
            "cleanup failure result is still aborted since it early returns"
            (match result with
             | Ok res -> res = "(aborted)"
             | _ -> false)

        check "abort was called before delete" (sequence.Count = 2 && sequence.[0] = "abort" && sequence.[1] = "delete")

        // Verify that actor registry and registry have NOT removed it because delete failed!
        check "actor is NOT removed" (SubsessionActorRegistry.TryGet workspaceDir "child-session-2" |> Option.isSome)
        check "child agent registry is NOT unregistered" (registry.LookupChildAgent "child-session-2" |> Option.isSome)

        // Verify that PhysicalSessionClosed event is NOT written to file
        let ndjsonFile = System.IO.Path.Combine(workspaceDir, ".wanxiangshu.ndjson")

        let written =
            if System.IO.File.Exists(ndjsonFile) then
                let content = readFileSync ndjsonFile "utf8"
                content.Contains("subsession_physical_session_closed")
            else
                false

        check "ndjson does NOT contain physical session closed event" (not written)

        // Cleanup the actor from registry manually to prevent leaking in tests
        SubsessionActorRegistry.Remove workspaceDir "child-session-2"

        do! rmAsync workspaceDir
    }

let run () =
    promise {
        do! executeMuxDecodeFailureNeverCallsRunMux ()
        do! executeMuxInvalidConfigNeverCallsRunMux ()
        do! executeMuxDecodeInvalidIntentNeverCallsRunMux ()
        do! executeMuxSubagentSpawnPreservesPhysicalTaskId ()
        do! executeMuxContinuationUsesPhysicalTaskId ()
        do! executeOpencodeCleanupSuccessSpec ()
        do! executeOpencodeCleanupFailureSpec ()
    }
