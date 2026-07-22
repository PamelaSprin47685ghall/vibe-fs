module Wanxiangshu.Tests.MuxSubagentToolExecuteTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.MuxSubagentToolExecute
open Wanxiangshu.Runtime.SubagentIteratorStore
open Wanxiangshu.Kernel.ToolOutputInfoTypes

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

        check "output has content" (out.Contains "body =")

        let iterOpt =
            if out.Contains "sci_s" || out.Contains "iter-" then
                let m =
                    System.Text.RegularExpressions.Regex.Match(out, @"(?:sci_s:[^\s""]+|iter-[a-zA-Z0-9_-]+)")

                if m.Success then Some m.Value else None
            else
                None

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

let run () =
    promise {
        do! executeMuxDecodeFailureNeverCallsRunMux ()
        do! executeMuxInvalidConfigNeverCallsRunMux ()
        do! executeMuxDecodeInvalidIntentNeverCallsRunMux ()
        do! executeMuxSubagentSpawnPreservesPhysicalTaskId ()
        do! executeMuxContinuationUsesPhysicalTaskId ()
    }
