module VibeFs.Tests.AgentConfigApplyTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.HostTools
open VibeFs.Opencode.AgentConfig
open VibeFs.Shell.Dyn

let applyAgentConfigForNullCoderEntryUsesDefaults () =
    let cfg =
        createObj [
            "agent", box (createObj [ "coder", null ])
        ]
    let next = applyAgentConfigFor opencode cfg (createObj [])
    let coder = get (get next "agent") "coder"
    check "coder not nullish" (not (isNullish coder))
    equal "coder default mode" "subagent" (str coder "mode")
    let tools = get coder "tools"
    check "coder tools object" (not (isNullish tools))
    check "coder tools.glob present" (has tools "glob")

let run () =
    applyAgentConfigForNullCoderEntryUsesDefaults ()