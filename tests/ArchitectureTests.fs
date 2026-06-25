module VibeFs.Tests.ArchitectureTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.ArchitectureTestsSupport

/// Kernel layer must stay free of FFI, Dyn, obj, Shell references.
/// Enforced at the directory level (src/Kernel/*.fs) regardless of
/// compilation-unit topology — a single-project merge must not weaken this.
let kernelBoundary () =
    for f in fsFiles "src/Kernel" do
        let path = "src/Kernel/" + f
        let content = requireFile path
        check ("arch: " + f + " createObj-free") (not (content.Contains "createObj"))
        check ("arch: " + f + " Dyn-free") (not (content.Contains "Dyn."))
        check ("arch: " + f + " no open Shell") (not (content.Contains "open VibeFs.Shell"))
        check ("arch: " + f + " obj-type-free") (not (objTypeRe.IsMatch content))
        check ("arch: " + f + " box-free") (not (boxRe.IsMatch content))
        let code = nonCommentCode content
        check ("arch: " + f + " unbox-free") (not (code.Contains "unbox"))

let kernelNoEmptyDefault () =
    for f in fsFiles "src/Kernel" do
        let content = requireFile ("src/Kernel/" + f)
        check ("arch: " + f + " no empty-string default") (not (emptyDefaultRe.IsMatch content))

let shellLayering () =
    for f in fsFiles "src/Shell" do
        let content = requireFile ("src/Shell/" + f)
        check ("arch: " + f + " no Opencode ref") (not (content.Contains "VibeFs.Opencode"))
        check ("arch: " + f + " no Mux ref") (not (content.Contains "VibeFs.Mux"))

let noBuiltinDictionary () =
    for dir in [|"src/Kernel"; "src/Shell"; "src/Mux"; "src/Opencode"|] do
        for f in fsFiles dir do
            let content = requireFile (dir + "/" + f)
            check ("arch: " + f + " no Dictionary") (not (content.Contains "Dictionary"))

let fileBodyUnder300 () =
    for dir in [|"src/Kernel"; "src/Shell"; "src/Mux"; "src/Opencode"|] do
        for f in fsFiles dir do
            let content = requireFile (dir + "/" + f)
            let lineCount = content.Length - content.Replace("\n", "").Length
            check ("arch: " + dir + "/" + f + " <=300 lines") (lineCount <= 300)

let noDanglingMarkers () =
    for dir in [|"src/Kernel"; "src/Shell"; "src/Mux"; "src/Opencode"|] do
        for f in fsFiles dir do
            let content = requireFile (dir + "/" + f)
            check ("arch: " + f + " no TODO") (not (content.Contains "TODO"))
            check ("arch: " + f + " no FIXME") (not (content.Contains "FIXME"))
            check ("arch: " + f + " no HACK") (not (content.Contains "HACK"))

let opencodeHookSchemaNoDirectZodImport () =
    let content = requireFile "src/Opencode/HookSchema.fs"
    check "arch: HookSchema no direct zod import" (not (content.Contains "import \"z\" \"zod\""))

let hookSchemaNoDuplicateMethodologySchema () =
    let code = requireFile "src/Opencode/HookSchema.fs" |> nonCommentCode
    check "arch: HookSchema no local selectMethodologyProperty def"
        (not (code.Contains "let selectMethodologyProperty"))

let opencodeHookSchemaUsesIntentsRawFromArgs () =
    let codec = requireFile "src/Shell/SubagentIntentsCodec.fs" |> nonCommentCode
    check "arch: SubagentIntentsCodec defines intentsRawFromArgs"
        (codec.Contains "let intentsRawFromArgs")
    let code = requireFile "src/Opencode/HookSchema.fs" |> nonCommentCode
    check "arch: HookSchema uses intentsRawFromArgs"
        (code.Contains "intentsRawFromArgs")
    check "arch: HookSchema must not Dyn.get args intents"
        (not (code.Contains "Dyn.get args \"intents\""))

let private forbiddenMuxOpencodeProjectionPatterns =
    [| System.Text.RegularExpressions.Regex(@"captureReport\s+opencode")
       System.Text.RegularExpressions.Regex(@"tryGetReport\s+opencode")
       System.Text.RegularExpressions.Regex(@"storeBacklog\s+opencode") |]

/// Opencode adapter must not depend on Mux modules (shared semantics live in Kernel/Shell).
let opencodeNoMuxRef () =
    for f in fsFiles "src/Opencode" do
        let content = requireFile ("src/Opencode/" + f)
        check ("arch: Opencode/" + f + " no VibeFs.Mux ref") (not (content.Contains "VibeFs.Mux"))

/// Mux adapter must not depend on Opencode modules.
let muxNoOpencodeRef () =
    for f in fsFiles "src/Mux" do
        let content = requireFile ("src/Mux/" + f)
        check ("arch: Mux/" + f + " no VibeFs.Opencode ref") (not (content.Contains "VibeFs.Opencode"))

/// Mux backlog / SessionProjection must not route through the Opencode host key.
let muxBacklogUsesMuxHost () =
    for path in [| "src/Mux/BacklogSession.fs"; "src/Mux/Wrappers.fs" |] do
        let code = requireFile path |> nonCommentCode
        for re in forbiddenMuxOpencodeProjectionPatterns do
            check ("arch: " + path + " avoids opencode SessionProjection host (" + re.ToString() + ")")
                (not (re.IsMatch code))

let opencodeMessageTransformUsesProjectionPolicy () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform uses shouldExcludeAgentFromProjection"
        (code.Contains "shouldExcludeAgentFromProjection")
    check "arch: Opencode MessageTransform no defaultExcludedAgents Set.contains"
        (not (code.Contains "defaultExcludedAgents |> Set.contains"))

let muxMessageTransformUsesProjectionPolicy () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform uses shouldExcludeAgentFromProjection"
        (code.Contains "shouldExcludeAgentFromProjection")
    check "arch: Mux MessageTransform no shouldExcludeMuxAgent"
        (not (code.Contains "shouldExcludeMuxAgent"))

let muxMessageTransformNoLocalCapsBuilder () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform no local buildCapsMessages"
        (not (code.Contains "let private buildCapsMessages"))
    check "arch: Mux MessageTransform delegates CapsCodec"
        (code.Contains "Mux.CapsCodec")

let muxMessageTransformUsesShellCapsCache () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform opens MessageTransformHostHooks"
        (code.Contains "MessageTransformHostHooks")
    check "arch: Mux MessageTransform uses loadCapsForScope"
        (code.Contains "loadCapsForScope")
    check "arch: Mux MessageTransform must not inline getOrLoadCapsFilesForScope"
        (not (code.Contains "getOrLoadCapsFilesForScope"))
    check "arch: Mux MessageTransform no local CapsFileCache"
        (not (code.Contains "module private CapsFileCache"))
    check "arch: Mux MessageTransform no direct findCapsFiles"
        (not (code.Contains "findCapsFiles"))

let muxMessageTransformUsesCommonExtractTexts () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform no local extractTexts"
        (not (code.Contains "let private extractTexts"))
    check "arch: Mux MessageTransform uses Shell extractTextsFromEncodedMessages"
        (code.Contains "extractTextsFromEncodedMessages")

let messageTransformCommonUsesHostMessagePartCodec () =
    let code = requireFile "src/Shell/MessageTransformCommon.fs" |> nonCommentCode
    check "arch: MessageTransformCommon opens HostMessagePartCodec"
        (code.Contains "HostMessagePartCodec")
    check "arch: MessageTransformCommon no Dyn.get msg parts"
        (not (code.Contains "Dyn.get msg \"parts\""))

let readDedupMuxPluginUsesHostMessagePartCodec () =
    let code = requireFile "src/Shell/ReadDedupMuxPlugin.fs" |> nonCommentCode
    check "arch: ReadDedupMuxPlugin opens HostMessagePartCodec"
        (code.Contains "HostMessagePartCodec")
    check "arch: ReadDedupMuxPlugin uses getMessageParts"
        (code.Contains "getMessageParts")
    check "arch: ReadDedupMuxPlugin uses decodeDynamicToolReadOutput"
        (code.Contains "decodeDynamicToolReadOutput")

let messagingPartCodecExists () =
    let code = requireFile "src/Shell/MessagingPartCodec.fs" |> nonCommentCode
    check "arch: MessagingPartCodec defines decodeTextPart" (code.Contains "let decodeTextPart")
    check "arch: MessagingPartCodec defines decodePartsFromArray" (code.Contains "let decodePartsFromArray")
    check "arch: MessagingPartCodec defines operationActionFromInput" (code.Contains "let operationActionFromInput")
    check "arch: MessagingPartCodec defines decodeOpencodeToolStateBox" (code.Contains "let decodeOpencodeToolStateBox")
    check "arch: MessagingPartCodec defines toolOutputAndErrorFromHostOutput" (code.Contains "let toolOutputAndErrorFromHostOutput")
    check "arch: MessagingPartCodec defines muxPartStateToKernelStatus" (code.Contains "let muxPartStateToKernelStatus")
    check "arch: MessagingPartCodec defines decodeMuxDynamicToolState" (code.Contains "let decodeMuxDynamicToolState")

let opencodeMessagingCodecUsesMessagingPartCodec () =
    let code = requireFile "src/Opencode/MessagingCodec.fs" |> nonCommentCode
    check "arch: Opencode MessagingCodec opens MessagingPartCodec" (code.Contains "MessagingPartCodec")
    check "arch: Opencode MessagingCodec uses decodeOpencodeToolStateBox" (code.Contains "decodeOpencodeToolStateBox")
    check "arch: Opencode MessagingCodec uses decodePartsFromArray" (code.Contains "decodePartsFromArray")
    check "arch: Opencode MessagingCodec uses decodeTextPart" (code.Contains "decodeTextPart")
    check "arch: Opencode MessagingCodec no inline operation action from input"
        (not (code.Contains "str operation \"action\""))

let muxMessagingCodecUsesMessagingPartCodec () =
    let code = requireFile "src/Mux/MessagingCodec.fs" |> nonCommentCode
    check "arch: Mux MessagingCodec opens MessagingPartCodec" (code.Contains "MessagingPartCodec")
    check "arch: Mux MessagingCodec uses decodeMuxDynamicToolState" (code.Contains "decodeMuxDynamicToolState")
    check "arch: Mux MessagingCodec uses decodeTextPart" (code.Contains "decodeTextPart")
    check "arch: Mux MessagingCodec uses decodePartsFromArray" (code.Contains "decodePartsFromArray")
    check "arch: Mux MessagingCodec no private decodeToolStatus"
        (not (code.Contains "let private decodeToolStatus"))

