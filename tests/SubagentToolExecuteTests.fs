module VibeFs.Tests.SubagentToolExecuteTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Domain
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.MuxSubagentToolExecute
open VibeFs.Shell.SubagentToolExecute
open VibeFs.Tests.IntegrationToolSetup

let private stubSpawn () =
    { Host = VibeFs.Kernel.HostTools.opencode
      Registry = ChildAgentRegistry.Create()
      Client = createObj []
      PluginCtx = createObj [ "directory", box "/proj" ]
      ToolContext = createObj [ "directory", box "/proj"; "sessionID", box "s-parent" ] }

let private stubMuxSpawn role =
    { ToolNames = [||]
      AgentId = "coder-agent"
      Title = "Coder"
      AiSettingsAgentId = "coder"
      Role = role
      ToolOptions = None }

let private validMuxConfig () =
    createObj [ "workspaceId", box "ws-1"; "directory", box "/proj"; "sessionID", box "s-mux" ]

let executeOpencodeDecodeFailureNeverCallsRunCore () = promise {
    let mutable runCoreCalls = 0
    let runCore _ _ _ _ _ _ _ _ _ _ =
        runCoreCalls <- runCoreCalls + 1
        Promise.lift (Ok "should not run")
    let args = createObj [ "intents", box [||] ]
    let! out = executeOpencodeSubagentTool runCore (stubSpawn ()) "coder" args
    check "opencode empty intents rejects before runCore" (runCoreCalls = 0)
    check "opencode decode failure mentions non-empty" (out.Contains "non-empty")
}

let executeOpencodeMissingIntentsNeverCallsRunCore () = promise {
    let mutable runCoreCalls = 0
    let runCore _ _ _ _ _ _ _ _ _ _ =
        runCoreCalls <- runCoreCalls + 1
        Promise.lift (Ok "should not run")
    let! out = executeOpencodeSubagentTool runCore (stubSpawn ()) "coder" (createObj [])
    check "opencode missing intents rejects before runCore" (runCoreCalls = 0)
    check "opencode missing intents uses formatDomainError" (out.Contains "coder failed:")
}

let executeMuxDecodeFailureNeverCallsRunMux () = promise {
    let mutable runMuxCalls = 0
    let runMux _ _ _ _ _ _ =
        runMuxCalls <- runMuxCalls + 1
        Promise.lift "should not run"
    let args = createObj [ "intents", box [||] ]
    let! out = executeMuxSubagentTool runMux (createObj []) (stubMuxSpawn "coder") args (validMuxConfig ())
    check "mux empty intents rejects before runMux" (runMuxCalls = 0)
    check "mux decode failure mentions non-empty" (out.Contains "non-empty")
}

let executeMuxInvalidConfigNeverCallsRunMux () = promise {
    let mutable runMuxCalls = 0
    let runMux _ _ _ _ _ _ =
        runMuxCalls <- runMuxCalls + 1
        Promise.lift "should not run"
    let args = createObj [ "intents", box [| createObj [ "objective", box "x"; "background", box "b"; "targets", box [||] ] |] ]
    let badConfig = createObj [ "directory", box "/proj" ]
    let! out = executeMuxSubagentTool runMux (createObj []) (stubMuxSpawn "coder") args badConfig
    check "mux missing workspaceId rejects before runMux" (runMuxCalls = 0)
    check "mux config failure mentions workspaceId" (out.Contains "workspaceId")
}

let executeOpencodeValidIntentCallsRunCore () = promise {
    let mutable runCoreCalls = 0
    let runCore _ _ _ _ _ _ _ _ _ _ =
        runCoreCalls <- runCoreCalls + 1
        Promise.lift (Ok "child report")
    let args = createObj [ "intents", box [| sampleCoderIntent "fix" "a.ts" |] ]
    let! out = executeOpencodeSubagentTool runCore (stubSpawn ()) "coder" args
    check "opencode valid intent invokes runCore" (runCoreCalls = 1)
    check "opencode valid intent returns runCore text" (out = "child report")
}

let executeMuxDecodeInvalidIntentNeverCallsRunMux () = promise {
    let mutable runMuxCalls = 0
    let runMux _ _ _ _ _ _ =
        runMuxCalls <- runMuxCalls + 1
        Promise.lift "should not run"
    let args = createObj [ "intents", box [| createObj [ "objective", box ""; "background", box "b"; "targets", box [||] ] |] ]
    let! out = executeMuxSubagentTool runMux (createObj []) (stubMuxSpawn "coder") args (validMuxConfig ())
    check "mux invalid intent shape rejects before runMux" (runMuxCalls = 0)
    check "mux invalid intent uses subagentToolFailed" (out.Contains "coder failed:")
}

let run () = promise {
    do! executeOpencodeDecodeFailureNeverCallsRunCore ()
    do! executeOpencodeMissingIntentsNeverCallsRunCore ()
    do! executeOpencodeValidIntentCallsRunCore ()
    do! executeMuxDecodeFailureNeverCallsRunMux ()
    do! executeMuxInvalidConfigNeverCallsRunMux ()
    do! executeMuxDecodeInvalidIntentNeverCallsRunMux ()
}