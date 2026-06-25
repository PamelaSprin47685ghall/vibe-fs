module VibeFs.Tests.ArchitectureTestsMuxToolAux

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.ArchitectureTestsSupport

let muxReviewUsesToolCopy () =
    let code = requireFile "src/Mux/ReviewToolsMux.fs" |> nonCommentCode
    check "arch: Mux ReviewToolsMux opens ToolCopy"
        (code.Contains "ToolCopy")
    check "arch: Mux ReviewToolsMux uses muxSubmitReviewRequiresWorkspaceId"
        (code.Contains "muxSubmitReviewRequiresWorkspaceId")
    check "arch: Mux ReviewToolsMux uses submitReviewInProgress"
        (code.Contains "submitReviewInProgress")
    check "arch: Mux ReviewToolsMux uses submitReviewNotNeeded"
        (code.Contains "submitReviewNotNeeded")
    check "arch: Mux ReviewToolsMux must not inline submit_review requires workspaceId"
        (not (code.Contains "submit_review requires workspaceId"))
    check "arch: Mux ReviewToolsMux must not inline review already in progress"
        (not (code.Contains "A review is already in progress for this session."))
    check "arch: Mux ReviewToolsMux must not inline you do not need review"
        (not (code.Contains "You do not need review. Just continue with your work."))

let muxReviewUsesFromMuxConfig () =
    let code = requireFile "src/Mux/ReviewToolsMux.fs" |> nonCommentCode
    check "arch: Mux ReviewToolsMux opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Mux ReviewToolsMux submit uses fromMuxConfig"
        (code.Contains "fromMuxConfig")
    check "arch: Mux ReviewToolsMux config decode uses wireEncodeToolError MuxConfig"
        (code.Contains "wireEncodeToolError \"MuxConfig\"")
    check "arch: Mux ReviewToolsMux uses Execution.WorkspaceId"
        (code.Contains "runtime.Execution.WorkspaceId")
    check "arch: Mux ReviewToolsMux must not strField config workspaceId"
        (not (code.Contains "strField config \"workspaceId\""))
    check "arch: Mux ReviewToolsMux must not Dyn.str config workspaceId"
        (not (code.Contains "Dyn.str config \"workspaceId\""))

let muxReviewUsesReviewToolsCodec () =
    let code = requireFile "src/Mux/ReviewToolsMux.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/ReviewToolsCodec.fs" |> nonCommentCode
    check "arch: Mux ReviewToolsMux opens ReviewToolsCodec"
        (code.Contains "ReviewToolsCodec")
    check "arch: Mux ReviewToolsMux submit uses decodeSubmitReviewArgs"
        (code.Contains "decodeSubmitReviewArgs")
    check "arch: Mux ReviewToolsMux submit must not strField args report"
        (not (code.Contains "strField args \"report\""))
    check "arch: Mux ReviewToolsMux submit must not requireStrArray args affectedFiles"
        (not (code.Contains "requireStrArray args \"affectedFiles\""))
    check "arch: ReviewToolsCodec defines decodeSubmitReviewArgs"
        (codec.Contains "let decodeSubmitReviewArgs")
    check "arch: Mux ReviewToolsMux opens ToolExecute"
        (code.Contains "ToolExecute")
    check "arch: Mux ReviewToolsMux submit decode uses wireDecodeFailure submit_review"
        (code.Contains "wireDecodeFailure \"submit_review\"")
    check "arch: Mux ReviewToolsMux submit decode must not return formatDomainError"
        (not (code.Contains "| Error e -> return formatDomainError e"))

let muxWrappersSyntaxUsesFromMuxConfig () =
    let code = requireFile "src/Mux/Wrappers.fs" |> nonCommentCode
    check "arch: Mux Wrappers opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Mux Wrappers applySyntaxCheck uses fromMuxConfig"
        (code.Contains "fromMuxConfig")
    check "arch: Mux Wrappers applySyntaxCheck uses Execution.Directory"
        (code.Contains "runtime.Execution.Directory")
    check "arch: Mux Wrappers applySyntaxCheck must not Dyn.str config cwd"
        (not (code.Contains "Dyn.str config \"cwd\""))

let muxWrappersTodoUsesWorkBacklogToolsCodec () =
    let code = requireFile "src/Mux/Wrappers.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/WorkBacklogToolsCodec.fs" |> nonCommentCode
    check "arch: WorkBacklogToolsCodec defines decodeTodoWriteArgs"
        (codec.Contains "let decodeTodoWriteArgs")
    check "arch: WorkBacklogToolsCodec defines decodeTodoToolOpts"
        (codec.Contains "let decodeTodoToolOpts")
    check "arch: Mux Wrappers opens WorkBacklogToolsCodec"
        (code.Contains "WorkBacklogToolsCodec")
    check "arch: Mux Wrappers captureTodoReport uses decodeTodoWriteArgs"
        (code.Contains "decodeTodoWriteArgs")
    check "arch: Mux Wrappers captureTodoReport must not strField args content"
        (not (code.Contains "strField args \"content\""))
    check "arch: Mux Wrappers todo decode uses wireDecodeFailure todowrite"
        (code.Contains "wireDecodeFailure \"todowrite\"")
    check "arch: Mux Wrappers must not return formatDomainError for todo decode"
        (not (code.Contains "| Error e -> return formatDomainError e"))

