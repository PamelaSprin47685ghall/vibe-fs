module Wanxiangshu.Tests.ArchitectureTestsWirePipeline

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let messageTransformUsesBacklogSessionOpsFrom () =
    let core = requireFile "src/Shell/MessageTransformCore.fs" |> nonCommentCode
    check "arch: MessageTransformCore defines backlogSessionOpsFrom" (core.Contains "let backlogSessionOpsFrom")

    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " uses backlogSessionOpsFrom") (code.Contains "backlogSessionOpsFrom")

        check
            ("arch: " + path + " must not inline BacklogSessionOps record for backlog")
            (not (code.Contains "GetOrRebuildBacklog = fun sid msgs"))

let messageTransformUsesChatTransformOutputCodec () =
    let codec = requireFile "src/Shell/ChatTransformOutputCodec.fs" |> nonCommentCode

    check
        "arch: ChatTransformOutputCodec defines tryGetMessagesArrayFromOutput"
        (codec.Contains "let tryGetMessagesArrayFromOutput")

    check
        "arch: ChatTransformOutputCodec defines clearSystemOutputLength"
        (codec.Contains "let clearSystemOutputLength")

    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " opens ChatTransformOutputCodec") (code.Contains "ChatTransformOutputCodec")
        check ("arch: " + path + " uses tryGetMessagesArrayFromOutput") (code.Contains "tryGetMessagesArrayFromOutput")

        check
            ("arch: " + path + " must not Dyn.get output messages")
            (not (code.Contains "Dyn.get output \"messages\""))

    let opencode = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode

    check
        "arch: Opencode MessageTransform uses setSystemOutputToDirectory"
        (opencode.Contains "setSystemOutputToDirectory")

let messageTransformUsesMessageTransformCore () =
    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " opens MessageTransformCore") (code.Contains "MessageTransformCore")
        check ("arch: " + path + " no direct projectBacklogFor") (not (code.Contains "projectBacklogFor"))

    let core = requireFile "src/Shell/MessageTransformCore.fs" |> nonCommentCode
    check "arch: MessageTransformCore defines applyBacklogProjection" (core.Contains "let applyBacklogProjection")

let messageTransformUsesPipeline () =
    let pipeline = requireFile "src/Shell/MessageTransformPipeline.fs" |> nonCommentCode

    check
        "arch: MessageTransformPipeline defines runMessageTransformPipeline"
        (pipeline.Contains "let runMessageTransformPipeline")

    check "arch: MessageTransformPipeline defines MessageTransformPlan" (pipeline.Contains "type MessageTransformPlan")

    let hostEntry =
        requireFile "src/Shell/MessageTransformHostEntry.fs" |> nonCommentCode

    check
        "arch: MessageTransformHostEntry uses runMessageTransformPipeline"
        (hostEntry.Contains "runMessageTransformPipeline")

    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let code = requireFile path |> nonCommentCode

        check
            ("arch: " + path + " must not call runMessageTransformPipeline directly")
            (not (code.Contains "runMessageTransformPipeline"))

let messageTransformUsesCapsKgHostHooks () =
    let hooks = requireFile "src/Shell/MessageTransformHostHooks.fs" |> nonCommentCode
    check "arch: MessageTransformHostHooks defines loadCapsForScope" (hooks.Contains "let loadCapsForScope")
    check "arch: MessageTransformHostHooks defines loadKgPreludeForAgent" (hooks.Contains "let loadKgPreludeForAgent")
    check "arch: MessageTransformHostHooks defines CapsLoadPolicy" (hooks.Contains "type CapsLoadPolicy")

    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " opens MessageTransformHostHooks") (code.Contains "MessageTransformHostHooks")
        check ("arch: " + path + " uses loadCapsForScope") (code.Contains "loadCapsForScope")
        check ("arch: " + path + " uses loadKgPreludeForAgent") (code.Contains "loadKgPreludeForAgent")

        check
            ("arch: " + path + " must not inline getOrLoadCapsFilesForScope")
            (not (code.Contains "getOrLoadCapsFilesForScope"))

let dualHostMessagingCodecUsesEncodeHelpers () =
    let helpers = requireFile "src/Shell/MessagingEncodeHelpers.fs" |> nonCommentCode

    check
        "arch: MessagingEncodeHelpers defines replacePartsOnRawMessage"
        (helpers.Contains "let replacePartsOnRawMessage")

    for path in [| "src/Opencode/MessagingCodec.fs"; "src/Mux/MessagingCodec.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " opens MessagingEncodeHelpers") (code.Contains "MessagingEncodeHelpers")
        check ("arch: " + path + " uses replacePartsOnRawMessage") (code.Contains "replacePartsOnRawMessage")

        check
            ("arch: " + path + " must not inline Dyn.withKey rawMsg parts")
            (not (code.Contains "Dyn.withKey rawMsg \"parts\""))

let messagingWireForkDocumented () =
    // MESSAGING_WIRE.md was intentionally removed; the dual-host wire fork
    // rules are now enforced directly against source by the checks above.
    ()

let hostObjBoundaryDocumented () =
    // HOST_OBJ_BOUNDARY.md was intentionally removed; obj-boundary policy is
    // enforced by shellLayering / opencodeNoMuxRef / ToolArgsDecode coverage.
    ()

let opencodeMessageTransformUsesHookInputCodec () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform opens OpencodeHookInputCodec" (code.Contains "OpencodeHookInputCodec")

    check
        "arch: Opencode MessageTransform must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))

    check "arch: Opencode MessageTransform must not Dyn.str input agent" (not (code.Contains "Dyn.str input \"agent\""))

let opencodeMessageTransformUsesResolveMessagesTransformAgent () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/OpencodeHookInputCodec.fs" |> nonCommentCode

    check
        "arch: Opencode MessageTransform uses resolveMessagesTransformAgent"
        (code.Contains "resolveMessagesTransformAgent")

    check
        "arch: Opencode MessageTransform must not local resolveAgentFromMessages"
        (not (code.Contains "resolveAgentFromMessages"))

    check
        "arch: OpencodeHookInputCodec defines resolveMessagesTransformAgent"
        (codec.Contains "resolveMessagesTransformAgent")

    check "arch: OpencodeHookInputCodec defines agentFromMessageInfo" (codec.Contains "agentFromMessageInfo")
    check "arch: OpencodeHookInputCodec defines resolveAgentFromMessages" (codec.Contains "resolveAgentFromMessages")
