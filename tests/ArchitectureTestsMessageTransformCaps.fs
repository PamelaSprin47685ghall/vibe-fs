module Wanxiangshu.Tests.ArchitectureTestsMessageTransformCaps

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let muxMessageTransformUsesReadDedupMuxPlugin () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform opens ReadDedupMuxPlugin" (code.Contains "ReadDedupMuxPlugin")

    check
        "arch: Mux MessageTransform must not open Mux.ReadDedup for plugin dedup"
        (not (code.Contains "Mux.ReadDedup"))

let muxMessageTransformUsesMuxHookInputCodec () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform opens MuxHookInputCodec" (code.Contains "MuxHookInputCodec")

    check
        "arch: Mux MessageTransform uses decodeMuxMessagesTransformInput"
        (code.Contains "decodeMuxMessagesTransformInput")

    check "arch: Mux MessageTransform must not Dyn.str input agent" (not (code.Contains "Dyn.str input \"agent\""))

    check
        "arch: Mux MessageTransform must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))

    check
        "arch: Mux MessageTransform must not Dyn.str input directory"
        (not (code.Contains "Dyn.str input \"directory\""))

let muxWrappersCaptureUsesProjectionNotModuleCapture () =
    let code = requireFile "src/Mux/Wrappers.fs" |> nonCommentCode
    check "arch: Mux Wrappers uses projection.CaptureBacklogEntry" (code.Contains "projection.CaptureBacklogEntry")
    check "arch: Mux Wrappers must not module captureReport" (not (code.Contains "captureReport host"))
    check "arch: Mux createAllWrappers passes RuntimeScope" (code.Contains "scope: RuntimeScope")

let opencodeMessageTransformNoLocalCapsBuilder () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode

    check
        "arch: Opencode MessageTransform no local buildCapsMessages"
        (not (code.Contains "let private buildCapsMessages"))

    check "arch: Opencode MessageTransform delegates CapsCodec" (code.Contains "Opencode.CapsCodec")
    check "arch: Opencode MessageTransform no local CapsFileCache" (not (code.Contains "module private CapsFileCache"))

let opencodeMessageTransformUsesShellCapsCache () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform opens MessageTransformHostHooks" (code.Contains "MessageTransformHostHooks")
    check "arch: Opencode MessageTransform uses loadCapsForScope" (code.Contains "loadCapsForScope")

    check
        "arch: Opencode MessageTransform must not inline getOrLoadCapsFilesForScope"
        (not (code.Contains "getOrLoadCapsFilesForScope"))

    check "arch: Opencode MessageTransform no local CapsFileCache" (not (code.Contains "module private CapsFileCache"))
    check "arch: Opencode MessageTransform no direct findCapsFiles" (not (code.Contains "findCapsFiles"))

let noReconstructReviewStateInMessageTransforms () =
    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let content = requireFile path
        check ("arch: " + path + " no reconstructReviewState") (not (content.Contains "reconstructReviewState"))

let messageTransformUsesHostEntry () =
    let hostEntry =
        requireFile "src/Shell/MessageTransformHostEntry.fs" |> nonCommentCode

    check "arch: MessageTransformHostEntry defines ReviewReplayMode" (hostEntry.Contains "type ReviewReplayMode")

    check
        "arch: MessageTransformHostEntry defines runHostMessagesTransform"
        (hostEntry.Contains "let runHostMessagesTransform")

    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " opens MessageTransformHostEntry") (code.Contains "MessageTransformHostEntry")
        check ("arch: " + path + " uses runHostMessagesTransform") (code.Contains "runHostMessagesTransform")
        check ("arch: " + path + " forbids replayReviewIfStoreEmpty") (not (code.Contains "replayReviewIfStoreEmpty"))
        check ("arch: " + path + " forbids replayReviewAlwaysSync") (not (code.Contains "replayReviewAlwaysSync"))

    let opencode = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform uses IfStoreEmpty replay mode" (opencode.Contains "IfStoreEmpty")
    let mux = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform uses IfStoreEmpty replay mode" (mux.Contains "IfStoreEmpty")

let capsFileCacheCompositeKey () =
    let code = requireFile "src/Shell/CapsFileCache.fs" |> nonCommentCode
    check "arch: CapsFileCache defines cacheKey" (code.Contains "cacheKey")
    check "arch: CapsFileCache cacheKey binds directory" (code.Contains "cacheKey sessionID directory")
    check "arch: CapsFileCache loads via RuntimeScope TryGetCapsFiles" (code.Contains "TryGetCapsFiles")
    check "arch: CapsFileCache stores via RuntimeScope AddCapsFilesIfAbsent" (code.Contains "AddCapsFilesIfAbsent")
    check "arch: CapsFileCache normalizes directory for cache" (code.Contains "normalizeDirectory")
    let scopeCode = requireFile "src/Shell/RuntimeScope.fs" |> nonCommentCode
    check "arch: RuntimeScope holds capsFiles map" (scopeCode.Contains "capsFiles")

let capsFileCacheNoGetOrLoadCapsFilesDefault () =
    let code = requireFile "src/Shell/CapsFileCache.fs" |> nonCommentCode
    check "arch: CapsFileCache must not define getOrLoadCapsFiles" (not (code.Contains "let getOrLoadCapsFiles "))
    check "arch: CapsFileCache must not call getOrLoadCapsFiles(" (not (code.Contains "getOrLoadCapsFiles("))
    check "arch: CapsFileCache must not use getDefault" (not (code.Contains "getDefault"))

let capsFileCacheUsesInflight () =
    let cacheCode = requireFile "src/Shell/CapsFileCache.fs" |> nonCommentCode
    let scopeCode = requireFile "src/Shell/RuntimeScope.fs" |> nonCommentCode
    check "arch: CapsFileCache uses GetOrLoadCapsInflight" (cacheCode.Contains "GetOrLoadCapsInflight")
    check "arch: RuntimeScope defines GetOrLoadCapsInflight" (scopeCode.Contains "GetOrLoadCapsInflight")
    check "arch: RuntimeScope holds capsInflight map" (scopeCode.Contains "capsInflight")
