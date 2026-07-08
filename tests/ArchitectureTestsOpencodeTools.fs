module Wanxiangshu.Tests.ArchitectureTestsOpencodeTools

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let opencodeToolSchemaDescriptionsFromCatalog () =
    let code = requireFile "src/Opencode/ToolSchema.fs" |> nonCommentCode
    check "arch: Opencode ToolSchema defines toolDescription" (code.Contains "let private toolDescription")
    check "arch: Opencode ToolSchema coder uses toolDescription" (code.Contains "let coder = toolDescription")

    check
        "arch: Opencode ToolSchema must not alias description as coder"
        (not (code.Contains "let coder = description"))

let opencodeSubagentToolsUsesOpencodeClientCodec () =
    let code = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    check "arch: Opencode SubagentTools opens OpencodeClientCodec" (code.Contains "OpencodeClientCodec")
    check "arch: Opencode SubagentTools uses getClientFromPluginCtx" (code.Contains "getClientFromPluginCtx")
    check "arch: Opencode SubagentTools must not Dyn.get ctx client" (not (code.Contains "Dyn.get ctx \"client\""))

let opencodeToolsUseHostForSummarizerPrompts () =
    let executor = requireFile "src/Opencode/ExecutorTool.fs" |> nonCommentCode
    let search = requireFile "src/Opencode/SearchTools.fs" |> nonCommentCode
    let methodology = requireFile "src/Methodology/OpencodeTools.fs" |> nonCommentCode

    for (label, code) in
        [| "ExecutorTool", executor
           "SearchTools", search
           "OpencodeTools", methodology |] do
        check ("arch: " + label + " must not use formatPrompt opencode") (not (code.Contains "formatPrompt opencode"))

    check
        "arch: summarizer paths use dynamic host (formatPrompt host or spawn/plugin host)"
        ((executor.Contains "formatPrompt host")
         || (executor.Contains "formatPrompt spawn")
         || (executor.Contains "formatPrompt pluginHost")
         || (search.Contains "formatPrompt host")
         || (search.Contains "formatPrompt spawn")
         || (search.Contains "formatPrompt pluginHost")
         || (methodology.Contains "formatPrompt host")
         || (methodology.Contains "formatPrompt spawn")
         || (methodology.Contains "formatPrompt pluginHost")
         || (executor.Contains "formatPrompt Mimocode")
         || (search.Contains "formatPrompt Mimocode")
         || (methodology.Contains "formatPrompt Mimocode"))