let muxWrappersUsesJsonSchemaBuilders () =
    let wrappers = requireFile "src/Mux/Wrappers.fs" |> nonCommentCode
    let builders = requireFile "src/Shell/JsonSchemaBuilders.fs" |> nonCommentCode
    check "arch: Mux Wrappers opens JsonSchemaBuilders"
        (wrappers.Contains "JsonSchemaBuilders")
    check "arch: JsonSchemaBuilders defines jsonStrProp"
        (builders.Contains "let jsonStrProp")
    check "arch: Mux Wrappers must not inline strProp createObj schema"
        (not (wrappers.Contains "let strProp (desc: string) : obj = createObj"))
    check "arch: MuxJsonSchema delegates muxStrReq to JsonSchemaBuilders"
        (requireFile "src/Shell/MuxJsonSchema.fs" |> nonCommentCode |> fun s -> s.Contains "jsonStrReq")

let muxSubagentToolsUsesMuxJsonSchema () =
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let schema = requireFile "src/Shell/MuxJsonSchema.fs" |> nonCommentCode
    check "arch: Mux SubagentTools opens MuxJsonSchema"
        (mux.Contains "MuxJsonSchema")
    check "arch: MuxJsonSchema defines muxCoderIntentsSchema"
        (schema.Contains "let muxCoderIntentsSchema")
    check "arch: Mux SubagentTools must not define private muxCoderIntentsSchema"
        (not (mux.Contains "let private muxCoderIntentsSchema"))

let muxSubagentToolsUsesMuxSpawnUniverse () =
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let hostTools = requireFile "src/Kernel/HostTools.fs" |> nonCommentCode
    check "arch: HostTools defines muxSpawnToolUniverse"
        (hostTools.Contains "let muxSpawnToolUniverse")
    check "arch: Mux SubagentTools references muxSpawnToolUniverse"
        (mux.Contains "muxSpawnToolUniverse")
    check "arch: Mux SubagentTools must not define private muxHostToolNames"
        (not (mux.Contains "let private muxHostToolNames"))
    check "arch: Mux SubagentTools must not embed mux_agents_read tool-universe literal"
        (not (mux.Contains "mux_agents_read"))

let muxSubagentToolsUsesSimpleArgsCodec () =
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let shell = requireFile "src/Shell/MuxSubagentToolExecute.fs" |> nonCommentCode
    check "arch: Mux SubagentTools must not open SubagentSimpleArgsCodec"
        (not (mux.Contains "SubagentSimpleArgsCodec"))
    check "arch: MuxSubagentToolExecute must not decodeMeditatorArgs"
        (not (shell.Contains "decodeMeditatorArgs"))
    check "arch: MuxSubagentToolExecute uses decodeToolInvocation"
        (shell.Contains "decodeToolInvocation")

let muxKnowledgeGraphStartBookkeeperUsesFromMuxConfig () =
    let code = requireFile "src/Mux/KnowledgeGraphTools.fs" |> nonCommentCode
    check "arch: Mux KnowledgeGraphTools opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Mux KnowledgeGraphTools StartBookkeeperAppend uses fromMuxConfig"
        (code.Contains "fromMuxConfig")
    check "arch: Mux KnowledgeGraphTools uses runtime.Execution.Directory for root"
        (code.Contains "runtime.Execution.Directory")
    check "arch: Mux KnowledgeGraphTools uses muxConfigDirectoryFallback"
        (code.Contains "muxConfigDirectoryFallback")
    check "arch: Mux KnowledgeGraphTools must not Dyn.str config cwd"
        (not (code.Contains "Dyn.str config \"cwd\""))

let muxKgToolDefsUsesKnowledgeGraphToolsCodec () =
    let code = requireFile "src/Mux/KnowledgeGraphToolDefs.fs" |> nonCommentCode
    check "arch: Mux KnowledgeGraphToolDefs opens KnowledgeGraphToolsCodec"
        (code.Contains "KnowledgeGraphToolsCodec")
    check "arch: Mux KnowledgeGraphToolDefs uses decodeFetchEntity"
        (code.Contains "decodeFetchEntity")
    check "arch: Mux KnowledgeGraphToolDefs uses decodeReturnBookkeeperArgs"
        (code.Contains "decodeReturnBookkeeperArgs")
    check "arch: Mux KnowledgeGraphToolDefs must not parseDraftArray"
        (not (code.Contains "parseDraftArray"))
    check "arch: Mux KnowledgeGraphToolDefs fetch must not Dyn.str args entity"
        (not (code.Contains "Dyn.str args \"entity\""))
    check "arch: Mux KnowledgeGraphToolDefs must not Dyn.get args entries"
        (not (code.Contains "Dyn.get args \"entries\""))
    check "arch: Mux KnowledgeGraphToolDefs must not decodeDraftEntries in execute"
        (not (code.Contains "decodeDraftEntries"))
    check "arch: Mux KnowledgeGraphToolDefs opens ToolExecute"
        (code.Contains "ToolExecute")
    check "arch: Mux KnowledgeGraphToolDefs fetch decode uses wireDecodeFailure knowledge_graph_fetch"
        (code.Contains "wireDecodeFailure \"knowledge_graph_fetch\"")
    check "arch: Mux KnowledgeGraphToolDefs return decode uses wireDecodeFailure return_bookkeeper"
        (code.Contains "wireDecodeFailure \"return_bookkeeper\"")
    check "arch: Mux KnowledgeGraphToolDefs fromMuxConfig uses wireEncodeToolError MuxConfig"
        (code.Contains "wireEncodeToolError \"MuxConfig\"")
    check "arch: Mux KnowledgeGraphToolDefs must not resolveStr (formatDomainError e)"
        (not (code.Contains "resolveStr (formatDomainError e)"))