let muxMessageTransformUsesMuxWorkspaceCodec () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform opens MuxWorkspaceCodec"
        (code.Contains "MuxWorkspaceCodec")
    check "arch: Mux MessageTransform uses isChildWorkspace from codec"
        (code.Contains "isChildWorkspace")
    check "arch: Mux MessageTransform must not local findWorkspaceEntry"
        (not (code.Contains "let private findWorkspaceEntry"))

let muxMessageTransformUsesReadDedupMuxPlugin () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform opens ReadDedupMuxPlugin"
        (code.Contains "ReadDedupMuxPlugin")
    check "arch: Mux MessageTransform must not open Mux.ReadDedup for plugin dedup"
        (not (code.Contains "Mux.ReadDedup"))

let muxMessageTransformUsesMuxHookInputCodec () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform opens MuxHookInputCodec"
        (code.Contains "MuxHookInputCodec")
    check "arch: Mux MessageTransform uses decodeMuxMessagesTransformInput"
        (code.Contains "decodeMuxMessagesTransformInput")
    check "arch: Mux MessageTransform must not Dyn.str input agent"
        (not (code.Contains "Dyn.str input \"agent\""))
    check "arch: Mux MessageTransform must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Mux MessageTransform must not Dyn.str input directory"
        (not (code.Contains "Dyn.str input \"directory\""))

let muxPluginToolExecuteAfterUsesMuxHookInputCodec () =
    let code = requireFile "src/Mux/Plugin.fs" |> nonCommentCode
    check "arch: Mux Plugin opens MuxHookInputCodec"
        (code.Contains "MuxHookInputCodec")
    check "arch: Mux Plugin toolExecuteAfter uses decodeMuxToolExecuteAfterInput"
        (code.Contains "decodeMuxToolExecuteAfterInput")
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str input tool"
        (not (code.Contains "Dyn.str input \"tool\""))
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str input directory"
        (not (code.Contains "Dyn.str input \"directory\""))
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str input workspaceId"
        (not (code.Contains "Dyn.str input \"workspaceId\""))

let muxWrappersCaptureUsesProjectionNotModuleCapture () =
    let code = requireFile "src/Mux/Wrappers.fs" |> nonCommentCode
    check "arch: Mux Wrappers uses projection.CaptureReport"
        (code.Contains "projection.CaptureReport")
    check "arch: Mux Wrappers must not module captureReport"
        (not (code.Contains "captureReport host"))
    check "arch: Mux createAllWrappers passes RuntimeScope"
        (code.Contains "scope: RuntimeScope")

let opencodeMessageTransformNoLocalCapsBuilder () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform no local buildCapsMessages"
        (not (code.Contains "let private buildCapsMessages"))
    check "arch: Opencode MessageTransform delegates CapsCodec"
        (code.Contains "Opencode.CapsCodec")
    check "arch: Opencode MessageTransform no local CapsFileCache"
        (not (code.Contains "module private CapsFileCache"))

let opencodeMessageTransformUsesShellCapsCache () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform opens MessageTransformHostHooks"
        (code.Contains "MessageTransformHostHooks")
    check "arch: Opencode MessageTransform uses loadCapsForScope"
        (code.Contains "loadCapsForScope")
    check "arch: Opencode MessageTransform must not inline getOrLoadCapsFilesForScope"
        (not (code.Contains "getOrLoadCapsFilesForScope"))
    check "arch: Opencode MessageTransform no local CapsFileCache"
        (not (code.Contains "module private CapsFileCache"))
    check "arch: Opencode MessageTransform no direct findCapsFiles"
        (not (code.Contains "findCapsFiles"))

let noReconstructReviewStateInMessageTransforms () =
    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let content = requireFile path
        check ("arch: " + path + " no reconstructReviewState")
            (not (content.Contains "reconstructReviewState"))

let messageTransformUsesHostEntry () =
    let hostEntry = requireFile "src/Shell/MessageTransformHostEntry.fs" |> nonCommentCode
    check "arch: MessageTransformHostEntry defines ReviewReplayMode"
        (hostEntry.Contains "type ReviewReplayMode")
    check "arch: MessageTransformHostEntry defines runHostMessagesTransform"
        (hostEntry.Contains "let runHostMessagesTransform")
    check "arch: MessageTransformHostEntry defines replayReviewForMode"
        (hostEntry.Contains "let replayReviewForMode")
    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " opens MessageTransformHostEntry")
            (code.Contains "MessageTransformHostEntry")
        check ("arch: " + path + " uses runHostMessagesTransform")
            (code.Contains "runHostMessagesTransform")
        check ("arch: " + path + " forbids replayReviewIfStoreEmpty")
            (not (code.Contains "replayReviewIfStoreEmpty"))
        check ("arch: " + path + " forbids replayReviewAlwaysSync")
            (not (code.Contains "replayReviewAlwaysSync"))
    let opencode = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform uses IfStoreEmpty replay mode"
        (opencode.Contains "IfStoreEmpty")
    let mux = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform uses Always replay mode"
        (mux.Contains "Always")

let capsFileCacheCompositeKey () =
    let code = requireFile "src/Shell/CapsFileCache.fs" |> nonCommentCode
    check "arch: CapsFileCache defines cacheKey"
        (code.Contains "cacheKey")
    check "arch: CapsFileCache cacheKey binds directory"
        (code.Contains "cacheKey sessionID directory")
    check "arch: CapsFileCache loads via RuntimeScope TryGetCapsFiles"
        (code.Contains "TryGetCapsFiles")
    check "arch: CapsFileCache stores via RuntimeScope AddCapsFilesIfAbsent"
        (code.Contains "AddCapsFilesIfAbsent")
    check "arch: CapsFileCache normalizes directory for cache"
        (code.Contains "normalizeDirectory")
    let scopeCode = requireFile "src/Shell/RuntimeScope.fs" |> nonCommentCode
    check "arch: RuntimeScope holds capsFiles map"
        (scopeCode.Contains "capsFiles")

let capsFileCacheNoGetOrLoadCapsFilesDefault () =
    let code = requireFile "src/Shell/CapsFileCache.fs" |> nonCommentCode
    check "arch: CapsFileCache must not define getOrLoadCapsFiles"
        (not (code.Contains "let getOrLoadCapsFiles "))
    check "arch: CapsFileCache must not call getOrLoadCapsFiles("
        (not (code.Contains "getOrLoadCapsFiles("))
    check "arch: CapsFileCache must not use getDefault"
        (not (code.Contains "getDefault"))

let capsFileCacheUsesInflight () =
    let cacheCode = requireFile "src/Shell/CapsFileCache.fs" |> nonCommentCode
    let scopeCode = requireFile "src/Shell/RuntimeScope.fs" |> nonCommentCode
    check "arch: CapsFileCache uses GetOrLoadCapsInflight"
        (cacheCode.Contains "GetOrLoadCapsInflight")
    check "arch: RuntimeScope defines GetOrLoadCapsInflight"
        (scopeCode.Contains "GetOrLoadCapsInflight")
    check "arch: RuntimeScope holds capsInflight map"
        (scopeCode.Contains "capsInflight")

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
    let muxShell = requireFile "src/Shell/MuxSubagentToolExecute.fs" |> nonCommentCode
    let opencode = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    let shellExec = requireFile "src/Shell/SubagentToolExecute.fs" |> nonCommentCode
    for (label, code) in [| "SubagentToolExecute", shellExec; "MuxSubagentToolExecute", muxShell |] do
        check ("arch: " + label + " uses promptsFromCoderIntents")
            (code.Contains "promptsFromCoderIntents")
        check ("arch: " + label + " uses meditatorPromptFromFiles")
            (code.Contains "meditatorPromptFromFiles")
        check ("arch: " + label + " uses browserPromptText")
            (code.Contains "browserPromptText")
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

let subagentToolsUseToolCatalogRequiredKeys () =
    let catalog = requireFile "src/Kernel/ToolCatalog.fs" |> nonCommentCode
    check "arch: ToolCatalog defines subagentRequiredKeys"
        (catalog.Contains "let subagentRequiredKeys")
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    check "arch: Mux SubagentTools uses subagentRequiredKeys for coder"
        (mux.Contains "subagentRequiredKeys \"coder\"")
    check "arch: Mux SubagentTools uses subagentRequiredKeys for investigator"
        (mux.Contains "subagentRequiredKeys \"investigator\"")
    check "arch: Mux SubagentTools uses subagentRequiredKeys for meditator"
        (mux.Contains "subagentRequiredKeys \"meditator\"")
    check "arch: Mux SubagentTools uses subagentRequiredKeys for browser"
        (mux.Contains "subagentRequiredKeys \"browser\"")
    check "arch: Mux SubagentTools must not hardcode [| intents; tdd |]"
        (not (mux.Contains "[| \"intents\"; \"tdd\" |]"))
    check "arch: Mux SubagentTools must not hardcode [| intents |] required array"
        (not (mux.Contains "[| \"intents\" |]"))
    check "arch: Mux SubagentTools must not hardcode [| intent; files |]"
        (not (mux.Contains "[| \"intent\"; \"files\" |]"))
    check "arch: Mux SubagentTools must not hardcode [| intent |] required array"
        (not (mux.Contains "[| \"intent\" |]"))
    let opencode = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    check "arch: Opencode SubagentTools uses subagentRequiredKeys for coder"
        (opencode.Contains "subagentRequiredKeys \"coder\"")
    check "arch: Opencode SubagentTools uses subagentRequiredKeys for investigator"
        (opencode.Contains "subagentRequiredKeys \"investigator\"")
    check "arch: Opencode SubagentTools uses subagentRequiredKeys for meditator"
        (opencode.Contains "subagentRequiredKeys \"meditator\"")
    check "arch: Opencode SubagentTools uses subagentRequiredKeys for browser"
        (opencode.Contains "subagentRequiredKeys \"browser\"")
    check "arch: Opencode SubagentTools uses subagentZodShape"
        (opencode.Contains "subagentZodShape")
    check "arch: Opencode SubagentTools must not hardcode [| intents; tdd |]"
        (not (opencode.Contains "[| \"intents\"; \"tdd\" |]"))
    check "arch: Opencode SubagentTools must not hardcode [| intents |] required array"
        (not (opencode.Contains "[| \"intents\" |]"))
    check "arch: Opencode SubagentTools must not hardcode [| intent; files |]"
        (not (opencode.Contains "[| \"intent\"; \"files\" |]"))
    check "arch: Opencode SubagentTools must not hardcode [| intent |] required array"
        (not (opencode.Contains "[| \"intent\" |]"))
    let toolSchema = requireFile "src/Opencode/ToolSchema.fs" |> nonCommentCode
    check "arch: Opencode ToolSchema defines subagentZodShape"
        (toolSchema.Contains "let subagentZodShape")

