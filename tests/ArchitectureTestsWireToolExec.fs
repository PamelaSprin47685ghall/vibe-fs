module Wanxiangshu.Tests.ArchitectureTestsWireToolExec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let toolExecuteWireHelperExists () =
    let code = requireFile "src/Shell/ToolExecute.fs" |> nonCommentCode
    check "arch: ToolExecute defines wireDecodeFailure"
        (code.Contains "let wireDecodeFailure")
    check "arch: ToolExecute wireDecodeFailure uses wireEncodeToolError"
        (code.Contains "wireEncodeToolError")
    check "arch: ToolExecute defines wireDomainFailure"
        (code.Contains "let wireDomainFailure")
    check "arch: ToolExecute defines runDecodedToWire"
        (code.Contains "let runDecodedToWire")
    check "arch: ToolExecute defines mapDecodeError"
        (code.Contains "let mapDecodeError")
    check "arch: ToolExecute must not formatDomainError"
        (not (code.Contains "formatDomainError"))

let opencodeToolsUseWireEncodeForClient () =
    let executor = requireFile "src/Opencode/ExecutorTool.fs" |> nonCommentCode
    let subagent = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    let review = requireFile "src/Opencode/ReviewTools.fs" |> nonCommentCode
    let search = requireFile "src/Opencode/SearchTools.fs" |> nonCommentCode
    let result = requireFile "src/Kernel/ToolResult.fs" |> nonCommentCode
    for (label, code) in [| "SearchTools", search; "ExecutorTool", executor; "ReviewTools", review; "SubagentTools", subagent |] do
        check ("arch: " + label + " opens ToolResult")
            (code.Contains "ToolResult")
        check ("arch: " + label + " client failure uses wireEncodeToolError OpencodeClient")
            (code.Contains "wireEncodeToolError \"OpencodeClient\"")
        check ("arch: " + label + " must not formatDomainError for client")
            (not (code.Contains "formatDomainError"))
    check "arch: Kernel ToolResult defines wireEncodeToolError"
        (result.Contains "let wireEncodeToolError")

let muxHostToolsWireDecodeFailures () =
    let host = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    let toolExec = requireFile "src/Shell/ToolExecute.fs" |> nonCommentCode
    check "arch: Mux HostTools non-comment code uses wireDecodeFailure"
        (host.Contains "wireDecodeFailure")
    check "arch: Mux HostTools non-comment code uses wireEncodeToolError MuxConfig"
        (host.Contains "wireEncodeToolError \"MuxConfig\"")
    check "arch: Mux HostTools non-comment code uses wireDomainFailure write"
        (host.Contains "wireDomainFailure \"write\"")
    check "arch: ToolExecute exposes wireEncodeToolError"
        (toolExec.Contains "wireEncodeToolError")

let muxWebToolsUsesWireDecodeFailure () =
    let web = requireFile "src/Mux/WebTools.fs" |> nonCommentCode
    check "arch: Mux WebTools uses wireDecodeFailure"
        (web.Contains "wireDecodeFailure")
    check "arch: Mux WebTools uses wireEncodeToolError MuxConfig"
        (web.Contains "wireEncodeToolError \"MuxConfig\"")
    check "arch: Mux WebTools must not formatDomainError for decode failure"
        (not (web.Contains "formatDomainError"))

let kernelToolCopyWebExecutorFields () =
    let copy = requireFile "src/Kernel/ToolCopy.fs" |> nonCommentCode
    let result = requireFile "src/Kernel/ToolResult.fs" |> nonCommentCode
    check "arch: ToolCopy.webToolFailed uses wireEncodeToolError $Web label"
        (copy.Contains "wireEncodeToolError $\"Web {label}\"")
    check "arch: ToolCopy.executorRequiresSession uses wireEncodeToolError"
        (copy.Contains "wireEncodeToolError")
    check "arch: ToolCopy.executorInvalidLanguage uses wireEncodeToolError"
        (copy.Contains "wireEncodeToolError")
    check "arch: Kernel ToolResult defines wireEncodeResult"
        (result.Contains "let wireEncodeResult")

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
           "src/Shell/KnowledgeGraphToolsCodec.fs" |]
    for path in paths do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " must not locally define let strField")
            (not (code.Contains "let strField "))
        check ("arch: " + path + " must not locally define let optInt")
            (not (code.Contains "let optInt "))
        check ("arch: " + path + " must not locally define let hasField")
            (not (code.Contains "let hasField "))
    check "arch: Shell DynField exists"
        (requireFile "src/Shell/DynField.fs" |> nonCommentCode |> fun s -> s.Contains "let strField")
    let dynField = requireFile "src/Shell/DynField.fs" |> nonCommentCode
    check "arch: DynField defines requiredStrField" (dynField.Contains "let requiredStrField")
    check "arch: DynField defines strListField" (dynField.Contains "let strListField")
    let dynMod = requireFile "src/Shell/Dyn.fs" |> nonCommentCode
    check "arch: Dyn.fs NoComparison NoEquality markers"
        (dynMod.Contains "NoComparison" && dynMod.Contains "NoEquality")

