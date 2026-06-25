module VibeFs.Tests.ArchitectureTestsMessageTransform

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.ArchitectureTestsSupport

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