module Wanxiangshu.Tests.SubagentToolExecuteTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.MuxSubagentToolExecute
open Wanxiangshu.Tests.IntegrationToolSetup

let private stubMuxSpawn role =
    { ToolNames = [||]
      AgentId = "coder-agent"
      Title = "Coder"
      AiSettingsAgentId = "coder"
      Role = role
      ToolOptions = None }

let private validMuxConfig () =
    createObj
        [ "workspaceId", box "ws-1"
          "directory", box "/proj"
          "sessionID", box "s-mux" ]

let executeMuxDecodeFailureNeverCallsRunMux () =
    promise {
        let mutable runMuxCalls = 0

        let runMux _ _ _ _ _ _ =
            runMuxCalls <- runMuxCalls + 1
            Promise.lift "should not run"

        let args = createObj [ "intents", box [||] ]

        let! out =
            executeMuxSubagentTool
                runMux
                (createObj [])
                (stubMuxSpawn "coder")
                args
                (validMuxConfig ())
                (Wanxiangshu.Shell.RuntimeScope.create ())

        check "mux empty intents rejects before runMux" (runMuxCalls = 0)
        check "mux decode failure mentions non-empty" (out.Contains "non-empty")
    }

let executeMuxInvalidConfigNeverCallsRunMux () =
    promise {
        let mutable runMuxCalls = 0

        let runMux _ _ _ _ _ _ =
            runMuxCalls <- runMuxCalls + 1
            Promise.lift "should not run"

        let args =
            createObj
                [ "intents", box [| createObj [ "objective", box "x"; "background", box "b"; "targets", box [||] ] |] ]

        let badConfig = createObj [ "directory", box "/proj" ]

        let! out =
            executeMuxSubagentTool
                runMux
                (createObj [])
                (stubMuxSpawn "coder")
                args
                badConfig
                (Wanxiangshu.Shell.RuntimeScope.create ())

        check "mux missing workspaceId rejects before runMux" (runMuxCalls = 0)
        check "mux config failure mentions workspaceId" (out.Contains "workspaceId")
    }

let executeMuxDecodeInvalidIntentNeverCallsRunMux () =
    promise {
        let mutable runMuxCalls = 0

        let runMux _ _ _ _ _ _ =
            runMuxCalls <- runMuxCalls + 1
            Promise.lift "should not run"

        let args =
            createObj
                [ "intents", box [| createObj [ "objective", box ""; "background", box "b"; "targets", box [||] ] |] ]

        let! out =
            executeMuxSubagentTool
                runMux
                (createObj [])
                (stubMuxSpawn "coder")
                args
                (validMuxConfig ())
                (Wanxiangshu.Shell.RuntimeScope.create ())

        check "mux invalid intent shape rejects before runMux" (runMuxCalls = 0)
        check "mux invalid intent uses subagentToolFailed" (out.Contains "coder failed:")
    }

let run () =
    promise {
        do! executeMuxDecodeFailureNeverCallsRunMux ()
        do! executeMuxInvalidConfigNeverCallsRunMux ()
        do! executeMuxDecodeInvalidIntentNeverCallsRunMux ()
    }
