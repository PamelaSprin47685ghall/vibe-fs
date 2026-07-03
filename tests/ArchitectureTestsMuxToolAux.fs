module Wanxiangshu.Tests.ArchitectureTestsMuxToolAux

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

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
    check "arch: Mux ReviewToolsMux submit syncs review from event log"
        (code.Contains "syncReviewFromEventLogDir")

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

let muxPluginCatalogToolExecuteAfterUsesLivelockGuard () =
    let code = requireFile "src/Mux/PluginCatalog.fs" |> nonCommentCode
    check "arch: Mux PluginCatalog toolExecuteAfter uses LivelockGuard.check"
        (code.Contains "LivelockGuard.check")
    check "arch: Mux PluginCatalog toolExecuteAfter must not be Promise.lift noop"
        (not (code.Contains "toolExecuteAfter (input: obj) (output: obj) : JS.Promise<unit> = Promise.lift ()"))

let muxSlashCommandsLoopUsesDepsDirectory () =
    let code = requireFile "src/Mux/SlashCommands.fs" |> nonCommentCode
    check "arch: Mux SlashCommands createLoopOnlyCommand takes deps"
        (code.Contains "let createLoopOnlyCommand (deps: obj)")
    check "arch: Mux SlashCommands loop path reads deps directory"
        (code.Contains "eventLogRootFromDeps")
    check "arch: Mux SlashCommands syncReview must not use deps cwd for event log"
        (not (code.Contains "Dyn.str deps \"cwd\""))

let muxSubagentToolsUsesSimpleArgsCodec () =
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let dispatcher = requireFile "src/Shell/SubagentDispatcher.fs" |> nonCommentCode
    check "arch: Mux SubagentTools must not open SubagentSimpleArgsCodec"
        (not (mux.Contains "SubagentSimpleArgsCodec"))
    check "arch: SubagentDispatcher must not decodeMeditatorArgs"
        (not (dispatcher.Contains "decodeMeditatorArgs"))
    check "arch: SubagentDispatcher uses decodeToolInvocation"
        (dispatcher.Contains "decodeToolInvocation")