let private shellBareDynAllowlist =
    Set.ofList
        [ "ExecutorJavascript.fs"
          "FuzzyFinderShell.fs"
          "FuzzySearch.fs"
          "FuzzySearchHelpers.fs"
          "FuzzySearchFind.fs"
          "FuzzySearchGrep.fs"
          "OpencodeSessionEventCodecCommon.fs"
          "OpencodeSessionEventNudge.fs"
          "MuxHostBindings.fs"
          "NudgeRuntime.fs"
          "OmpHostBindings.fs"
          "OpencodeAgentConfigWire.fs"
          "ReadDedupMuxPlugin.fs"
          "ReadDedupOpenCode.fs"
          "SubagentIo.fs"
          "ToolRuntimeContext.fs"
          "TreeSitterPlatform.fs"
          "WebSearchApi.fs"
          "WorkspaceFiles.fs" ]

let shellNonCodecMustUseDynFieldHelpers () =
    for f in fsFiles "src/Shell" do
        if f = "Dyn.fs" || f = "DynField.fs" || f.EndsWith "Codec.fs" then ()
        elif Set.contains f shellBareDynAllowlist then ()
        else
            let path = "src/Shell/" + f
            let code = requireFile path |> nonCommentCode
            check ("arch: " + path + " must not Dyn.str ")
                (not (code.Contains "Dyn.str "))
            check ("arch: " + path + " must not Dyn.get ")
                (not (code.Contains "Dyn.get "))

let mustUseCodecHelper () = shellNonCodecMustUseDynFieldHelpers ()

let muxFileReadWrapperReturnsDisabled () =
    let wrappers = requireFile "src/Mux/Wrappers.fs" |> nonCommentCode
    let review = requireFile "src/Mux/WrappersReview.fs" |> nonCommentCode
    check "arch: Mux Wrappers calls mkFileReadCapture"
        (wrappers.Contains "mkFileReadCapture")
    check "arch: Mux WrappersReview defines mkFileReadCapture"
        (review.Contains "let mkFileReadCapture")
    check "arch: Mux file_read wrapper returns disabled"
        (review.Contains "disabled")

let opencodeHookExecuteUsesHookArgsHelpers () =
    let code = requireFile "src/Opencode/HookExecute.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/OpencodeHookInputCodec.fs" |> nonCommentCode
    check "arch: OpencodeHookInputCodec defines argsFromHookOutput"
        (codec.Contains "let argsFromHookOutput")
    check "arch: OpencodeHookInputCodec defines setHookArgs"
        (codec.Contains "let setHookArgs")
    check "arch: OpencodeHookInputCodec defines setHookError"
        (codec.Contains "let setHookError")
    check "arch: Opencode HookExecute uses argsFromHookOutput"
        (code.Contains "argsFromHookOutput")
    check "arch: Opencode HookExecute uses setHookArgs"
        (code.Contains "setHookArgs")
    check "arch: Opencode HookExecute uses setHookError"
        (code.Contains "setHookError")
    check "arch: Opencode HookExecute must not Dyn.get output args"
        (not (code.Contains "Dyn.get output \"args\""))
    check "arch: Opencode HookExecute must not setKey output args"
        (not (code.Contains "setKey output \"args\""))
    check "arch: Opencode HookExecute must not setKey output error"
        (not (code.Contains "setKey output \"error\""))

let opencodeCommandHooksUsesPartsWriter () =
    let code = requireFile "src/Opencode/CommandHooks.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/OpencodeHookInputCodec.fs" |> nonCommentCode
    check "arch: OpencodeHookInputCodec defines setHookParts"
        (codec.Contains "let setHookParts")
    check "arch: Opencode CommandHooks uses setHookParts"
        (code.Contains "setHookParts")
    check "arch: Opencode CommandHooks must not setKey output parts"
        (not (code.Contains "setKey output \"parts\""))

let muxHookOutputUsesMuxHookInputCodec () =
    let plugin = requireFile "src/Mux/Plugin.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/MuxHookInputCodec.fs" |> nonCommentCode
    check "arch: MuxHookInputCodec defines argsFromHookOutputMux"
        (codec.Contains "let argsFromHookOutputMux")
    check "arch: MuxHookInputCodec defines setHookArgsMux"
        (codec.Contains "let setHookArgsMux")
    check "arch: MuxHookInputCodec defines setHookErrorMux"
        (codec.Contains "let setHookErrorMux")
    check "arch: MuxHookInputCodec defines hookOutputErrorOptMux"
        (codec.Contains "let hookOutputErrorOptMux")
    check "arch: Mux Plugin must not Dyn.get output args"
        (not (plugin.Contains "Dyn.get output \"args\""))
    check "arch: Mux Plugin must not setKey output args"
        (not (plugin.Contains "setKey output \"args\""))
    check "arch: Mux Plugin must not setKey output error"
        (not (plugin.Contains "setKey output \"error\""))
    check "arch: Mux Plugin must not Dyn.str output error"
        (not (plugin.Contains "Dyn.str output \"error\""))