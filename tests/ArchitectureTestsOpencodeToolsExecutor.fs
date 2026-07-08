module Wanxiangshu.Tests.ArchitectureTestsOpencodeToolsExecutor

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let opencodeExecutorUsesToolCopy () =
    let code = requireFile "src/Opencode/ExecutorTool.fs" |> nonCommentCode
    check "arch: Opencode ExecutorTool opens ToolCopy" (code.Contains "ToolCopy")
    check "arch: Opencode ExecutorTool uses executorRequiresSession" (code.Contains "executorRequiresSession")

    check
        "arch: Opencode ExecutorTool must not inline expected shell, python, or javascript"
        (not (code.Contains "expected shell, python, or javascript"))

let opencodeExecutorUsesExecutorToolsCodec () =
    let code = requireFile "src/Opencode/ExecutorTool.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/ExecutorToolsCodec.fs" |> nonCommentCode
    check "arch: ExecutorToolsCodec defines decodeExecutorArgs" (codec.Contains "let decodeExecutorArgs")
    check "arch: ExecutorToolsCodec defines toExecuteOptions" (codec.Contains "let toExecuteOptions")
    check "arch: Opencode ExecutorTool opens ExecutorToolsCodec" (code.Contains "ExecutorToolsCodec")
    check "arch: Opencode ExecutorTool uses decodeExecutorArgs" (code.Contains "decodeExecutorArgs")

    check
        "arch: Opencode ExecutorTool uses wireDomainFailure for executor decode"
        (code.Contains "wireDomainFailure \"Executor\"")

    check "arch: Opencode ExecutorTool uses toExecuteOptions" (code.Contains "toExecuteOptions")
    check "arch: Opencode ExecutorTool must not Dyn.str args language" (not (code.Contains "Dyn.str args \"language\""))
    check "arch: Opencode ExecutorTool must not Dyn.str args program" (not (code.Contains "Dyn.str args \"program\""))
    check "arch: Opencode ExecutorTool must not Dyn.str args mode" (not (code.Contains "Dyn.str args \"mode\""))

    check
        "arch: Opencode ExecutorTool must not Dyn.str args timeout_type"
        (not (code.Contains "Dyn.str args \"timeout_type\""))

let opencodeExecutorUsesFromOpencode () =
    let code = requireFile "src/Opencode/ExecutorTool.fs" |> nonCommentCode
    check "arch: Opencode ExecutorTool opens ToolRuntimeContext" (code.Contains "ToolRuntimeContext")
    check "arch: Opencode ExecutorTool uses fromOpencode" (code.Contains "fromOpencode")

    check
        "arch: Opencode ExecutorTool uses runtime.Execution for session and directory"
        ((code.Contains "runtime.Execution.SessionId")
         && (code.Contains "runtime.Execution.Directory"))

    check "arch: Opencode ExecutorTool uses executorRequiresSession" (code.Contains "executorRequiresSession")
    check "arch: Opencode ExecutorTool must not extractToolContext" (not (code.Contains "extractToolContext"))
    check "arch: Opencode ExecutorTool must not Dyn.str tc sessionID" (not (code.Contains "Dyn.str tc \"sessionID\""))
    check "arch: Opencode ExecutorTool must not Dyn.str tc directory" (not (code.Contains "Dyn.str tc \"directory\""))
    check "arch: Opencode ExecutorTool uses pluginDirectoryFromCtx" (code.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode ExecutorTool must not Dyn.str ctx directory" (not (code.Contains "Dyn.str ctx \"directory\""))