let kernelToolArgsExists () =
    let code = requireFile "src/Kernel/ToolArgs.fs" |> nonCommentCode
    check "arch: Kernel ToolArgs defines ToolArgs DU"
        (code.Contains "type ToolArgs =")
    check "arch: Kernel ToolArgs must not define CoderIntents"
        (not (code.Contains "CoderIntents"))
    check "arch: Kernel ToolArgs must not define InvestigatorIntents"
        (not (code.Contains "InvestigatorIntents"))

let toolExecuteWireHelperExists () =
    let code = requireFile "src/Shell/ToolExecute.fs" |> nonCommentCode
    check "arch: ToolExecute defines wireDecodeFailure"
        (code.Contains "let wireDecodeFailure")
    check "arch: ToolExecute wireDecodeFailure uses wireEncodeToolError"
        (code.Contains "wireEncodeToolError")

let toolArgsDecodeExists () =
    let code = requireFile "src/Shell/ToolArgsDecode.fs" |> nonCommentCode
    check "arch: ToolArgsDecode defines decodeToolArgs"
        (code.Contains "let decodeToolArgs")
    check "arch: ToolArgsDecode defines decodeToolInvocation"
        (code.Contains "let decodeToolInvocation")
    check "arch: ToolArgsDecode defines DecodedToolInvocation"
        (code.Contains "type DecodedToolInvocation =")
    check "arch: DecodedToolInvocation defines CoderBatch"
        (code.Contains "CoderBatch")
    check "arch: DecodedToolInvocation defines InvestigatorBatch"
        (code.Contains "InvestigatorBatch")

let toolArgsDecodeCoversMajorTools () =
    let code = requireFile "src/Shell/ToolArgsDecode.fs" |> nonCommentCode
    check "arch: ToolArgsDecode mentions websearch"
        (code.Contains "websearch")
    check "arch: ToolArgsDecode mentions webfetch"
        (code.Contains "webfetch")
    check "arch: ToolArgsDecode mentions executor"
        (code.Contains "executor")
    check "arch: ToolArgsDecode uses decodeWebsearchArgs"
        (code.Contains "decodeWebsearchArgs")
    check "arch: ToolArgsDecode uses decodeWebfetchArgs"
        (code.Contains "decodeWebfetchArgs")
    check "arch: ToolArgsDecode uses decodeExecutorArgs"
        (code.Contains "decodeExecutorArgs")
    check "arch: ToolArgsDecode mentions todowrite"
        (code.Contains "todowrite")
    check "arch: ToolArgsDecode mentions knowledge_graph_fetch"
        (code.Contains "knowledge_graph_fetch")
    check "arch: ToolArgsDecode mentions return_bookkeeper"
        (code.Contains "return_bookkeeper")
    check "arch: ToolArgsDecode mentions apply_patch"
        (code.Contains "apply_patch")
    check "arch: ToolArgsDecode mentions submit_review"
        (code.Contains "submit_review")
    check "arch: ToolArgsDecode uses decodeTodoWriteArgs"
        (code.Contains "decodeTodoWriteArgs")
    check "arch: ToolArgsDecode uses decodeFetchEntity"
        (code.Contains "decodeFetchEntity")
    check "arch: ToolArgsDecode uses decodeReturnBookkeeperArgs"
        (code.Contains "decodeReturnBookkeeperArgs")
    check "arch: ToolArgsDecode uses decodeApplyPatchFields"
        (code.Contains "decodeApplyPatchFields")
    check "arch: ToolArgsDecode uses decodeSubmitReviewArgs"
        (code.Contains "decodeSubmitReviewArgs")

let decodedToolInvocationNoObj () =
    let code = requireFile "src/Shell/ToolArgsDecode.fs" |> nonCommentCode
    check "arch: DecodedToolInvocation must not carry intents obj"
        (not (code.Contains "intents: obj"))
    check "arch: DecodedToolInvocation must not define SubagentIntents case"
        (not (code.Contains "SubagentIntents of"))

let muxSubagentToolsUsesToolArgsDecode () =
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let shell = requireFile "src/Shell/MuxSubagentToolExecute.fs" |> nonCommentCode
    check "arch: Mux SubagentTools opens MuxSubagentToolExecute"
        (mux.Contains "MuxSubagentToolExecute")
    check "arch: Mux SubagentTools uses executeMuxSubagentTool"
        (mux.Contains "executeMuxSubagentTool")
    check "arch: MuxSubagentToolExecute uses decodeToolInvocation"
        (shell.Contains "decodeToolInvocation")
    check "arch: MuxSubagentToolExecute uses wireDecodeFailure on decode errors"
        (shell.Contains "wireDecodeFailure")
    check "arch: Mux SubagentTools must not parallelPromptsFromIntents"
        (not (mux.Contains "parallelPromptsFromIntents"))
    check "arch: Mux SubagentTools must not buildPromptsFor"
        (not (mux.Contains "buildPromptsFor"))

let opencodeSubagentToolsUsesToolArgsDecode () =
    let code = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    let shell = requireFile "src/Shell/SubagentToolExecute.fs" |> nonCommentCode
    check "arch: Opencode SubagentTools opens SubagentToolExecute"
        (code.Contains "SubagentToolExecute")
    check "arch: Opencode SubagentTools uses executeOpencodeSubagentTool"
        (code.Contains "executeOpencodeSubagentTool")
    check "arch: Opencode SubagentTools must not decodeIntentsField"
        (not (code.Contains "decodeIntentsField"))
    check "arch: Opencode SubagentTools must not decodeMeditatorArgs"
        (not (code.Contains "decodeMeditatorArgs"))
    check "arch: SubagentToolExecute uses decodeToolInvocation"
        (shell.Contains "decodeToolInvocation")
    check "arch: SubagentToolExecute uses wireDecodeFailure on decode errors"
        (shell.Contains "wireDecodeFailure")
    check "arch: SubagentToolExecute must not use decodeToolArgs"
        (not (shell.Contains "decodeToolArgs"))

let sessionIoRunSubagentReturnsResult () =
    let sessionIo = requireFile "src/Opencode/SessionIo.fs" |> nonCommentCode
    check "arch: SessionIo runSubagent returns Promise Result"
        (sessionIo.Contains "let runSubagent" && sessionIo.Contains "JS.Promise<Result<string, DomainError>>")
    check "arch: SessionIo runSubagentWithCleanup returns Promise Result"
        (sessionIo.Contains "let runSubagentWithCleanup")
    check "arch: SessionIo must not define private runSubagentCore string wrapper"
        (not (sessionIo.Contains "let private runSubagentCore"))
    check "arch: runSubagentCoreResult outer catch returns Error not reject"
        (sessionIo.Contains "return Error (translateJsError err)")
    check "arch: runSubagentCoreResult inner catch returns Error domain"
        (sessionIo.Contains "return Error other")

let commandHooksUsesToolCopyReviewMessages () =
    let code = requireFile "src/Opencode/CommandHooks.fs" |> nonCommentCode
    let copy = requireFile "src/Kernel/ToolCopy.fs" |> nonCommentCode
    check "arch: CommandHooks opens ToolCopy"
        (code.Contains "ToolCopy")
    check "arch: CommandHooks uses reviewAlreadyActiveMessage"
        (code.Contains "reviewAlreadyActiveMessage")
    check "arch: ToolCopy defines preReviewCouldNotComplete"
        (copy.Contains "let preReviewCouldNotComplete")
    check "arch: CommandHooks must not inline With-Review Mode is already active"
        (not (code.Contains "With-Review Mode is already active. Submit your work via submit_review."))

let subagentToolsUseSubagentSpawn () =
    let spawn = requireFile "src/Shell/SubagentSpawn.fs" |> nonCommentCode
    check "arch: SubagentSpawn defines runParallelSpawns"
        (spawn.Contains "let runParallelSpawns")
    check "arch: SubagentSpawn defines runParallelSpawnsWithAbort"
        (spawn.Contains "let runParallelSpawnsWithAbort")
    let opencode = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    let shellExec = requireFile "src/Shell/SubagentToolExecute.fs" |> nonCommentCode
    check "arch: Opencode SubagentTools uses executeOpencodeSubagentTool"
        (opencode.Contains "executeOpencodeSubagentTool")
    check "arch: SubagentToolExecute uses runParallelSpawns"
        (shellExec.Contains "runParallelSpawns")
    check "arch: Opencode SubagentTools must not inline parallel Promise.all joinReports"
        (not (opencode.Contains "|> Promise.all"))
    check "arch: Opencode SubagentTools must not call joinReports for parallel coder/investigator"
        (not (opencode.Contains "joinReports"))
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let muxShell = requireFile "src/Shell/MuxSubagentToolExecute.fs" |> nonCommentCode
    check "arch: MuxSubagentToolExecute uses runParallelSpawnsWithAbort"
        (muxShell.Contains "runParallelSpawnsWithAbort")
    check "arch: Mux SubagentTools must not inline AbortController parallel spawn"
        (not (mux.Contains "AbortController"))
    check "arch: Mux SubagentTools must not inline parallel Promise.all joinReports"
        (not (mux.Contains "|> Promise.all"))
    check "arch: Mux SubagentTools must not call joinReports in bindParallel"
        (not (mux.Contains "joinReports"))
    check "arch: MuxSubagentToolExecute must not inline AbortController parallel spawn"
        (not (muxShell.Contains "AbortController"))

let muxSubagentToolsUsesMuxJsonSchema () =
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let schema = requireFile "src/Shell/MuxJsonSchema.fs" |> nonCommentCode
    check "arch: Mux SubagentTools opens MuxJsonSchema"
        (mux.Contains "MuxJsonSchema")
    check "arch: MuxJsonSchema defines muxCoderIntentsSchema"
        (schema.Contains "let muxCoderIntentsSchema")
    check "arch: Mux SubagentTools must not define private muxCoderIntentsSchema"
        (not (mux.Contains "let private muxCoderIntentsSchema"))

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

