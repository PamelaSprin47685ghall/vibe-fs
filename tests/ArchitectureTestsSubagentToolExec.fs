module VibeFs.Tests.ArchitectureTestsSubagentToolExec

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.ArchitectureTestsSupport

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
    let shell = requireFile "src/Shell/SubagentToolExecute.fs" |> nonCommentCode
    let muxShell = requireFile "src/Shell/MuxSubagentToolExecute.fs" |> nonCommentCode
    let copy = requireFile "src/Kernel/ToolCopy.fs" |> nonCommentCode
    check "arch: ToolCopy defines subagentIntentsMustBeNonEmpty"
        (copy.Contains "let subagentIntentsMustBeNonEmpty")
    check "arch: SubagentToolExecute calls subagentIntentsMustBeNonEmpty"
        (shell.Contains "subagentIntentsMustBeNonEmpty")
    check "arch: MuxSubagentToolExecute calls subagentIntentsMustBeNonEmpty"
        (muxShell.Contains "subagentIntentsMustBeNonEmpty")