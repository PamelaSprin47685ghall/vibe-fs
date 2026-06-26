module Wanxiangshu.Tests.ArchitectureTestsOpencodeTools

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let opencodeToolSchemaDescriptionsFromCatalog () =
    let code = requireFile "src/Opencode/ToolSchema.fs" |> nonCommentCode
    check "arch: Opencode ToolSchema defines toolDescription" (code.Contains "let private toolDescription")
    check "arch: Opencode ToolSchema coder uses toolDescription" (code.Contains "let coder = toolDescription")
    check "arch: Opencode ToolSchema must not alias description as coder" (not (code.Contains "let coder = description"))
    check "arch: Opencode ToolSchema knowledgeGraphDraftEntriesReq uses Params.kgEntryEntity" (code.Contains "Params.kgEntryEntity")
    check "arch: Opencode ToolSchema knowledgeGraphDraftEntriesReq must not inline Knowledge graph entity"
        (not (code.Contains "Knowledge graph entity"))

let opencodeSubagentToolsUsesOpencodeClientCodec () =
    let code = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    check "arch: Opencode SubagentTools opens OpencodeClientCodec" (code.Contains "OpencodeClientCodec")
    check "arch: Opencode SubagentTools uses getClientFromPluginCtx" (code.Contains "getClientFromPluginCtx")
    check "arch: Opencode SubagentTools must not Dyn.get ctx client" (not (code.Contains "Dyn.get ctx \"client\""))

let opencodeKnowledgeGraphToolsUsesFromOpencode () =
    let code = requireFile "src/Opencode/KnowledgeGraphTools.fs" |> nonCommentCode
    check "arch: Opencode KnowledgeGraphTools opens ToolRuntimeContext" (code.Contains "ToolRuntimeContext")
    check "arch: Opencode KnowledgeGraphTools uses fromOpencode" (code.Contains "fromOpencode")
    check "arch: Opencode KnowledgeGraphTools uses runtime.Execution for session and directory"
        ((code.Contains "runtime.Execution.SessionId") && (code.Contains "runtime.Execution.Directory"))
    check "arch: Opencode KnowledgeGraphTools must not Dyn.str context sessionID" (not (code.Contains "Dyn.str context \"sessionID\""))
    check "arch: Opencode KnowledgeGraphTools must not Dyn.str context directory" (not (code.Contains "Dyn.str context \"directory\""))
    check "arch: Opencode KnowledgeGraphTools must not Dyn.str ctx directory" (not (code.Contains "Dyn.str ctx \"directory\""))
    check "arch: Opencode KnowledgeGraphTools uses pluginDirectoryFromCtx" (code.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode KnowledgeGraphTools fetch uses Params.fetchKnowledgeGraphEntity" (code.Contains "Params.fetchKnowledgeGraphEntity")
    check "arch: Opencode KnowledgeGraphTools entries uses Params.submitKnowledgeGraphEntries" (code.Contains "Params.submitKnowledgeGraphEntries")

let opencodeKgUsesKnowledgeGraphToolsCodec () =
    let code = requireFile "src/Opencode/KnowledgeGraphTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/KnowledgeGraphToolsCodec.fs" |> nonCommentCode
    check "arch: KnowledgeGraphToolsCodec defines decodeFetchEntity" (codec.Contains "let decodeFetchEntity")
    check "arch: KnowledgeGraphToolsCodec defines decodeDraftEntries" (codec.Contains "let decodeDraftEntries")
    check "arch: KnowledgeGraphToolsCodec defines decodeReturnBookkeeperArgs" (codec.Contains "let decodeReturnBookkeeperArgs")
    check "arch: Opencode KnowledgeGraphTools opens KnowledgeGraphToolsCodec" (code.Contains "KnowledgeGraphToolsCodec")
    check "arch: Opencode KnowledgeGraphTools uses decodeFetchEntity" (code.Contains "decodeFetchEntity")
    check "arch: Opencode KnowledgeGraphTools uses decodeReturnBookkeeperArgs" (code.Contains "decodeReturnBookkeeperArgs")
    check "arch: Opencode KnowledgeGraphTools must not open Dyn" (not (code.Contains "open Wanxiangshu.Shell.Dyn"))
    check "arch: Opencode KnowledgeGraphTools must not Dyn.get args entries" (not (code.Contains "Dyn.get args \"entries\""))
    check "arch: Opencode KnowledgeGraphTools must not parseDraftArray" (not (code.Contains "parseDraftArray"))
    check "arch: Opencode KnowledgeGraphTools fetch must not Dyn.str args entity" (not (code.Contains "Dyn.str args \"entity\""))
    check "arch: Opencode KnowledgeGraphTools opens ToolExecute" (code.Contains "ToolExecute")
    check "arch: Opencode KnowledgeGraphTools fetch decode uses wireDecodeFailure knowledge_graph_fetch"
        (code.Contains "wireDecodeFailure \"knowledge_graph_fetch\"")
    check "arch: Opencode KnowledgeGraphTools return decode uses wireDecodeFailure return_bookkeeper"
        (code.Contains "wireDecodeFailure \"return_bookkeeper\"")
    check "arch: Opencode KnowledgeGraphTools must not formatDomainError on decode" (not (code.Contains "formatDomainError"))

let opencodeToolsUseHostForSummarizerPrompts () =
    let executor = requireFile "src/Opencode/ExecutorTool.fs" |> nonCommentCode
    let search = requireFile "src/Opencode/SearchTools.fs" |> nonCommentCode
    let methodology = requireFile "src/Methodology/OpencodeTools.fs" |> nonCommentCode
    for (label, code) in [| "ExecutorTool", executor; "SearchTools", search; "OpencodeTools", methodology |] do
        check ("arch: " + label + " must not use formatPrompt opencode") (not (code.Contains "formatPrompt opencode"))
    check "arch: summarizer paths use dynamic host (formatPrompt host or spawn/plugin host)"
        ((executor.Contains "formatPrompt host") || (executor.Contains "formatPrompt spawn") || (executor.Contains "formatPrompt pluginHost")
         || (search.Contains "formatPrompt host") || (search.Contains "formatPrompt spawn") || (search.Contains "formatPrompt pluginHost")
         || (methodology.Contains "formatPrompt host") || (methodology.Contains "formatPrompt spawn") || (methodology.Contains "formatPrompt pluginHost")
         || (executor.Contains "formatPrompt Mimocode") || (search.Contains "formatPrompt Mimocode") || (methodology.Contains "formatPrompt Mimocode"))