let opencodeSubagentToolsUsesFromOpencode () =
    let opencode = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    let shellExec = requireFile "src/Shell/SubagentToolExecute.fs" |> nonCommentCode
    check "arch: SubagentToolExecute opens ToolRuntimeContext"
        (shellExec.Contains "ToolRuntimeContext")
    check "arch: SubagentToolExecute uses fromOpencode"
        (shellExec.Contains "fromOpencode")
    check "arch: SubagentToolExecute uses runtime.Execution for directory and session"
        ((shellExec.Contains "runtime.Execution.Directory")
         && (shellExec.Contains "runtime.Execution.SessionId"))
    check "arch: Opencode SubagentTools must not decodeOpencodeToolContext"
        (not (opencode.Contains "decodeOpencodeToolContext"))
    check "arch: Opencode SubagentTools must not define private ToolExecutionContext"
        (not (opencode.Contains "type private ToolExecutionContext"))
    check "arch: SubagentToolExecute uses pluginDirectoryFromCtx"
        (shellExec.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode SubagentTools must not Dyn.str ctx directory"
        (not (opencode.Contains "Dyn.str ctx \"directory\""))

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

let opencodeMessageTransformNoLocalApplyReadDedup () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform no local applyReadDedup"
        (not (code.Contains "let private applyReadDedup"))
    check "arch: Opencode MessageTransform uses ReadDedupOpenCode"
        (code.Contains "ReadDedupOpenCode")
    check "arch: Opencode MessageTransform calls deduplicateOpencodeReadPartsInPlace"
        (code.Contains "deduplicateOpencodeReadPartsInPlace")

let webToolsUsesWebToolsCodec () =
    let web = requireFile "src/Mux/WebTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/WebToolsCodec.fs" |> nonCommentCode
    check "arch: Mux WebTools opens WebToolsCodec"
        (web.Contains "WebToolsCodec")
    check "arch: WebToolsCodec defines decodeWebsearchArgs"
        (codec.Contains "let decodeWebsearchArgs")
    check "arch: Mux WebTools uses decodeWebsearchArgs"
        (web.Contains "decodeWebsearchArgs")
    check "arch: Mux WebTools websearch must not inline strField args query"
        (not (web.Contains "strField args \"query\""))

let messageTransformUsesChatTransformOutputCodec () =
    let codec = requireFile "src/Shell/ChatTransformOutputCodec.fs" |> nonCommentCode
    check "arch: ChatTransformOutputCodec defines tryGetMessagesArrayFromOutput"
        (codec.Contains "let tryGetMessagesArrayFromOutput")
    check "arch: ChatTransformOutputCodec defines clearSystemOutputLength"
        (codec.Contains "let clearSystemOutputLength")
    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " opens ChatTransformOutputCodec")
            (code.Contains "ChatTransformOutputCodec")
        check ("arch: " + path + " uses tryGetMessagesArrayFromOutput")
            (code.Contains "tryGetMessagesArrayFromOutput")
        check ("arch: " + path + " must not Dyn.get output messages")
            (not (code.Contains "Dyn.get output \"messages\""))
    let opencode = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform uses clearSystemOutputLength"
        (opencode.Contains "clearSystemOutputLength")

let messageTransformUsesMessageTransformCore () =
    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " opens MessageTransformCore")
            (code.Contains "MessageTransformCore")
        check ("arch: " + path + " no direct projectBacklogFor")
            (not (code.Contains "projectBacklogFor"))
    let opencode = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform uses applyBacklogProjection in compacting path"
        (opencode.Contains "applyBacklogProjection")
    let mux = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform must not call applyBacklogProjection directly"
        (not (mux.Contains "applyBacklogProjection"))
    let core = requireFile "src/Shell/MessageTransformCore.fs" |> nonCommentCode
    check "arch: MessageTransformCore defines applyBacklogProjection"
        (core.Contains "let applyBacklogProjection")

let messageTransformUsesPipeline () =
    let pipeline = requireFile "src/Shell/MessageTransformPipeline.fs" |> nonCommentCode
    check "arch: MessageTransformPipeline defines runMessageTransformPipeline"
        (pipeline.Contains "let runMessageTransformPipeline")
    check "arch: MessageTransformPipeline defines MessageTransformPlan"
        (pipeline.Contains "type MessageTransformPlan")
    let hostEntry = requireFile "src/Shell/MessageTransformHostEntry.fs" |> nonCommentCode
    check "arch: MessageTransformHostEntry uses runMessageTransformPipeline"
        (hostEntry.Contains "runMessageTransformPipeline")
    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " must not call runMessageTransformPipeline directly")
            (not (code.Contains "runMessageTransformPipeline"))

let messageTransformUsesCapsKgHostHooks () =
    let hooks = requireFile "src/Shell/MessageTransformHostHooks.fs" |> nonCommentCode
    check "arch: MessageTransformHostHooks defines loadCapsForScope"
        (hooks.Contains "let loadCapsForScope")
    check "arch: MessageTransformHostHooks defines loadKgPreludeForAgent"
        (hooks.Contains "let loadKgPreludeForAgent")
    check "arch: MessageTransformHostHooks defines CapsLoadPolicy"
        (hooks.Contains "type CapsLoadPolicy")
    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " opens MessageTransformHostHooks")
            (code.Contains "MessageTransformHostHooks")
        check ("arch: " + path + " uses loadCapsForScope")
            (code.Contains "loadCapsForScope")
        check ("arch: " + path + " uses loadKgPreludeForAgent")
            (code.Contains "loadKgPreludeForAgent")
        check ("arch: " + path + " must not inline getOrLoadCapsFilesForScope")
            (not (code.Contains "getOrLoadCapsFilesForScope"))

let dualHostMessagingCodecUsesEncodeHelpers () =
    let helpers = requireFile "src/Shell/MessagingEncodeHelpers.fs" |> nonCommentCode
    check "arch: MessagingEncodeHelpers defines replacePartsOnRawMessage"
        (helpers.Contains "let replacePartsOnRawMessage")
    for path in [| "src/Opencode/MessagingCodec.fs"; "src/Mux/MessagingCodec.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " opens MessagingEncodeHelpers")
            (code.Contains "MessagingEncodeHelpers")
        check ("arch: " + path + " uses replacePartsOnRawMessage")
            (code.Contains "replacePartsOnRawMessage")
        check ("arch: " + path + " must not inline Dyn.withKey rawMsg parts")
            (not (code.Contains "Dyn.withKey rawMsg \"parts\""))

let messagingWireForkDocumented () =
    let docPath = "MESSAGING_WIRE.md"
    check "arch: MESSAGING_WIRE.md exists" (existsSync docPath)
    let doc = requireFile docPath
    check "arch: MESSAGING_WIRE.md mentions info envelope"
        (doc.Contains "info")
    check "arch: MESSAGING_WIRE.md mentions dynamic-tool"
        (doc.Contains "dynamic-tool")
    check "arch: MESSAGING_WIRE.md mentions MessagingEncodeHelpers"
        (doc.Contains "MessagingEncodeHelpers")
    check "arch: MESSAGING_WIRE.md mentions dualHostMessagingCodecUsesEncodeHelpers"
        (doc.Contains "dualHostMessagingCodecUsesEncodeHelpers")

let hostObjBoundaryDocumented () =
    let docPath = "HOST_OBJ_BOUNDARY.md"
    check "arch: HOST_OBJ_BOUNDARY.md exists" (existsSync docPath)
    let doc = requireFile docPath
    check "arch: HOST_OBJ_BOUNDARY.md mentions ToolArgsDecode"
        (doc.Contains "ToolArgsDecode")
    check "arch: HOST_OBJ_BOUNDARY.md mentions MESSAGING_WIRE.md"
        (doc.Contains "MESSAGING_WIRE.md")
    check "arch: HOST_OBJ_BOUNDARY.md mentions mergeConfigObj"
        (doc.Contains "mergeConfigObj")

let opencodeSubagentToolsUsesOpencodeClientCodec () =
    let code = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    check "arch: Opencode SubagentTools opens OpencodeClientCodec"
        (code.Contains "OpencodeClientCodec")
    check "arch: Opencode SubagentTools uses getClientFromPluginCtx"
        (code.Contains "getClientFromPluginCtx")
    check "arch: Opencode SubagentTools must not Dyn.get ctx client"
        (not (code.Contains "Dyn.get ctx \"client\""))

let sessionIoUsesOpencodeClientCodec () =
    let code = requireFile "src/Opencode/SessionIo.fs" |> nonCommentCode
    check "arch: SessionIo opens OpencodeClientCodec"
        (code.Contains "OpencodeClientCodec")
    check "arch: SessionIo uses getSessionApiFromClient"
        (code.Contains "getSessionApiFromClient")
    check "arch: SessionIo must not Dyn.get client session"
        (not (code.Contains "Dyn.get client \"session\""))

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

let messageTransformUsesBacklogSessionOpsFrom () =
    let core = requireFile "src/Shell/MessageTransformCore.fs" |> nonCommentCode
    check "arch: MessageTransformCore defines backlogSessionOpsFrom"
        (core.Contains "let backlogSessionOpsFrom")
    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " uses backlogSessionOpsFrom")
            (code.Contains "backlogSessionOpsFrom")
        check ("arch: " + path + " must not inline BacklogSessionOps record for backlog")
            (not (code.Contains "GetOrRebuildBacklog = fun sid msgs"))

let knowledgeGraphRuntimeUsesWorkflow () =
    let opencode = requireFile "src/Opencode/KnowledgeGraphRuntime.fs" |> nonCommentCode
    check "arch: Opencode KnowledgeGraphRuntime opens KnowledgeGraphWorkflow"
        (opencode.Contains "KnowledgeGraphWorkflow")
    check "arch: Opencode KnowledgeGraphRuntime uses trackBackgroundJob"
        (opencode.Contains "trackBackgroundJob")
    check "arch: Opencode KnowledgeGraphRuntime uses recordLaunchResult"
        (opencode.Contains "recordLaunchResult")
    check "arch: Opencode KnowledgeGraphRuntime no local ResizeArray backgroundJobs"
        (not (opencode.Contains "let backgroundJobs = ResizeArray"))
    let mux = requireFile "src/Mux/KnowledgeGraphTools.fs" |> nonCommentCode
    check "arch: Mux KnowledgeGraphTools opens KnowledgeGraphWorkflow"
        (mux.Contains "KnowledgeGraphWorkflow")
    check "arch: Mux KnowledgeGraphTools uses trackBackgroundJob"
        (mux.Contains "trackBackgroundJob")
    check "arch: Mux KnowledgeGraphTools uses recordLaunchResult"
        (mux.Contains "recordLaunchResult")
    check "arch: Mux KnowledgeGraphTools no local ResizeArray backgroundJobs"
        (not (mux.Contains "let backgroundJobs = ResizeArray"))

