module Wanxiangshu.Tests.ArchitectureTestsMuxToolCore

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let muxHostToolsFuzzyUsesToolCopy () =
    let code = requireMuxHostTools () |> nonCommentCode
    check "arch: Mux HostTools fuzzy must not use muxFuzzyFindRequiresWorkspaceId"
        (not (code.Contains "muxFuzzyFindRequiresWorkspaceId"))
    check "arch: Mux HostTools fuzzy must not use muxFuzzyGrepRequiresWorkspaceId"
        (not (code.Contains "muxFuzzyGrepRequiresWorkspaceId"))
    check "arch: Mux HostTools must not inline fuzzy_find requires workspaceId"
        (not (code.Contains "fuzzy_find requires workspaceId"))
    check "arch: Mux HostTools must not inline fuzzy_grep requires workspaceId"
        (not (code.Contains "fuzzy_grep requires workspaceId"))

let muxHostToolsFuzzyUsesFromMuxConfig () =
    let code = requireMuxHostTools () |> nonCommentCode
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
    let code = requireMuxHostTools () |> nonCommentCode
    check "arch: Mux HostTools opens FuzzyToolsCodec" (code.Contains "FuzzyToolsCodec")
    check "arch: Mux HostTools fuzzy_find uses decodeFuzzyFindArgs" (code.Contains "decodeFuzzyFindArgs")
    check "arch: Mux HostTools fuzzy_grep uses decodeFuzzyGrepArgs" (code.Contains "decodeFuzzyGrepArgs")
    check "arch: Mux HostTools fuzzy must not strField args pattern" (not (code.Contains "strField args \"pattern\""))
    check "arch: Mux HostTools fuzzy must not parseExcludeField args inline" (not (code.Contains "parseExcludeField args"))

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

let muxHostToolsExecutorUsesFromMuxConfig () =
    let code = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    check "arch: Mux HostTools executor opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Mux HostTools executor uses fromMuxConfig"
        (code.Contains "fromMuxConfig")
    check "arch: Mux HostTools executor uses wireEncodeToolError MuxConfig"
        (code.Contains "wireEncodeToolError \"MuxConfig\"")
    check "arch: Mux HostTools executor uses Execution.Directory"
        (code.Contains "runtime.Execution.Directory")
    check "arch: Mux HostTools executor uses Execution.SessionId"
        (code.Contains "runtime.Execution.SessionId")
    check "arch: Mux HostTools executor must not Dyn.str config cwd"
        (not (code.Contains "Dyn.str config \"cwd\""))

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

let webToolsUsesWebfetchCodec () =
    let web = requireFile "src/Mux/WebTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/WebToolsCodec.fs" |> nonCommentCode
    check "arch: WebToolsCodec defines decodeWebfetchArgs"
        (codec.Contains "let decodeWebfetchArgs")
    check "arch: Mux WebTools uses decodeWebfetchArgs"
        (web.Contains "decodeWebfetchArgs")
    check "arch: Mux WebTools webfetch must not inline strField args url"
        (not (web.Contains "strField args \"url\""))

let fuzzyToolsCodecExists () =
    let codec = requireFile "src/Shell/FuzzyToolsCodec.fs" |> nonCommentCode
    check "arch: FuzzyToolsCodec uses DynField strField" (codec.Contains "strField args")
    check "arch: FuzzyToolsCodec must not define local let strField" (not (codec.Contains "let strField"))
    check "arch: FuzzyToolsCodec returns FuzzyFindParams" (codec.Contains "FuzzyFindParams")
    check "arch: FuzzyToolsCodec returns FuzzyGrepParams" (codec.Contains "FuzzyGrepParams")

let dualHostFuzzyUsesFuzzyToolsCodec () =
    let codec = requireFile "src/Shell/FuzzyToolsCodec.fs" |> nonCommentCode
    let mux = requireMuxHostTools () |> nonCommentCode
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