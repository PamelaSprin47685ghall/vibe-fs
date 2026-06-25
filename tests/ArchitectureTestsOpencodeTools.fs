module VibeFs.Tests.ArchitectureTestsOpencodeTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.ArchitectureTestsSupport

let opencodeReviewUsesToolCopy () =
    let code = requireFile "src/Opencode/ReviewTools.fs" |> nonCommentCode
    check "arch: Opencode ReviewTools opens ToolCopy"
        (code.Contains "ToolCopy")
    check "arch: Opencode ReviewTools uses submitReviewNotNeeded"
        (code.Contains "submitReviewNotNeeded")
    check "arch: Opencode ReviewTools uses opencodeSubmitReviewInProgress"
        (code.Contains "opencodeSubmitReviewInProgress")
    check "arch: Opencode ReviewTools must not inline you do not need review"
        (not (code.Contains "You do not need review. Just continue with your work."))

let opencodeReviewUsesFromOpencode () =
    let code = requireFile "src/Opencode/ReviewTools.fs" |> nonCommentCode
    check "arch: Opencode ReviewTools opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Opencode ReviewTools submit uses fromOpencode"
        (code.Contains "fromOpencode")
    check "arch: Opencode ReviewTools uses runtime.Execution for session and directory"
        ((code.Contains "runtime.Execution.SessionId")
         && (code.Contains "runtime.Execution.Directory"))
    check "arch: Opencode ReviewTools must not extractToolContext in submit"
        (not (code.Contains "extractToolContext"))
    check "arch: Opencode ReviewTools must not Dyn.str tc sessionID"
        (not (code.Contains "Dyn.str tc \"sessionID\""))
    check "arch: Opencode ReviewTools must not Dyn.str tc directory"
        (not (code.Contains "Dyn.str tc \"directory\""))
    check "arch: Opencode ReviewTools must not Dyn.str context sessionID"
        (not (code.Contains "Dyn.str context \"sessionID\""))
    check "arch: Opencode ReviewTools must not Dyn.str context directory"
        (not (code.Contains "Dyn.str context \"directory\""))
    check "arch: Opencode ReviewTools uses pluginDirectoryFromCtx"
        (code.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode ReviewTools must not Dyn.str ctx directory"
        (not (code.Contains "Dyn.str ctx \"directory\""))

let opencodeReviewUsesReviewToolsCodec () =
    let code = requireFile "src/Opencode/ReviewTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/ReviewToolsCodec.fs" |> nonCommentCode
    check "arch: ReviewToolsCodec defines decodeSubmitReviewArgs"
        (codec.Contains "let decodeSubmitReviewArgs")
    check "arch: Opencode ReviewTools opens ReviewToolsCodec"
        (code.Contains "ReviewToolsCodec")
    check "arch: Opencode ReviewTools submit uses decodeSubmitReviewArgs"
        (code.Contains "decodeSubmitReviewArgs")
    check "arch: Opencode ReviewTools submit must not Dyn.str args report"
        (not (code.Contains "Dyn.str args \"report\""))
    check "arch: ReviewToolsCodec defines decodeReturnReviewerArgs"
        (codec.Contains "let decodeReturnReviewerArgs")
    check "arch: Opencode ReviewTools return uses decodeReturnReviewerArgs"
        (code.Contains "decodeReturnReviewerArgs")
    check "arch: Opencode ReviewTools return must not Dyn.str args verdict"
        (not (code.Contains "Dyn.str args \"verdict\""))
    check "arch: Opencode ReviewTools return uses submitReviewResult description"
        (code.Contains "submitReviewResult")
    check "arch: Opencode ReviewTools return uses Params.returnReviewerVerdict"
        (code.Contains "Params.returnReviewerVerdict")
    check "arch: Opencode ReviewTools opens ToolExecute"
        (code.Contains "ToolExecute")
    check "arch: Opencode ReviewTools submit decode uses wireDecodeFailure submit_review"
        (code.Contains "wireDecodeFailure \"submit_review\"")
    check "arch: Opencode ReviewTools return decode uses wireDecodeFailure return_reviewer"
        (code.Contains "wireDecodeFailure \"return_reviewer\"")
    check "arch: Opencode ReviewTools client failure uses wireEncodeToolError OpencodeClient"
        (code.Contains "wireEncodeToolError \"OpencodeClient\"")
    check "arch: Opencode ReviewTools must not ToolHelpers.formatDomainError submit_review"
        (not (code.Contains "formatDomainError \"submit_review\""))
    check "arch: Opencode ReviewTools must not ToolHelpers.formatDomainError return_reviewer"
        (not (code.Contains "formatDomainError \"return_reviewer\""))

let opencodeSearchToolsUsesFuzzyToolsCodec () =
    let search = requireFile "src/Opencode/SearchTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/FuzzyToolsCodec.fs" |> nonCommentCode
    check "arch: Opencode SearchTools opens FuzzyToolsCodec" (search.Contains "FuzzyToolsCodec")
    check "arch: FuzzyToolsCodec defines decodeFuzzyFindArgs" (codec.Contains "let decodeFuzzyFindArgs")
    check "arch: FuzzyToolsCodec defines decodeFuzzyGrepArgs" (codec.Contains "let decodeFuzzyGrepArgs")
    check "arch: FuzzyToolsCodec uses parseExcludeField" (codec.Contains "parseExcludeField")
    check "arch: Opencode SearchTools fuzzy_find uses decodeFuzzyFindArgs" (search.Contains "decodeFuzzyFindArgs")
    check "arch: Opencode SearchTools fuzzy_grep uses decodeFuzzyGrepArgs" (search.Contains "decodeFuzzyGrepArgs")
    check "arch: Opencode SearchTools fuzzy must not optStr args pattern" (not (search.Contains "optStr args \"pattern\""))
    check "arch: Opencode SearchTools fuzzy must not parseExcludeField args inline" (not (search.Contains "parseExcludeField args"))
    check "arch: Opencode SearchTools fuzzy decode uses wireDecodeFailure"
        (search.Contains "wireDecodeFailure toolName")

let opencodeSearchToolsUsesWebToolsCodec () =
    let search = requireFile "src/Opencode/SearchTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/WebToolsCodec.fs" |> nonCommentCode
    check "arch: Opencode SearchTools opens WebToolsCodec"
        (search.Contains "WebToolsCodec")
    check "arch: WebToolsCodec defines decodeWebsearchArgs"
        (codec.Contains "let decodeWebsearchArgs")
    check "arch: Opencode SearchTools uses decodeWebsearchArgs"
        (search.Contains "decodeWebsearchArgs")
    check "arch: Opencode SearchTools uses decodeWebfetchArgs"
        (search.Contains "decodeWebfetchArgs")
    check "arch: Opencode SearchTools opens ToolRuntimeContext"
        (search.Contains "ToolRuntimeContext")
    check "arch: Opencode SearchTools uses fromOpencode"
        (search.Contains "fromOpencode")
    check "arch: Opencode SearchTools websearch must not inline Dyn.str args query"
        (not (search.Contains "Dyn.str args \"query\""))
    check "arch: Opencode SearchTools webfetch must not inline Dyn.str args url"
        (not (search.Contains "Dyn.str args \"url\""))
    check "arch: Opencode SearchTools web decode uses wireDecodeFailure"
        ((search.Contains "wireDecodeFailure \"websearch\"") && (search.Contains "wireDecodeFailure \"webfetch\""))

let opencodeSearchToolsUsesToolCopy () =
    let search = requireFile "src/Opencode/SearchTools.fs" |> nonCommentCode
    check "arch: Opencode SearchTools opens ToolCopy"
        (search.Contains "ToolCopy")
    check "arch: Opencode SearchTools fuzzy session uses toolRequiresActiveSession"
        (search.Contains "toolRequiresActiveSession toolName")
    check "arch: Opencode SearchTools fuzzy uses fromOpencode for session/directory"
        ((search.Contains "fromOpencode context")
         && (search.Contains "runtime.Execution.SessionId")
         && (search.Contains "runtime.Execution.Directory"))
    check "arch: Opencode SearchTools fuzzy must not Dyn.str context sessionID"
        (not (search.Contains "Dyn.str context \"sessionID\""))
    check "arch: Opencode SearchTools must not inline requires an active session"
        (not (search.Contains "requires an active session"))
    check "arch: Opencode SearchTools web uses pluginDirectoryFromCtx"
        (search.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode SearchTools must not Dyn.str ctx directory"
        (not (search.Contains "Dyn.str ctx \"directory\""))

let opencodeExecutorUsesToolCopy () =
    let code = requireFile "src/Opencode/ExecutorTool.fs" |> nonCommentCode
    check "arch: Opencode ExecutorTool opens ToolCopy"
        (code.Contains "ToolCopy")
    check "arch: Opencode ExecutorTool uses executorRequiresSession"
        (code.Contains "executorRequiresSession")
    check "arch: Opencode ExecutorTool must not inline expected shell, python, or javascript"
        (not (code.Contains "expected shell, python, or javascript"))

let opencodeExecutorUsesExecutorToolsCodec () =
    let code = requireFile "src/Opencode/ExecutorTool.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/ExecutorToolsCodec.fs" |> nonCommentCode
    check "arch: ExecutorToolsCodec defines decodeExecutorArgs"
        (codec.Contains "let decodeExecutorArgs")
    check "arch: ExecutorToolsCodec defines toExecuteOptions"
        (codec.Contains "let toExecuteOptions")
    check "arch: Opencode ExecutorTool opens ExecutorToolsCodec"
        (code.Contains "ExecutorToolsCodec")
    check "arch: Opencode ExecutorTool uses decodeExecutorArgs"
        (code.Contains "decodeExecutorArgs")
    check "arch: Opencode ExecutorTool uses wireDomainFailure for executor decode"
        (code.Contains "wireDomainFailure \"Executor\"")
    check "arch: Opencode ExecutorTool uses toExecuteOptions"
        (code.Contains "toExecuteOptions")
    check "arch: Opencode ExecutorTool must not Dyn.str args language"
        (not (code.Contains "Dyn.str args \"language\""))
    check "arch: Opencode ExecutorTool must not Dyn.str args program"
        (not (code.Contains "Dyn.str args \"program\""))
    check "arch: Opencode ExecutorTool must not Dyn.str args mode"
        (not (code.Contains "Dyn.str args \"mode\""))
    check "arch: Opencode ExecutorTool must not Dyn.str args timeout_type"
        (not (code.Contains "Dyn.str args \"timeout_type\""))

let opencodeExecutorUsesFromOpencode () =
    let code = requireFile "src/Opencode/ExecutorTool.fs" |> nonCommentCode
    check "arch: Opencode ExecutorTool opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Opencode ExecutorTool uses fromOpencode"
        (code.Contains "fromOpencode")
    check "arch: Opencode ExecutorTool uses runtime.Execution for session and directory"
        ((code.Contains "runtime.Execution.SessionId")
         && (code.Contains "runtime.Execution.Directory"))
    check "arch: Opencode ExecutorTool uses executorRequiresSession"
        (code.Contains "executorRequiresSession")
    check "arch: Opencode ExecutorTool must not extractToolContext"
        (not (code.Contains "extractToolContext"))
    check "arch: Opencode ExecutorTool must not Dyn.str tc sessionID"
        (not (code.Contains "Dyn.str tc \"sessionID\""))
    check "arch: Opencode ExecutorTool must not Dyn.str tc directory"
        (not (code.Contains "Dyn.str tc \"directory\""))
    check "arch: Opencode ExecutorTool uses pluginDirectoryFromCtx"
        (code.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode ExecutorTool must not Dyn.str ctx directory"
        (not (code.Contains "Dyn.str ctx \"directory\""))

let opencodeToolSchemaDescriptionsFromCatalog () =
    let code = requireFile "src/Opencode/ToolSchema.fs" |> nonCommentCode
    check "arch: Opencode ToolSchema defines toolDescription"
        (code.Contains "let private toolDescription")
    check "arch: Opencode ToolSchema coder uses toolDescription"
        (code.Contains "let coder = toolDescription")
    check "arch: Opencode ToolSchema must not alias description as coder"
        (not (code.Contains "let coder = description"))
    check "arch: Opencode ToolSchema knowledgeGraphDraftEntriesReq uses Params.kgEntryEntity"
        (code.Contains "Params.kgEntryEntity")
    check "arch: Opencode ToolSchema knowledgeGraphDraftEntriesReq must not inline Knowledge graph entity"
        (not (code.Contains "Knowledge graph entity"))

let opencodeSubagentToolsUsesOpencodeClientCodec () =
    let code = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    check "arch: Opencode SubagentTools opens OpencodeClientCodec"
        (code.Contains "OpencodeClientCodec")
    check "arch: Opencode SubagentTools uses getClientFromPluginCtx"
        (code.Contains "getClientFromPluginCtx")
    check "arch: Opencode SubagentTools must not Dyn.get ctx client"
        (not (code.Contains "Dyn.get ctx \"client\""))

let opencodeKnowledgeGraphToolsUsesFromOpencode () =
    let code = requireFile "src/Opencode/KnowledgeGraphTools.fs" |> nonCommentCode
    check "arch: Opencode KnowledgeGraphTools opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Opencode KnowledgeGraphTools uses fromOpencode"
        (code.Contains "fromOpencode")
    check "arch: Opencode KnowledgeGraphTools uses runtime.Execution for session and directory"
        ((code.Contains "runtime.Execution.SessionId")
         && (code.Contains "runtime.Execution.Directory"))
    check "arch: Opencode KnowledgeGraphTools must not Dyn.str context sessionID"
        (not (code.Contains "Dyn.str context \"sessionID\""))
    check "arch: Opencode KnowledgeGraphTools must not Dyn.str context directory"
        (not (code.Contains "Dyn.str context \"directory\""))
    check "arch: Opencode KnowledgeGraphTools must not Dyn.str ctx directory"
        (not (code.Contains "Dyn.str ctx \"directory\""))
    check "arch: Opencode KnowledgeGraphTools uses pluginDirectoryFromCtx"
        (code.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode KnowledgeGraphTools fetch uses Params.fetchKnowledgeGraphEntity"
        (code.Contains "Params.fetchKnowledgeGraphEntity")
    check "arch: Opencode KnowledgeGraphTools entries uses Params.submitKnowledgeGraphEntries"
        (code.Contains "Params.submitKnowledgeGraphEntries")

let opencodeKgUsesKnowledgeGraphToolsCodec () =
    let code = requireFile "src/Opencode/KnowledgeGraphTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/KnowledgeGraphToolsCodec.fs" |> nonCommentCode
    check "arch: KnowledgeGraphToolsCodec defines decodeFetchEntity"
        (codec.Contains "let decodeFetchEntity")
    check "arch: KnowledgeGraphToolsCodec defines decodeDraftEntries"
        (codec.Contains "let decodeDraftEntries")
    check "arch: KnowledgeGraphToolsCodec defines decodeReturnBookkeeperArgs"
        (codec.Contains "let decodeReturnBookkeeperArgs")
    check "arch: Opencode KnowledgeGraphTools opens KnowledgeGraphToolsCodec"
        (code.Contains "KnowledgeGraphToolsCodec")
    check "arch: Opencode KnowledgeGraphTools uses decodeFetchEntity"
        (code.Contains "decodeFetchEntity")
    check "arch: Opencode KnowledgeGraphTools uses decodeReturnBookkeeperArgs"
        (code.Contains "decodeReturnBookkeeperArgs")
    check "arch: Opencode KnowledgeGraphTools must not open Dyn"
        (not (code.Contains "open VibeFs.Shell.Dyn"))
    check "arch: Opencode KnowledgeGraphTools must not Dyn.get args entries"
        (not (code.Contains "Dyn.get args \"entries\""))
    check "arch: Opencode KnowledgeGraphTools must not parseDraftArray"
        (not (code.Contains "parseDraftArray"))
    check "arch: Opencode KnowledgeGraphTools fetch must not Dyn.str args entity"
        (not (code.Contains "Dyn.str args \"entity\""))
    check "arch: Opencode KnowledgeGraphTools opens ToolExecute"
        (code.Contains "ToolExecute")
    check "arch: Opencode KnowledgeGraphTools fetch decode uses wireDecodeFailure knowledge_graph_fetch"
        (code.Contains "wireDecodeFailure \"knowledge_graph_fetch\"")
    check "arch: Opencode KnowledgeGraphTools return decode uses wireDecodeFailure return_bookkeeper"
        (code.Contains "wireDecodeFailure \"return_bookkeeper\"")
    check "arch: Opencode KnowledgeGraphTools must not formatDomainError on decode"
        (not (code.Contains "formatDomainError"))