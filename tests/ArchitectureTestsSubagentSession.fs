module Wanxiangshu.Tests.ArchitectureTestsSubagentSession

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let sessionIoRunSubagentReturnsResult () =
    let sessionIo =
        (requireFile "src/Opencode/SessionIo.fs" |> nonCommentCode)
        + (requireFile "src/Opencode/SessionIoSubagent.fs" |> nonCommentCode)
        + (requireFile "src/Opencode/SubagentSpawn.fs" |> nonCommentCode)
        + (requireFile "src/Opencode/SubagentIo.fs" |> nonCommentCode)
        + (requireFile "src/Opencode/SubagentTypes.fs" |> nonCommentCode)
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
    check "arch: CommandHooks syncs review from event log"
        (code.Contains "syncReviewFromEventLog")
    check "arch: ToolCopy defines preReviewCouldNotComplete"
        (copy.Contains "let preReviewCouldNotComplete")
    check "arch: CommandHooks must not inline With-Review Mode is already active"
        (not (code.Contains "With-Review Mode is already active. Submit your work via submit_review."))

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
