module Wanxiangshu.Tests.ArchitectureTestsSubagent

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let muxSubagentToolsUsesToolCopy () =
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let shell = requireFile "src/Shell/MuxSubagentToolExecute.fs" |> nonCommentCode
    check "arch: Mux SubagentTools must not open ToolCopy (Shell owns copy)"
        (not (mux.Contains "ToolCopy"))
    check "arch: MuxSubagentToolExecute opens ToolCopy"
        (shell.Contains "ToolCopy")
    check "arch: MuxSubagentToolExecute uses muxToolRequiresWorkspaceId"
        (shell.Contains "muxToolRequiresWorkspaceId")
    check "arch: MuxSubagentToolExecute must not inline requires workspaceId template"
        (not (shell.Contains "requires workspaceId"))

let muxSubagentToolsUsesFromMuxConfig () =
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let shell = requireFile "src/Shell/MuxSubagentToolExecute.fs" |> nonCommentCode
    check "arch: Mux SubagentTools must not open ToolRuntimeContext (Shell owns config)"
        (not (mux.Contains "ToolRuntimeContext"))
    check "arch: MuxSubagentToolExecute opens ToolRuntimeContext"
        (shell.Contains "ToolRuntimeContext")
    check "arch: MuxSubagentToolExecute uses fromMuxConfig"
        (shell.Contains "fromMuxConfig")
    check "arch: MuxSubagentToolExecute must not strField config workspaceId"
        (not (shell.Contains "strField config \"workspaceId\""))

let muxSubagentToolsUsesSubagentToolPolicy () =
    let code = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    check "arch: Mux SubagentTools calls SubagentToolPolicy.disabledToolNamesForRole"
        (code.Contains "SubagentToolPolicy.disabledToolNamesForRole")
    check "arch: Mux SubagentTools disabledToolsForRole delegates to policy with muxSpawnToolUniverse"
        (code.Contains "disabledToolNamesForRole mux toolNames role muxSpawnToolUniverse")
    check "arch: Mux SubagentTools must not filter with canUseForHost locally"
        (not (code.Contains "canUseForHost"))
    check "arch: Mux SubagentTools must not call deniedToolsForHost locally"
        (not (code.Contains "deniedToolsForHost"))

let subagentToolsUseKernelPromptHelpers () =
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let dispatcher = requireFile "src/Shell/SubagentDispatcher.fs" |> nonCommentCode
    let opencode = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    check ("arch: SubagentDispatcher uses promptsFromCoderIntents")
        (dispatcher.Contains "promptsFromCoderIntents")
    check ("arch: SubagentDispatcher uses meditatorPromptFromFiles")
        (dispatcher.Contains "meditatorPromptFromFiles")
    check ("arch: SubagentDispatcher uses browserPromptText")
        (dispatcher.Contains "browserPromptText")
    check "arch: Opencode SubagentTools must not call promptsForParallelIntents locally"
        (not (opencode.Contains "promptsForParallelIntents"))
    check "arch: Opencode SubagentTools must not call meditatorPromptText locally"
        (not (opencode.Contains "meditatorPromptText"))
    check "arch: Opencode SubagentTools must not call buildMeditatorSections locally"
        (not (opencode.Contains "buildMeditatorSections"))
    check "arch: Mux SubagentTools must not call promptsForParallelIntents locally"
        (not (mux.Contains "promptsForParallelIntents"))
    check "arch: Mux SubagentTools must not call meditatorPromptText locally"
        (not (mux.Contains "meditatorPromptText"))
    check "arch: Mux SubagentTools must not call buildMeditatorSections locally"
        (not (mux.Contains "buildMeditatorSections"))
    check "arch: Mux SubagentTools must not call formatPrompt opencode (Coder"
        (not (mux.Contains "formatPrompt opencode (Coder"))
    check "arch: Mux SubagentTools must not call formatPrompt Host.Mimocode (Coder"
        (not (mux.Contains "formatPrompt Host.Mimocode (Coder"))
    check "arch: Mux SubagentTools must not call formatPrompt opencode (Investigator"
        (not (mux.Contains "formatPrompt opencode (Investigator"))
    check "arch: Mux SubagentTools must not call formatPrompt Host.Mimocode (Investigator"
        (not (mux.Contains "formatPrompt Host.Mimocode (Investigator"))
    check "arch: Mux SubagentTools must not call formatPrompt opencode (Meditator"
        (not (mux.Contains "formatPrompt opencode (Meditator"))
    check "arch: Mux SubagentTools must not call formatPrompt Host.Mimocode (Meditator"
        (not (mux.Contains "formatPrompt Host.Mimocode (Meditator"))
    check "arch: Mux SubagentTools must not call formatPrompt opencode (Browser"
        (not (mux.Contains "formatPrompt opencode (Browser"))
    check "arch: Mux SubagentTools must not call formatPrompt Host.Mimocode (Browser"
        (not (mux.Contains "formatPrompt Host.Mimocode (Browser"))

let opencodeSubagentToolExecuteUsesHostNotLiteralOpencode () =
    let opencode = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    check "arch: Opencode SubagentTools threads Host into prompt helpers (spawn.Host or execute host param)"
        (opencode.Contains "dispatch host")

let subagentToolsUseDecodeIntentsField () =
    let codec = requireFile "src/Shell/SubagentSimpleArgsCodec.fs" |> nonCommentCode
    let decode = requireFile "src/Shell/ToolArgsDecode.fs" |> nonCommentCode
    check "arch: SubagentSimpleArgsCodec defines decodeIntentsField"
        (codec.Contains "let decodeIntentsField")
    check "arch: ToolArgsDecode must not use decodeIntentsField"
        (not (decode.Contains "decodeIntentsField"))
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    check "arch: Mux SubagentTools must not use decodeIntentsField"
        (not (mux.Contains "decodeIntentsField"))
    check "arch: Mux SubagentTools must not Dyn.get args intents"
        (not (mux.Contains "Dyn.get args \"intents\""))