let muxKgToolDefsUsesFromMuxConfig () =
    let code = requireFile "src/Mux/KnowledgeGraphToolDefs.fs" |> nonCommentCode
    check "arch: Mux KnowledgeGraphToolDefs opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Mux KnowledgeGraphToolDefs uses fromMuxConfig"
        (code.Contains "fromMuxConfig")
    check "arch: Mux KnowledgeGraphToolDefs uses runtime.Execution.SessionId"
        (code.Contains "runtime.Execution.SessionId")
    check "arch: Mux KnowledgeGraphToolDefs uses runtime.Execution.Directory"
        (code.Contains "runtime.Execution.Directory")
    check "arch: Mux KnowledgeGraphToolDefs must not Dyn.str config sessionID"
        (not (code.Contains "Dyn.str config \"sessionID\""))
    check "arch: Mux KnowledgeGraphToolDefs must not Dyn.str config directory"
        (not (code.Contains "Dyn.str config \"directory\""))
    check "arch: Mux KnowledgeGraphToolDefs must not Dyn.str pluginConfig cwd"
        (not (code.Contains "Dyn.str pluginConfig \"cwd\""))
    check "arch: Mux KnowledgeGraphToolDefs condition uses muxConfigDirectoryFallback"
        (code.Contains "muxConfigDirectoryFallback")

let muxAiSettingsUsesMuxAiSettingsCodec () =
    let code = requireFile "src/Mux/AiSettings.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/MuxAiSettingsCodec.fs" |> nonCommentCode
    check "arch: MuxAiSettingsCodec defines decodeMuxDelegateConfig"
        (codec.Contains "let decodeMuxDelegateConfig")
    check "arch: MuxAiSettingsCodec defines decodeMuxParentRuntimeEnv"
        (codec.Contains "let decodeMuxParentRuntimeEnv")
    check "arch: MuxAiSettingsCodec defines decodeAgentAiEntryScalars"
        (codec.Contains "let decodeAgentAiEntryScalars")
    check "arch: Mux AiSettings opens MuxAiSettingsCodec"
        (code.Contains "MuxAiSettingsCodec")
    check "arch: Mux AiSettings uses decodeMuxDelegateConfig"
        (code.Contains "decodeMuxDelegateConfig")
    check "arch: Mux AiSettings must not Dyn.str config runtime cwd"
        (not (code.Contains "Dyn.get config runtime/cwd"))

let muxDelegateUsesDelegateToolsCodec () =
    let code = requireFile "src/Mux/Delegate.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/DelegateToolsCodec.fs" |> nonCommentCode
    check "arch: DelegateToolsCodec defines decodeTaskCreateResult"
        (codec.Contains "let decodeTaskCreateResult")
    check "arch: DelegateToolsCodec defines decodeTaskReport"
        (codec.Contains "let decodeTaskReport")
    check "arch: Mux Delegate opens DelegateToolsCodec"
        (code.Contains "DelegateToolsCodec")
    check "arch: Mux Delegate uses decodeTaskCreateResult"
        (code.Contains "decodeTaskCreateResult")
    check "arch: Mux Delegate uses decodeTaskReport"
        (code.Contains "decodeTaskReport")
    check "arch: Mux Delegate uses wireDomainFailure delegate create"
        (code.Contains "wireDomainFailure \"delegate.create\"")
    check "arch: Mux Delegate uses wireDomainFailure delegate report"
        (code.Contains "wireDomainFailure \"delegate.report\"")
    check "arch: Mux Delegate must not formatDomainError for delegate"
        (not (code.Contains "formatDomainError"))
    check "arch: Mux Delegate must not Failed to create inline"
        (not (code.Contains "Failed to create"))

let muxHookInputCodecExecutorReadOnlyUsesCodec () =
    let code = requireFile "src/Shell/MuxHookInputCodec.fs" |> nonCommentCode
    check "arch: MuxHookInputCodec opens ExecutorToolsCodec" (code.Contains "ExecutorToolsCodec")
    check "arch: MuxHookInputCodec isReadOnlyExecutorMux uses peekExecutorMode" (code.Contains "peekExecutorMode")
    check "arch: MuxHookInputCodec isReadOnlyExecutorMux must not Dyn.str args mode" (not (code.Contains "Dyn.str args \"mode\""))