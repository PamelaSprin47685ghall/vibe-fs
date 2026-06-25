module VibeFs.Tests.ArchitectureTestsMuxToolAuxKg

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.ArchitectureTestsSupport

let muxKnowledgeGraphStartBookkeeperUsesFromMuxConfig () =
    let muxCore = requireFile "src/Mux/KnowledgeGraphRuntimeMux.fs" |> nonCommentCode
    let muxSubmit = requireFile "src/Mux/KnowledgeGraphRuntimeMuxSubmit.fs" |> nonCommentCode
    let code = muxCore + muxSubmit
    check "arch: Mux KnowledgeGraphRuntimeMux opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Mux KnowledgeGraphRuntimeMux StartBookkeeperAppend uses fromMuxConfig"
        (code.Contains "fromMuxConfig")
    check "arch: Mux KnowledgeGraphRuntimeMux uses runtime.Execution.Directory for root"
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

let muxPluginRegistrationOrchestration () =
    let reg = requireFile "src/Mux/PluginRegistration.fs" |> nonCommentCode
    let parts = requireFile "src/Mux/PluginRegistrationParts.fs" |> nonCommentCode
    check "arch: PluginRegistration createRegistration uses createScope" (reg.Contains "createScope")
    check "arch: PluginRegistration createRegistration uses createWrapperExecution" (reg.Contains "createWrapperExecution")
    check "arch: PluginRegistration createRegistration uses createMessageTransforms" (reg.Contains "createMessageTransforms")
    check "arch: PluginRegistration createRegistration uses createEventHooksSlashAndPolicy" (reg.Contains "createEventHooksSlashAndPolicy")
    check "arch: PluginRegistration createRegistration uses registerTestHooks" (reg.Contains "registerTestHooks")
    check "arch: PluginRegistrationParts defines assembleRegistrationObject" (parts.Contains "assembleRegistrationObject")
    check "arch: PluginRegistration must not define buildRegistrationObject" (not (reg.Contains "buildRegistrationObject"))