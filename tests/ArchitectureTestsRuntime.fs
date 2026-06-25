module VibeFs.Tests.ArchitectureTestsRuntime

open VibeFs.Tests.Assert
open VibeFs.Tests.ArchitectureTestsSupport

let toolContextCodecUsesKernelType () =
    let codec = requireFile "src/Shell/ToolContextCodec.fs" |> nonCommentCode
    let kernel = requireFile "src/Kernel/ToolContext.fs" |> nonCommentCode
    check "arch: ToolContextCodec opens Kernel.ToolContext"
        (codec.Contains "Kernel.ToolContext")
    check "arch: ToolContextCodec must not define ToolExecutionContext record"
        (not (codec.Contains "type ToolExecutionContext"))
    check "arch: Kernel.ToolContext defines ToolExecutionContext"
        (kernel.Contains "type ToolExecutionContext")
    check "arch: Kernel.ToolExecutionContext must not include AbortSignal"
        (not (kernel.Contains "AbortSignal"))

let toolContextCodecAbortFree () =
    let codec = requireFile "src/Shell/ToolContextCodec.fs" |> nonCommentCode
    check "arch: ToolContextCodec must not use getAbortSignalFromContext"
        (not (codec.Contains "getAbortSignalFromContext"))
    check "arch: ToolContextCodec must not Dyn.get context abort"
        (not (codec.Contains "Dyn.get context \"abort\""))
    check "arch: ToolContextCodec must not Dyn.get config abortSignal"
        (not (codec.Contains "Dyn.get config \"abortSignal\""))

let toolRuntimeContextAbortFromShellCodec () =
    let code = requireFile "src/Shell/ToolRuntimeContext.fs" |> nonCommentCode
    check "arch: ToolRuntimeContext fromOpencode uses getAbortSignalFromContext"
        (code.Contains "getAbortSignalFromContext")
    check "arch: ToolRuntimeContext fromOpencode must not use execution.AbortSignal"
        (not (code.Contains "execution.AbortSignal"))
    check "arch: ToolRuntimeContext fromMuxConfig uses config abortSignal"
        (code.Contains "Dyn.get config \"abortSignal\"")

let sessionIoUsesToolContextCodec () =
    let code = requireFile "src/Opencode/SessionIo.fs" |> nonCommentCode
    check "arch: SessionIo uses decodeOpencodeToolContext"
        (code.Contains "decodeOpencodeToolContext")
    check "arch: SessionIo must not define private firstString"
        (not (code.Contains "let private firstString"))

let private sessionIoSubagentCode () =
    (requireFile "src/Opencode/SessionIo.fs" |> nonCommentCode)
    + (requireFile "src/Opencode/SessionIoSubagent.fs" |> nonCommentCode)

let sessionIoUsesOpencodeContextCodec () =
    let code = sessionIoSubagentCode ()
    check "arch: SessionIo uses getAbortSignalFromContext"
        (code.Contains "getAbortSignalFromContext")
    check "arch: SessionIo must not read context.abort via Dyn.get locally"
        (not (code.Contains "Dyn.get context \"abort\""))

let sessionIoUsesOpencodeSessionPromptCodec () =
    let code = sessionIoSubagentCode ()
    check "arch: SessionIo uses tryDecodePromptModelFromPayload"
        (code.Contains "tryDecodePromptModelFromPayload")
    check "arch: SessionIo must not call tryDecodePromptModelFromModelString directly"
        (not (code.Contains "tryDecodePromptModelFromModelString"))
    check "arch: SessionIo must not define private tryReadPromptModel"
        (not (code.Contains "let private tryReadPromptModel"))

let sessionIoUsesOpencodeSessionSpawnCodec () =
    let code = sessionIoSubagentCode ()
    check "arch: SessionIo uses decodeChildSessionIdFromCreateResult"
        (code.Contains "decodeChildSessionIdFromCreateResult")
    check "arch: SessionIo must not Dyn.str createResult data id"
        (not (code.Contains "Dyn.str (Dyn.get createResult \"data\") \"id\""))
    check "arch: SessionIo must not Dyn.get createResult data"
        (not (code.Contains "Dyn.get createResult \"data\""))
    let subagentOnly = requireFile "src/Opencode/SessionIoSubagent.fs" |> nonCommentCode
    let startIdx = subagentOnly.IndexOf "let startSubagentSession"
    check "arch: SessionIo startSubagentSession exists" (startIdx >= 0)
    let startWindow =
        if startIdx >= 0 then
            subagentOnly.Substring(startIdx, min 1200 (subagentOnly.Length - startIdx))
        else
            ""
    check "arch: SessionIo startSubagentSession propagates spawn decode as DomainError"
        (startWindow.Contains "decodeChildSessionIdFromCreateResult"
         && (startWindow.Contains "return Error err" || startWindow.Contains "formatDomainError"))

