module Wanxiangshu.Tests.ArchitectureTestsMessageTransform

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

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

