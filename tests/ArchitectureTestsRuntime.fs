module VibeFs.Tests.ArchitectureTestsRuntime

open Fable.Core
open Fable.Core.JsInterop
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

let sessionIoUsesOpencodeContextCodec () =
    let code = requireFile "src/Opencode/SessionIo.fs" |> nonCommentCode
    check "arch: SessionIo uses getAbortSignalFromContext"
        (code.Contains "getAbortSignalFromContext")
    check "arch: SessionIo must not read context.abort via Dyn.get locally"
        (not (code.Contains "Dyn.get context \"abort\""))

let sessionIoUsesOpencodeSessionPromptCodec () =
    let code = requireFile "src/Opencode/SessionIo.fs" |> nonCommentCode
    check "arch: SessionIo uses tryDecodePromptModelFromPayload"
        (code.Contains "tryDecodePromptModelFromPayload")
    check "arch: SessionIo must not call tryDecodePromptModelFromModelString directly"
        (not (code.Contains "tryDecodePromptModelFromModelString"))
    check "arch: SessionIo must not define private tryReadPromptModel"
        (not (code.Contains "let private tryReadPromptModel"))

let sessionIoUsesOpencodeSessionSpawnCodec () =
    let code = requireFile "src/Opencode/SessionIo.fs" |> nonCommentCode
    check "arch: SessionIo uses decodeChildSessionIdFromCreateResult"
        (code.Contains "decodeChildSessionIdFromCreateResult")
    check "arch: SessionIo must not Dyn.str createResult data id"
        (not (code.Contains "Dyn.str (Dyn.get createResult \"data\") \"id\""))
    check "arch: SessionIo must not Dyn.get createResult data"
        (not (code.Contains "Dyn.get createResult \"data\""))
    let startIdx = code.IndexOf "let startSubagentSession"
    check "arch: SessionIo startSubagentSession exists" (startIdx >= 0)
    let startWindow =
        if startIdx >= 0 then
            code.Substring(startIdx, min 1200 (code.Length - startIdx))
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

let sessionIoUsesOpencodeClientCodec () =
    let code = requireFile "src/Opencode/SessionIo.fs" |> nonCommentCode
    check "arch: SessionIo opens OpencodeClientCodec"
        (code.Contains "OpencodeClientCodec")
    check "arch: SessionIo uses getSessionApiFromClient"
        (code.Contains "getSessionApiFromClient")
    check "arch: SessionIo must not Dyn.get client session"
        (not (code.Contains "Dyn.get client \"session\""))
    check "arch: SessionIo opens ToolResult"
        (code.Contains "ToolResult")
    check "arch: SessionIo promptWithAbort client failure uses wireEncodeToolError OpencodeClient"
        (code.Contains "wireEncodeToolError \"OpencodeClient\"")
    check "arch: SessionIo must not formatDomainError"
        (not (code.Contains "formatDomainError"))

let sessionIoUsesSubagentResultPath () =
    let sessionIo = requireFile "src/Opencode/SessionIo.fs" |> nonCommentCode
    let spawn = requireFile "src/Shell/SessionIoSpawn.fs" |> nonCommentCode
    check "arch: SessionIo defines runSubagentCoreResult"
        (sessionIo.Contains "let runSubagentCoreResult")
    check "arch: SessionIo spawn path uses Result<string, DomainError>"
        (sessionIo.Contains "Result<string, DomainError>")
    check "arch: SessionIoSpawn defines formatSubagentReport"
        (spawn.Contains "let formatSubagentReport")
    check "arch: SessionIo runSubagent is Result public API"
        (sessionIo.Contains "let runSubagent" && sessionIo.Contains "JS.Promise<Result<string, DomainError>>")

let private opencodeClientSessionDynCtxRe =
    System.Text.RegularExpressions.Regex(@"Dyn\.get\s+ctx\s+""client""")
let private opencodeClientSessionDynClientRe =
    System.Text.RegularExpressions.Regex(@"Dyn\.get\s+client\s+""session""")

let opencodeNoDirectClientSessionDyn () =
    for f in fsFiles "src/Opencode" do
        if f <> "OpencodeClientCodec.fs" then
            let code = requireFile ("src/Opencode/" + f) |> nonCommentCode
            check ("arch: Opencode/" + f + " no Dyn.get ctx client")
                (not (opencodeClientSessionDynCtxRe.IsMatch code))
            check ("arch: Opencode/" + f + " no Dyn.get client session")
                (not (opencodeClientSessionDynClientRe.IsMatch code))

let sessionExecutorCreateForScope () =
    let code = requireFile "src/Shell/SessionExecutor.fs" |> nonCommentCode
    check "arch: SessionExecutor exposes createForScope"
        (code.Contains "createForScope")

let pluginInjectsSessionScopeForExecutor () =
    let muxPlugin = requireFile "src/Mux/Plugin.fs" |> nonCommentCode
    let muxHost = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    check "arch: Mux Plugin createToolCatalog passes sessionScope to executorTool"
        (muxPlugin.Contains "executorTool deps toolNames null sessionScope")
    check "arch: Mux HostTools executor uses sessionScope.EnqueuePerSession"
        (muxHost.Contains "sessionScope.EnqueuePerSession")
    let pluginCore = requireFile "src/Opencode/PluginCore.fs" |> nonCommentCode
    let tools = requireFile "src/Opencode/Tools.fs" |> nonCommentCode
    let executor = requireFile "src/Opencode/ExecutorTool.fs" |> nonCommentCode
    check "arch: Opencode PluginCore createTools passes scope"
        (pluginCore.Contains "createTools host childAgentRegistry finderCache ctx knowledgeGraphRuntime reviewStore knowledgeGraphEnabled scope")
    check "arch: Opencode Tools passes sessionScope to executorTool"
        (tools.Contains "executorTool registry ctx sessionScope")
    check "arch: Opencode ExecutorTool uses sessionScope.EnqueuePerSession"
        (executor.Contains "sessionScope.EnqueuePerSession")

let runtimeScopeNoGetDefault () =
    let code = requireFile "src/Shell/RuntimeScope.fs" |> nonCommentCode
    check "arch: RuntimeScope must not define getDefault"
        (not (code.Contains "let getDefault"))
    check "arch: RuntimeScope must not call getDefault"
        (not (code.Contains "getDefault"))

let sessionExecutorNoModuleMutableQueues () =
    let code = requireFile "src/Shell/SessionExecutor.fs" |> nonCommentCode
    check "arch: SessionExecutor must not define module enqueuePerSession"
        (not (System.Text.RegularExpressions.Regex(@"let\s+enqueuePerSession\b").IsMatch code))
    check "arch: SessionExecutor must not call getDefault"
        (not (code.Contains "getDefault"))
    check "arch: SessionExecutor no module-level mutable queues"
        (not (code.Contains "mutable queues"))
    let scope = requireFile "src/Shell/RuntimeScope.fs" |> nonCommentCode
    check "arch: RuntimeScope holds sessionQueues map"
        (scope.Contains "sessionQueues")
    check "arch: RuntimeScope defines EnqueuePerSession"
        (scope.Contains "member _.EnqueuePerSession")
    check "arch: RuntimeScope defines ClearSessionQueues"
        (scope.Contains "member _.ClearSessionQueues")