let knowledgeGraphBookkeeperLaunchInShell () =
    let launch = requireFile "src/Shell/KnowledgeGraphBookkeeperLaunch.fs" |> nonCommentCode
    check "arch: Shell KnowledgeGraphBookkeeperLaunch defines queueBackgroundLaunch"
        (launch.Contains "let queueBackgroundLaunch")
    check "arch: Shell KnowledgeGraphBookkeeperLaunch defines launchBackgroundSession"
        (launch.Contains "let launchBackgroundSession")
    check "arch: Shell KnowledgeGraphBookkeeperLaunch defines queueMuxBackgroundLaunch"
        (launch.Contains "let queueMuxBackgroundLaunch")
    let opencodeIo = requireFile "src/Opencode/KnowledgeGraphRuntimeIO.fs" |> nonCommentCode
    check "arch: Opencode KnowledgeGraphRuntimeIO no queueBackgroundLaunch"
        (not (opencodeIo.Contains "let queueBackgroundLaunch"))
    let opencode = requireFile "src/Opencode/KnowledgeGraphRuntime.fs" |> nonCommentCode
    check "arch: Opencode KnowledgeGraphRuntime opens KnowledgeGraphBookkeeperLaunch"
        (opencode.Contains "KnowledgeGraphBookkeeperLaunch")
    check "arch: Opencode KnowledgeGraphRuntime calls queueBackgroundLaunch"
        (opencode.Contains "queueBackgroundLaunch")
    let mux = requireFile "src/Mux/KnowledgeGraphTools.fs" |> nonCommentCode
    check "arch: Mux KnowledgeGraphTools calls queueMuxBackgroundLaunch"
        (mux.Contains "queueMuxBackgroundLaunch")
    check "arch: Mux KnowledgeGraphTools no inline delegate bookkeeper trackBackgroundJob block"
        (not (mux.Contains "delegateToSubAgent deps cfg \"bookkeeper\""))
    for path in [| "src/Opencode/KnowledgeGraphRuntime.fs"; "src/Mux/KnowledgeGraphTools.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " no TestObservation type")
            (not (code.Contains "TestObservation"))
        check ("arch: " + path + " no member Observation")
            (not (code.Contains "member _.Observation"))
        check ("arch: " + path + " exposes CreateTestPorts")
            (code.Contains "CreateTestPorts")

let knowledgeGraphRuntimeNoLocalLaunchIfDue () =
    for path in [| "src/Opencode/KnowledgeGraphRuntime.fs"; "src/Mux/KnowledgeGraphTools.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " uses runMaintenanceIfDue")
            (code.Contains "runMaintenanceIfDue")
        check ("arch: " + path + " no local launchIfDue")
            (not (code.Contains "let launchIfDue"))

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

let muxHostToolsFuzzyUsesToolCopy () =
    let code = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    check "arch: Mux HostTools fuzzy must not use muxFuzzyFindRequiresWorkspaceId"
        (not (code.Contains "muxFuzzyFindRequiresWorkspaceId"))
    check "arch: Mux HostTools fuzzy must not use muxFuzzyGrepRequiresWorkspaceId"
        (not (code.Contains "muxFuzzyGrepRequiresWorkspaceId"))
    check "arch: Mux HostTools must not inline fuzzy_find requires workspaceId"
        (not (code.Contains "fuzzy_find requires workspaceId"))
    check "arch: Mux HostTools must not inline fuzzy_grep requires workspaceId"
        (not (code.Contains "fuzzy_grep requires workspaceId"))

let muxHostToolsFuzzyUsesFromMuxConfig () =
    let code = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    check "arch: Mux HostTools opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Mux HostTools fuzzy uses fromMuxConfig"
        (code.Contains "fromMuxConfig")
    check "arch: Mux HostTools fuzzy uses wireEncodeToolError MuxConfig on config decode"
        (code.Contains "wireEncodeToolError \"MuxConfig\"")
    check "arch: Mux HostTools fuzzy SearchOptions uses Execution.Directory"
        (code.Contains "runtime.Execution.Directory")
    check "arch: Mux HostTools fuzzy SearchOptions uses Execution.WorkspaceId"
        (code.Contains "runtime.Execution.WorkspaceId")
    check "arch: Mux HostTools fuzzy must not Dyn.str config workspaceId in execute"
        (not (code.Contains "Dyn.str config \"workspaceId\""))

let muxHostToolsFuzzyUsesFuzzyToolsCodec () =
    let code = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    check "arch: Mux HostTools opens FuzzyToolsCodec" (code.Contains "FuzzyToolsCodec")
    check "arch: Mux HostTools fuzzy_find uses decodeFuzzyFindArgs" (code.Contains "decodeFuzzyFindArgs")
    check "arch: Mux HostTools fuzzy_grep uses decodeFuzzyGrepArgs" (code.Contains "decodeFuzzyGrepArgs")
    check "arch: Mux HostTools fuzzy must not strField args pattern" (not (code.Contains "strField args \"pattern\""))
    check "arch: Mux HostTools fuzzy must not parseExcludeField args inline" (not (code.Contains "parseExcludeField args"))

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

let fuzzyToolsCodecExists () =
    let codec = requireFile "src/Shell/FuzzyToolsCodec.fs" |> nonCommentCode
    check "arch: FuzzyToolsCodec uses DynField strField" (codec.Contains "strField args")
    check "arch: FuzzyToolsCodec must not define local let strField" (not (codec.Contains "let strField"))
    check "arch: FuzzyToolsCodec returns FuzzyFindParams" (codec.Contains "FuzzyFindParams")
    check "arch: FuzzyToolsCodec returns FuzzyGrepParams" (codec.Contains "FuzzyGrepParams")

let webToolsUsesWebfetchCodec () =
    let web = requireFile "src/Mux/WebTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/WebToolsCodec.fs" |> nonCommentCode
    check "arch: WebToolsCodec defines decodeWebfetchArgs"
        (codec.Contains "let decodeWebfetchArgs")
    check "arch: Mux WebTools uses decodeWebfetchArgs"
        (web.Contains "decodeWebfetchArgs")
    check "arch: Mux WebTools webfetch must not inline strField args url"
        (not (web.Contains "strField args \"url\""))

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

let opencodePluginCoreUsesFromOpencode () =
    let code = requireFile "src/Opencode/PluginCore.fs" |> nonCommentCode
    check "arch: Opencode PluginCore opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Opencode PluginCore createCoreServices uses pluginDirectoryFromCtx"
        (code.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode PluginCore must not Dyn.str ctx directory"
        (not (code.Contains "Dyn.str ctx \"directory\""))
    check "arch: Opencode PluginCore must not fromOpencode ctx empty for directory"
        (not (code.Contains "(fromOpencode ctx \"\")"))

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

let dualHostFuzzyUsesFuzzyToolsCodec () =
    let codec = requireFile "src/Shell/FuzzyToolsCodec.fs" |> nonCommentCode
    let mux = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    let opencode = requireFile "src/Opencode/SearchTools.fs" |> nonCommentCode
    check "arch: FuzzyToolsCodec defines decodeFuzzyFindArgs"
        (codec.Contains "let decodeFuzzyFindArgs")
    check "arch: FuzzyToolsCodec defines decodeFuzzyGrepArgs"
        (codec.Contains "let decodeFuzzyGrepArgs")
    check "arch: Mux HostTools opens FuzzyToolsCodec"
        (mux.Contains "FuzzyToolsCodec")
    check "arch: Mux HostTools fuzzy_find uses decodeFuzzyFindArgs"
        (mux.Contains "decodeFuzzyFindArgs")
    check "arch: Mux HostTools fuzzy_grep uses decodeFuzzyGrepArgs"
        (mux.Contains "decodeFuzzyGrepArgs")
    check "arch: Mux HostTools fuzzy must not strField args pattern"
        (not (mux.Contains "strField args \"pattern\""))
    check "arch: Opencode SearchTools opens FuzzyToolsCodec"
        (opencode.Contains "FuzzyToolsCodec")
    check "arch: Opencode SearchTools uses decodeFuzzyFindArgs"
        (opencode.Contains "decodeFuzzyFindArgs")
    check "arch: Opencode SearchTools uses decodeFuzzyGrepArgs"
        (opencode.Contains "decodeFuzzyGrepArgs")
    check "arch: Opencode SearchTools fuzzy must not inline optStr args pattern"
        (not (opencode.Contains "optStr args \"pattern\""))
    check "arch: Opencode SearchTools fuzzy must not parseExcludeField args in tool body"
        (not (opencode.Contains "parseExcludeField args"))
    check "arch: Mux HostTools fuzzy_find decode uses wireDecodeFailure"
        (mux.Contains "wireDecodeFailure \"fuzzy_find\"")
    check "arch: Mux HostTools fuzzy_grep decode uses wireDecodeFailure"
        (mux.Contains "wireDecodeFailure \"fuzzy_grep\"")

let executeMuxSubagentToolUsesSpawnRoleOnly () =
    let shell = requireFile "src/Shell/MuxSubagentToolExecute.fs" |> nonCommentCode
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    check "arch: executeMuxSubagentTool uses spawn.Role for tool name"
        (shell.Contains "let toolName = spawn.Role")
    check "arch: executeMuxSubagentTool signature must not take toolName parameter"
        (not (System.Text.RegularExpressions.Regex(@"let\s+executeMuxSubagentTool[\s\S]{0,400}\(toolName:\s*string\)").IsMatch shell))
    check "arch: Mux SubagentTools calls executeMuxSubagentTool without toolName arg"
        (mux.Contains "executeMuxSubagentTool runMuxSubagent deps (spawnFor")
    check "arch: Mux SubagentTools must not pass role as extra arg after args"
        (not (mux.Contains "executeMuxSubagentTool runMuxSubagent deps (spawnFor deps toolNames agentId title aiSettingsAgentId role) role args config"))

let subagentToolExecuteEmptyBatchGuard () =
    let opencode = requireFile "src/Shell/SubagentToolExecute.fs" |> nonCommentCode
    let mux = requireFile "src/Shell/MuxSubagentToolExecute.fs" |> nonCommentCode
    check "arch: SubagentToolExecute uses subagentIntentsMustBeNonEmpty"
        (opencode.Contains "subagentIntentsMustBeNonEmpty")
    check "arch: SubagentToolExecute guards CoderBatch with prompts.IsEmpty"
        ((opencode.Contains "CoderBatch intents") && (opencode.Contains "prompts.IsEmpty"))
    check "arch: MuxSubagentToolExecute uses subagentIntentsMustBeNonEmpty"
        (mux.Contains "subagentIntentsMustBeNonEmpty")

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

let opencodeToolsUseWireEncodeForClient () =
    let executor = requireFile "src/Opencode/ExecutorTool.fs" |> nonCommentCode
    let subagent = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    let review = requireFile "src/Opencode/ReviewTools.fs" |> nonCommentCode
    let search = requireFile "src/Opencode/SearchTools.fs" |> nonCommentCode
    let assertClientWire (name: string) (code: string) =
        check (sprintf "arch: Opencode %s opens ToolResult" name) (code.Contains "ToolResult")
        check (sprintf "arch: Opencode %s uses wireEncodeToolError OpencodeClient" name)
            (code.Contains "wireEncodeToolError \"OpencodeClient\"")
        check (sprintf "arch: Opencode %s must not formatDomainError on getClientFromPluginCtx" name)
            (not (code.Contains "formatDomainError"))
    assertClientWire "ExecutorTool" executor
    assertClientWire "SubagentTools" subagent
    assertClientWire "ReviewTools" review
    assertClientWire "SearchTools" search

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

let opencodeSubagentToolsUsesSimpleArgsCodec () =
    let code = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    let decode = requireFile "src/Shell/ToolArgsDecode.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/SubagentSimpleArgsCodec.fs" |> nonCommentCode
    check "arch: SubagentSimpleArgsCodec defines decodeMeditatorArgs"
        (codec.Contains "let decodeMeditatorArgs")
    check "arch: SubagentSimpleArgsCodec defines decodeBrowserArgs"
        (codec.Contains "let decodeBrowserArgs")
    check "arch: ToolArgsDecode uses decodeMeditatorArgs"
        (decode.Contains "decodeMeditatorArgs")
    check "arch: ToolArgsDecode uses decodeBrowserArgs"
        (decode.Contains "decodeBrowserArgs")
    check "arch: Opencode SubagentTools must not open SubagentSimpleArgsCodec"
        (not (code.Contains "SubagentSimpleArgsCodec"))
    check "arch: Opencode SubagentTools must not decodeMeditatorArgs"
        (not (code.Contains "decodeMeditatorArgs"))
    check "arch: Opencode SubagentTools meditator must not Dyn.str args intent"
        (not (code.Contains "Dyn.str args \"intent\""))

let muxSubagentToolsUsesSimpleArgsCodec () =
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let shell = requireFile "src/Shell/MuxSubagentToolExecute.fs" |> nonCommentCode
    check "arch: Mux SubagentTools must not open SubagentSimpleArgsCodec"
        (not (mux.Contains "SubagentSimpleArgsCodec"))
    check "arch: MuxSubagentToolExecute must not decodeMeditatorArgs"
        (not (shell.Contains "decodeMeditatorArgs"))
    check "arch: MuxSubagentToolExecute uses decodeToolInvocation"
        (shell.Contains "decodeToolInvocation")

let opencodeHookExecuteUsesFromOpencode () =
    let code = requireFile "src/Opencode/HookExecute.fs" |> nonCommentCode
    check "arch: Opencode HookExecute opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Opencode HookExecute opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode HookExecute uses fromOpencode for bookkeeper session"
        (code.Contains "fromOpencode input pluginDirectory")
    check "arch: Opencode HookExecute uses Execution.SessionId"
        (code.Contains "Execution.SessionId")
    check "arch: Opencode HookExecute uses toolNameFromHookInput"
        (code.Contains "toolNameFromHookInput")
    check "arch: Opencode HookExecute uses argsFromHookInput"
        (code.Contains "argsFromHookInput")
    check "arch: Opencode HookExecute uses executorModeFromHookInput"
        (code.Contains "executorModeFromHookInput")
    check "arch: Opencode HookExecute uses hookOutputError and hookOutputText"
        ((code.Contains "hookOutputError") && (code.Contains "hookOutputText"))
    check "arch: Opencode HookExecute must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Opencode HookExecute must not Dyn.str input tool"
        (not (code.Contains "Dyn.str input \"tool\""))

let opencodeChatHooksUsesHookInputCodec () =
    let code = requireFile "src/Opencode/ChatHooks.fs" |> nonCommentCode
    check "arch: Opencode ChatHooks opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode ChatHooks uses resolveHookAgent"
        (code.Contains "resolveHookAgent")
    check "arch: Opencode ChatHooks uses sessionIdFromHookInput"
        (code.Contains "sessionIdFromHookInput")
    check "arch: Opencode ChatHooks must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))

let chatHooksUsesChatHookOutputCodec () =
    let code = requireFile "src/Opencode/ChatHooks.fs" |> nonCommentCode
    check "arch: Opencode ChatHooks references ChatHookOutputCodec"
        (code.Contains "ChatHookOutputCodec")
    check "arch: Opencode ChatHooks must not Dyn.keys existingTools loop"
        (not (code.Contains "Dyn.keys existingTools"))
    check "arch: Opencode ChatHooks uses filterChatToolsForAgent"
        (code.Contains "filterChatToolsForAgent")
    check "arch: Opencode ChatHooks uses encodeToolsOverridesToMessage"
        (code.Contains "encodeToolsOverridesToMessage")

let opencodeMessageTransformUsesHookInputCodec () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode MessageTransform uses sessionIdFromHookInput"
        (code.Contains "sessionIdFromHookInput")
    check "arch: Opencode MessageTransform must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Opencode MessageTransform must not Dyn.str input agent"
        (not (code.Contains "Dyn.str input \"agent\""))

let opencodeMessageTransformUsesResolveMessagesTransformAgent () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/OpencodeHookInputCodec.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform uses resolveMessagesTransformAgent"
        (code.Contains "resolveMessagesTransformAgent")
    check "arch: Opencode MessageTransform must not local resolveAgentFromMessages"
        (not (code.Contains "resolveAgentFromMessages"))
    check "arch: OpencodeHookInputCodec defines resolveMessagesTransformAgent"
        (codec.Contains "resolveMessagesTransformAgent")
    check "arch: OpencodeHookInputCodec defines agentFromMessageInfo"
        (codec.Contains "agentFromMessageInfo")
    check "arch: OpencodeHookInputCodec defines resolveAgentFromMessages"
        (codec.Contains "resolveAgentFromMessages")

let opencodeCommandHooksUsesFromOpencode () =
    let code = requireFile "src/Opencode/CommandHooks.fs" |> nonCommentCode
    check "arch: Opencode CommandHooks opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Opencode CommandHooks opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode CommandHooks loop uses sessionIdFromHookInput"
        (code.Contains "sessionIdFromHookInput")
    check "arch: Opencode CommandHooks uses commandNameFromHookInput"
        (code.Contains "commandNameFromHookInput")
    check "arch: Opencode CommandHooks uses commandArgumentsFromHookInput"
        (code.Contains "commandArgumentsFromHookInput")
    check "arch: Opencode CommandHooks loop-review uses pluginDirectoryFromCtx"
        (code.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode CommandHooks uses decodeHostEventEnvelope for KG cleanup"
        (code.Contains "decodeHostEventEnvelope")
    check "arch: Opencode CommandHooks must not Dyn.str ctx directory"
        (not (code.Contains "Dyn.str ctx \"directory\""))
    check "arch: Opencode CommandHooks must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Opencode CommandHooks must not Dyn.str input command"
        (not (code.Contains "Dyn.str input \"command\""))
    check "arch: Opencode CommandHooks must not Dyn.str input arguments"
        (not (code.Contains "Dyn.str input \"arguments\""))

let opencodeSessionLifecycleObserverUsesHookInputCodec () =
    let code = requireFile "src/Opencode/SessionLifecycleObserver.fs" |> nonCommentCode
    check "arch: Opencode SessionLifecycleObserver opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode SessionLifecycleObserver uses sessionIdFromHookInput for command"
        (code.Contains "sessionIdFromHookInput")
    check "arch: Opencode SessionLifecycleObserver uses toolNameFromHookInput"
        (code.Contains "toolNameFromHookInput")
    check "arch: Opencode SessionLifecycleObserver uses selectMethodologiesFromHookArgs"
        (code.Contains "selectMethodologiesFromHookArgs")
    check "arch: Opencode SessionLifecycleObserver must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Opencode SessionLifecycleObserver must not Dyn.str input tool"
        (not (code.Contains "Dyn.str input \"tool\""))

let opencodeEventHooksUsesEventEnvelopeCodec () =
    let code = requireFile "src/Opencode/EventHooks.fs" |> nonCommentCode
    check "arch: Opencode EventHooks opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode EventHooks uses decodeHostEventEnvelope"
        (code.Contains "decodeHostEventEnvelope")
    check "arch: Opencode EventHooks uses getSessionID from NudgeEventCodec"
        (code.Contains "getSessionID")
    check "arch: Opencode EventHooks must not Dyn.str props sessionID"
        (not (code.Contains "Dyn.str props \"sessionID\""))
    check "arch: Opencode EventHooks must not inline Dyn.get event properties for stream-abort"
        (not (code.Contains "Dyn.get event \"properties\""))

let opencodeToolDefinitionHooksUsesHookInputCodec () =
    let code = requireFile "src/Opencode/ToolDefinitionHooks.fs" |> nonCommentCode
    check "arch: Opencode ToolDefinitionHooks opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode ToolDefinitionHooks uses toolIdFromDefinitionHookInput"
        (code.Contains "toolIdFromDefinitionHookInput")
    check "arch: Opencode ToolDefinitionHooks must not Dyn.str input toolID"
        (not (code.Contains "Dyn.str input \"toolID\""))

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
    check "arch: Opencode KnowledgeGraphTools must not inline Knowledge graph entity from the session snapshot"
        (not (code.Contains "Knowledge graph entity from the session snapshot"))
    check "arch: Opencode KnowledgeGraphTools must not inline Knowledge graph draft entries"
        (not (code.Contains "Knowledge graph draft entries"))

let muxKnowledgeGraphStartBookkeeperUsesFromMuxConfig () =
    let code = requireFile "src/Mux/KnowledgeGraphTools.fs" |> nonCommentCode
    check "arch: Mux KnowledgeGraphTools opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Mux KnowledgeGraphTools StartBookkeeperAppend uses fromMuxConfig"
        (code.Contains "fromMuxConfig")
    check "arch: Mux KnowledgeGraphTools StartBookkeeperAppend uses Execution.Directory"
        (code.Contains "runtime.Execution.Directory")
    check "arch: Mux KnowledgeGraphTools StartBookkeeperAppend must not Dyn.str cfg directory"
        (not (code.Contains "Dyn.str cfg \"directory\""))
    check "arch: Mux KnowledgeGraphTools StartBookkeeperAppend uses muxConfigDirectoryFallback on fromMuxConfig failure"
        (code.Contains "muxConfigDirectoryFallback")

let muxHostToolsReadWriteUsesToolCatalog () =
    let code = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    check "arch: Mux HostTools read uses ToolCatalog description"
        (code.Contains "description \"read\"")
    check "arch: Mux HostTools write uses ToolCatalog description"
        (code.Contains "description \"write\"")
    check "arch: Mux HostTools read uses Params.readPath"
        (code.Contains "Params.readPath")
    check "arch: Mux HostTools read uses Params.readOffset"
        (code.Contains "Params.readOffset")
    check "arch: Mux HostTools read uses Params.readLimit"
        (code.Contains "Params.readLimit")
    check "arch: Mux HostTools write uses Params.writeFilePath"
        (code.Contains "Params.writeFilePath")
    check "arch: Mux HostTools write uses Params.writeContent"
        (code.Contains "Params.writeContent")
    check "arch: Mux HostTools read must not inline directory listing description"
        (not (code.Contains "formatted directory listing"))
    check "arch: Mux HostTools write must not inline syntax checking description"
        (not (code.Contains "runs syntax checking on the written content"))
    check "arch: Mux HostTools read uses fromMuxConfig"
        ((code.Contains "readTool") && (code.IndexOf("fromMuxConfig", code.IndexOf("readTool")) >= 0))
    check "arch: Mux HostTools write uses fromMuxConfig"
        ((code.Contains "writeTool") && (code.IndexOf("fromMuxConfig", code.IndexOf("writeTool")) >= 0))

let muxHostToolsReadWriteUsesFileToolsCodec () =
    let code = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    check "arch: Mux HostTools opens FileToolsCodec" (code.Contains "FileToolsCodec")
    check "arch: Mux HostTools read uses decodeReadArgs" (code.Contains "decodeReadArgs")
    check "arch: Mux HostTools read uses readArgsForHost" (code.Contains "readArgsForHost")
    check "arch: Mux HostTools write uses decodeWriteArgs" (code.Contains "decodeWriteArgs")
    check "arch: Mux HostTools must not Dyn.str args path" (not (code.Contains "Dyn.str args \"path\""))
    check "arch: Mux HostTools must not Dyn.str args file_path" (not (code.Contains "Dyn.str args \"file_path\""))
    check "arch: Mux HostTools must not Dyn.str args content" (not (code.Contains "Dyn.str args \"content\""))
    check "arch: Mux HostTools read decode uses wireDecodeFailure"
        (code.Contains "wireDecodeFailure \"read\"")
    check "arch: Mux HostTools write decode uses wireDecodeFailure"
        (code.Contains "wireDecodeFailure \"write\"")

let muxWrappersTodoUsesWorkBacklogToolsCodec () =
    let code = requireFile "src/Mux/Wrappers.fs" |> nonCommentCode
    check "arch: Mux Wrappers opens WorkBacklogToolsCodec" (code.Contains "WorkBacklogToolsCodec")
    check "arch: Mux Wrappers uses decodeTodoWriteArgs" (code.Contains "decodeTodoWriteArgs")
    check "arch: Mux Wrappers uses decodeTodoToolOpts" (code.Contains "decodeTodoToolOpts")
    check "arch: Mux Wrappers must not Dyn.str args completedWorkReport" (not (code.Contains "Dyn.str args \"completedWorkReport\""))
    check "arch: Mux Wrappers must not Dyn.get args select_methodology" (not (code.Contains "Dyn.get args \"select_methodology\""))
    check "arch: Mux Wrappers must not Dyn.str opts toolCallId" (not (code.Contains "Dyn.str opts \"toolCallId\""))
    check "arch: Mux Wrappers requireWorkspaceId uses decodeMuxConfig" (code.Contains "decodeMuxConfig")
    let todoIdx = code.IndexOf "mkTodoWriteWrapper"
    let todoWindow =
        if todoIdx >= 0 then code.Substring(todoIdx, min 800 (code.Length - todoIdx))
        else ""
    check "arch: Mux Wrappers todo decode failure sets success false"
        (todoWindow.Contains "\"success\"" && todoWindow.Contains "false")
    check "arch: Mux Wrappers opens ToolExecute"
        (code.Contains "ToolExecute")
    check "arch: Mux Wrappers todo decode failure uses wireDecodeFailure todowrite"
        (todoWindow.Contains "wireDecodeFailure \"todowrite\"")
    check "arch: Mux Wrappers todo decode failure must not formatDomainError in mkTodoWriteWrapper"
        (not (todoWindow.Contains "formatDomainError"))

let opencodeHookExecuteUsesPatchToolsCodec () =
    let code = requireFile "src/Opencode/HookExecute.fs" |> nonCommentCode
    check "arch: Opencode HookExecute opens PatchToolsCodec" (code.Contains "PatchToolsCodec")
    check "arch: Opencode HookExecute uses decodeApplyPatchFields" (code.Contains "decodeApplyPatchFields")
    check "arch: Opencode HookExecute must not Dyn.str args patchText" (not (code.Contains "Dyn.str args \"patchText\""))
    check "arch: Opencode HookExecute must not Dyn.str args patch" (not (code.Contains "Dyn.str args \"patch\""))
    let patchIdx = code.IndexOf "decodeApplyPatchFields"
    let patchWindow =
        if patchIdx >= 0 then code.Substring(patchIdx, min 400 (code.Length - patchIdx))
        else ""
    check "arch: Opencode HookExecute patch decode failure sets output error"
        (patchWindow.Contains "setKey output \"error\"")
    check "arch: Opencode HookExecute opens ToolExecute"
        (code.Contains "ToolExecute")
    check "arch: Opencode HookExecute patch decode failure uses wireEncodeToolError apply_patch"
        (patchWindow.Contains "wireEncodeToolError \"apply_patch\"")
    check "arch: Opencode HookExecute patch decode failure must not formatDomainError"
        (not (patchWindow.Contains "formatDomainError"))

let shellCodecFilesNoLocalStrField () =
    let paths =
        [| "src/Shell/FileToolsCodec.fs"
           "src/Shell/FuzzyToolsCodec.fs"
           "src/Shell/PatchToolsCodec.fs"
           "src/Shell/WorkBacklogToolsCodec.fs"
           "src/Shell/ExecutorToolsCodec.fs"
           "src/Shell/DelegateToolsCodec.fs"
           "src/Shell/WebToolsCodec.fs"
           "src/Shell/ReviewToolsCodec.fs"
           "src/Shell/KnowledgeGraphToolsCodec.fs"
           "src/Shell/SubagentSimpleArgsCodec.fs" |]
    for path in paths do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " must not define local let strField")
            (not (code.Contains "let strField"))
        check ("arch: " + path + " must not define local let optInt") (not (code.Contains "let optInt"))
        check ("arch: " + path + " must not define local let optBool") (not (code.Contains "let optBool"))

let muxAiSettingsUsesMuxAiSettingsCodec () =
    let code = requireFile "src/Mux/AiSettings.fs" |> nonCommentCode
    check "arch: Mux AiSettings opens MuxAiSettingsCodec" (code.Contains "MuxAiSettingsCodec")
    check "arch: Mux AiSettings uses decodeMuxDelegateConfigLenient" (code.Contains "decodeMuxDelegateConfigLenient")
    check "arch: Mux AiSettings uses readMuxConfigFileDefaults" (code.Contains "readMuxConfigFileDefaults")
    check "arch: Mux AiSettings uses readWorkspaceAiSettingsByAgent" (code.Contains "readWorkspaceAiSettingsByAgent")
    check "arch: Mux AiSettings uses readDescriptorAiFromFrontmatter" (code.Contains "readDescriptorAiFromFrontmatter")
    check "arch: Mux AiSettings uses readParentMuxEnv" (code.Contains "readParentMuxEnv")
    check "arch: Mux AiSettings uses readWorkspaceFromFindResult" (code.Contains "readWorkspaceFromFindResult")
    check "arch: Mux AiSettings must not define private decodeAiConfig" (not (code.Contains "let private decodeAiConfig"))
    check "arch: Mux AiSettings must not define private readMuxEnvSettings" (not (code.Contains "let private readMuxEnvSettings"))
    check "arch: Mux AiSettings must not define private normalizeStr" (not (code.Contains "let private normalizeStr"))
    check "arch: Mux AiSettings must not define private thinkingLevelMap" (not (code.Contains "let private thinkingLevelMap"))
    check "arch: Mux AiSettings resolve must not Dyn.get config runtime" (not (code.Contains "Dyn.get config \"runtime\""))
    check "arch: Mux AiSettings resolve must not Dyn.get config cwd" (not (code.Contains "Dyn.str config \"cwd\""))
    check "arch: Mux AiSettings must not Dyn.get configFile" (not (code.Contains "Dyn.get configFile"))
    check "arch: Mux AiSettings must not Dyn.get subagentAiDefaults" (not (code.Contains "subagentAiDefaults"))
    check "arch: Mux AiSettings must not Dyn.get agentAiDefaults" (not (code.Contains "agentAiDefaults"))
    check "arch: Mux AiSettings must not Dyn.get aiSettingsByAgent" (not (code.Contains "aiSettingsByAgent"))
    check "arch: Mux AiSettings must not Dyn.get workspace field" (not (code.Contains "Dyn.get result \"workspace\""))
    check "arch: Mux AiSettings must not Dyn.get config muxEnv" (not (code.Contains "Dyn.get config \"muxEnv\""))

let muxDelegateUsesDelegateToolsCodec () =
    let code = requireFile "src/Mux/Delegate.fs" |> nonCommentCode
    check "arch: Mux Delegate opens DelegateToolsCodec" (code.Contains "DelegateToolsCodec")
    check "arch: Mux Delegate uses decodeDelegateConfig" (code.Contains "decodeDelegateConfig")
    check "arch: Mux Delegate uses decodeTaskCreateResult" (code.Contains "decodeTaskCreateResult")
    check "arch: Mux Delegate uses decodeTaskReport" (code.Contains "decodeTaskReport")
    check "arch: Mux Delegate must not Dyn.str config workspaceId" (not (code.Contains "Dyn.str config \"workspaceId\""))
    check "arch: Mux Delegate must not Dyn.str createResult error" (not (code.Contains "Dyn.str createResult \"error\""))
    check "arch: Mux Delegate must not Dyn.str report reportMarkdown" (not (code.Contains "Dyn.str report \"reportMarkdown\""))

let muxHookInputCodecExecutorReadOnlyUsesCodec () =
    let code = requireFile "src/Shell/MuxHookInputCodec.fs" |> nonCommentCode
    check "arch: MuxHookInputCodec opens ExecutorToolsCodec" (code.Contains "ExecutorToolsCodec")
    check "arch: MuxHookInputCodec isReadOnlyExecutorMux uses peekExecutorMode" (code.Contains "peekExecutorMode")
    check "arch: MuxHookInputCodec isReadOnlyExecutorMux must not Dyn.str args mode" (not (code.Contains "Dyn.str args \"mode\""))

let knowledgeGraphSessionMessagesNotInRuntimeIO () =
    let io = requireFile "src/Opencode/KnowledgeGraphRuntimeIO.fs" |> nonCommentCode
    let session = requireFile "src/Opencode/KnowledgeGraphSessionMessages.fs" |> nonCommentCode
    check "arch: KnowledgeGraphSessionMessages defines fetchSessionMessageArray"
        (session.Contains "let fetchSessionMessageArray")
    check "arch: KnowledgeGraphSessionMessages defines loadSessionMessages"
        (session.Contains "let loadSessionMessages")
    check "arch: KnowledgeGraphSessionMessages defines tryResolveJobContext"
        (session.Contains "let tryResolveJobContext")
    check "arch: Opencode KnowledgeGraphRuntimeIO no fetchSessionMessageArray"
        (not (io.Contains "let fetchSessionMessageArray"))
    check "arch: Opencode KnowledgeGraphRuntimeIO no session messages invoke1"
        (not (io.Contains "let invoke1"))
    check "arch: Opencode KnowledgeGraphRuntimeIO no MessagingCodec.decodeMessages"
        (not (io.Contains "MessagingCodec.decodeMessages"))
    check "arch: Opencode KnowledgeGraphRuntimeIO opens or aliases SessionMessages"
        (io.Contains "KnowledgeGraphSessionMessages")

let muxHostToolsExecutorUsesFromMuxConfig () =
    let code = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    check "arch: Mux HostTools executor opens ToolCopy"
        (code.Contains "ToolCopy")
    check "arch: Mux HostTools executor uses fromMuxConfig"
        (code.Contains "fromMuxConfig")
    check "arch: Mux HostTools executor uses runtime.Execution for session and directory"
        ((code.Contains "runtime.Execution.SessionId")
         && (code.Contains "runtime.Execution.Directory"))
    check "arch: Mux HostTools executor uses executorRequiresSession"
        (code.Contains "executorRequiresSession")
    check "arch: Mux HostTools executor must not Dyn.str config sessionID"
        (not (code.Contains "Dyn.str config \"sessionID\""))

let muxHostToolsExecutorUsesExecutorToolsCodec () =
    let code = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    check "arch: Mux HostTools executor opens ExecutorToolsCodec"
        (code.Contains "ExecutorToolsCodec")
    check "arch: Mux HostTools executor uses decodeExecutorArgs"
        (code.Contains "decodeExecutorArgs")
    check "arch: Mux HostTools executor uses toExecuteOptions"
        (code.Contains "toExecuteOptions")
    check "arch: Mux HostTools executor must not buildExecutorOptions"
        (not (code.Contains "buildExecutorOptions"))
    check "arch: Mux HostTools executor must not Dyn.str args language"
        (not (code.Contains "Dyn.str args \"language\""))
    check "arch: Mux HostTools executor must not Dyn.str args program"
        (not (code.Contains "Dyn.str args \"program\""))
    check "arch: Mux HostTools executor must not Dyn.str args mode"
        (not (code.Contains "Dyn.str args \"mode\""))
    check "arch: Mux HostTools executor must not Dyn.str args timeout_type"
        (not (code.Contains "Dyn.str args \"timeout_type\""))
    check "arch: Mux HostTools executor uses wireDomainFailure for executor decode"
        (code.Contains "wireDomainFailure \"Executor\"")

let muxHostToolsWireDecodeFailures () =
    let code = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    check "arch: Mux HostTools opens ToolExecute" (code.Contains "ToolExecute")
    check "arch: Mux HostTools executor uses wireDomainFailure Executor"
        (code.Contains "wireDomainFailure \"Executor\"")
    check "arch: Mux HostTools fuzzy_find uses wireDecodeFailure"
        (code.Contains "wireDecodeFailure \"fuzzy_find\"")
    check "arch: Mux HostTools fuzzy_grep uses wireDecodeFailure"
        (code.Contains "wireDecodeFailure \"fuzzy_grep\"")
    check "arch: Mux HostTools read uses wireDecodeFailure"
        (code.Contains "wireDecodeFailure \"read\"")
    check "arch: Mux HostTools write uses wireDecodeFailure"
        (code.Contains "wireDecodeFailure \"write\"")
    check "arch: Mux HostTools fromMuxConfig uses wireEncodeToolError MuxConfig"
        (code.Contains "wireEncodeToolError \"MuxConfig\"")

let muxWebToolsUsesWireDecodeFailure () =
    let web = requireFile "src/Mux/WebTools.fs" |> nonCommentCode
    check "arch: Mux WebTools opens ToolExecute" (web.Contains "ToolExecute")
    check "arch: Mux WebTools websearch uses wireDecodeFailure"
        (web.Contains "wireDecodeFailure \"websearch\"")
    check "arch: Mux WebTools webfetch uses wireDecodeFailure"
        (web.Contains "wireDecodeFailure \"webfetch\"")
    check "arch: Mux WebTools fromMuxConfig uses wireEncodeToolError MuxConfig"
        (web.Contains "wireEncodeToolError \"MuxConfig\"")
    check "arch: Mux WebTools websearch must not formatDomainError on decode path"
        (not (web.Contains "resolveStr (formatDomainError e)"))
    check "arch: Mux WebTools webfetch must not formatDomainError on decode path"
        (not (web.Contains "match decodeWebfetchArgs args, fromMuxConfig config with"))

let kernelToolCopyWebExecutorFields () =
    let code = requireFile "src/Kernel/ToolCopy.fs" |> nonCommentCode
    check "arch: ToolCopy defines webSearchRequiredField"
        (code.Contains "let webSearchRequiredField")
    check "arch: ToolCopy defines webFetchRequiredField"
        (code.Contains "let webFetchRequiredField")
    check "arch: ToolCopy defines executorRequiresSession"
        (code.Contains "let executorRequiresSession")
    check "arch: ToolCopy defines toolRequiresActiveSession"
        (code.Contains "let toolRequiresActiveSession")
    check "arch: ToolCopy defines muxFuzzyFindRequiresWorkspaceId"
        (code.Contains "let muxFuzzyFindRequiresWorkspaceId")
    check "arch: ToolCopy defines muxFuzzyGrepRequiresWorkspaceId"
        (code.Contains "let muxFuzzyGrepRequiresWorkspaceId")
    let webIdx = code.IndexOf "let webToolFailed"
    check "arch: ToolCopy defines webToolFailed" (webIdx >= 0)
    let webWindow =
        if webIdx >= 0 then code.Substring(webIdx, min 200 (code.Length - webIdx)) else ""
    check "arch: ToolCopy webToolFailed uses wireEncodeToolError"
        (webWindow.Contains "wireEncodeToolError")
    check "arch: ToolCopy webToolFailed must not concat formatDomainError"
        (not (webWindow.Contains "formatDomainError"))
    check "arch: ToolCopy must not inline Web failed formatDomainError template"
        (not (code.Contains "{formatDomainError error}"))

let sessionExecutorCreateForScope () =
    let code = requireFile "src/Shell/SessionExecutor.fs" |> nonCommentCode
    check "arch: SessionExecutor defines createForScope"
        (code.Contains "let createForScope")
    check "arch: SessionExecutor type binds RuntimeScope"
        (code.Contains "type SessionExecutor(scope: RuntimeScope)")

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

let knowledgeGraphRuntimeNoTestDrainMembers () =
    for path in [| "src/Opencode/KnowledgeGraphRuntime.fs"; "src/Mux/KnowledgeGraphTools.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " no TakeBookkeeperLaunches")
            (not (code.Contains "TakeBookkeeperLaunches"))
        check ("arch: " + path + " no WaitForBackgroundJobs")
            (not (code.Contains "WaitForBackgroundJobs"))
    for path in [| "src/Opencode/KnowledgeGraphTestHooks.fs"; "src/Mux/KnowledgeGraphTestHooks.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " TakeLaunches uses drainLaunches")
            (code.Contains "drainLaunches")

let knowledgeGraphRuntimeNoSwapStateMembers () =
    for path in [| "src/Opencode/KnowledgeGraphRuntime.fs"; "src/Mux/KnowledgeGraphTools.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " no member SwapState")
            (not (code.Contains "member _.SwapState"))
        check ("arch: " + path + " no member RunOnCommandQueue")
            (not (code.Contains "member _.RunOnCommandQueue"))
        check ("arch: " + path + " no member AwaitBackgroundSinkJobs")
            (not (code.Contains "member _.AwaitBackgroundSinkJobs"))
    for path in [| "src/Opencode/KnowledgeGraphTestHooks.fs"; "src/Mux/KnowledgeGraphTestHooks.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " uses CreateTestPorts")
            (code.Contains "CreateTestPorts")

let runtimeScopeNoGetDefault () =
    let code = requireFile "src/Shell/RuntimeScope.fs" |> nonCommentCode
    check "arch: RuntimeScope must not define getDefault"
        (not (code.Contains "let getDefault"))
    check "arch: RuntimeScope must not define resetDefaultForTesting"
        (not (code.Contains "let resetDefaultForTesting"))
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