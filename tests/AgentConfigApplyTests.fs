module Wanxiangshu.Tests.AgentConfigApplyTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Opencode.AgentConfig
open Wanxiangshu.Shell.Dyn

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