let agentConfigUsesOpencodeAgentConfigWire () =
    let code = requireFile "src/Opencode/AgentConfig.fs" |> nonCommentCode
    let wire = requireFile "src/Shell/OpencodeAgentConfigWire.fs" |> nonCommentCode
    check "arch: OpencodeAgentConfigWire module exists" (wire.Contains "module VibeFs.Shell.OpencodeAgentConfigWire")
    check "arch: AgentConfig uses decodeUserAgentScalars"
        (code.Contains "decodeUserAgentScalars")
    check "arch: AgentConfig uses encodeAgentScalarsRecord"
        (code.Contains "encodeAgentScalarsRecord")
    check "arch: AgentConfig uses OpencodeAgentConfigWire.applyAgentConfigFor"
        (code.Contains "OpencodeAgentConfigWire.applyAgentConfigFor")
    check "arch: AgentConfig must not Dyn.str userAgent prompt"
        (not (code.Contains "Dyn.str userAgent \"prompt\""))
    check "arch: AgentConfig must not Dyn.str userAgent mode"
        (not (code.Contains "Dyn.str userAgent \"mode\""))
    check "arch: AgentConfig must not Dyn.keys"
        (not (code.Contains "Dyn.keys"))
    check "arch: AgentConfig must not Dyn.get cfg"
        (not (code.Contains "Dyn.get cfg"))
    check "arch: AgentConfig must not Dyn.get prepared"
        (not (code.Contains "Dyn.get prepared"))
    check "arch: AgentConfig must not injectAgentDisables"
        (not (code.Contains "injectAgentDisables"))
    check "arch: wire owns mergeConfigObj"
        (wire.Contains "let mergeConfigObj")
    check "arch: wire owns disableMimoMemoryAndCheckpoint"
        (wire.Contains "let disableMimoMemoryAndCheckpoint")

let fuzzyIteratorStoreOnRuntimeScope () =
    let store = requireFile "src/Shell/FuzzyIteratorStore.fs" |> nonCommentCode
    let scope = requireFile "src/Shell/RuntimeScope.fs" |> nonCommentCode
    check "arch: FuzzyIteratorStore no globalIteratorStore"
        (not (store.Contains "globalIteratorStore"))
    check "arch: RuntimeScope exposes IteratorStore"
        (scope.Contains "member _.IteratorStore")
    check "arch: RuntimeScope creates typed iterator store"
        (scope.Contains "createTypedIteratorStore 200")

let fuzzySearchNoDefaultIteratorStore () =
    let code = requireFile "src/Shell/FuzzySearch.fs" |> nonCommentCode
    check "arch: FuzzySearch must not fall back to getDefault IteratorStore"
        (not (code.Contains "getDefault().IteratorStore"))

let muxMessageTransformNoModuleBacklogSession () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform must not own module-level BacklogSession()"
        (not (code.Contains "let private backlogSession = BacklogSession()"))
    check "arch: Mux MessageTransform must not own module-level BacklogSession default"
        (not (code.Contains "let backlogSession = BacklogSession()"))
    check "arch: Mux MessageTransform accepts injected BacklogSession"
        (code.Contains "backlogSession: BacklogSession")

let backlogSessionNoGetDefaultFallback () =
    for path in [| "src/Opencode/BacklogSession.fs"; "src/Mux/BacklogSession.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " must not use defaultArg scope getDefault")
            (not (code.Contains "defaultArg scope"))
        check ("arch: " + path + " must not call getDefault")
            (not (code.Contains "getDefault"))

let private moduleProjectionLetRe name =
    System.Text.RegularExpressions.Regex(@"let\s+" + name + @"\b")

let runtimeScopeNoModuleProjectionHelpers () =
    let code = requireFile "src/Shell/RuntimeScope.fs" |> nonCommentCode
    for name in [| "captureReport"; "takeReport"; "tryGetReport"; "storeBacklog"; "tryGetBacklog" |] do
        check ("arch: RuntimeScope must not define module " + name)
            (not ((moduleProjectionLetRe name).IsMatch code))
    check "arch: RuntimeScope must not define projectionOf"
        (not (code.Contains "projectionOf"))

let backlogSessionCodecNoReportFromFlatPartDefault () =
    let codec = requireFile "src/Shell/BacklogSessionCodec.fs" |> nonCommentCode
    check "arch: BacklogSessionCodec defines reportFromFlatPartWithProjection"
        (codec.Contains "let reportFromFlatPartWithProjection")
    check "arch: BacklogSessionCodec must not define reportFromFlatPart"
        (not (reportFromFlatPartDefRe.IsMatch codec))
    check "arch: BacklogSessionCodec must not call getDefault"
        (not (codec.Contains "getDefault"))

let opencodeMessageTransformNoLocalApplyReadDedup () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform no local applyReadDedup"
        (not (code.Contains "let private applyReadDedup"))
    check "arch: Opencode MessageTransform uses ReadDedupOpenCode"
        (code.Contains "ReadDedupOpenCode")
    check "arch: Opencode MessageTransform calls deduplicateOpencodeReadPartsInPlace"
        (code.Contains "deduplicateOpencodeReadPartsInPlace")