module Wanxiangshu.Tests.ArchitectureTestsSubagentToolExec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let muxSubagentToolsUsesToolArgsDecode () =
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let dispatcher = requireFile "src/Shell/SubagentDispatcher.fs" |> nonCommentCode
    check "arch: Mux SubagentTools opens MuxSubagentToolExecute" (mux.Contains "MuxSubagentToolExecute")
    check "arch: Mux SubagentTools uses executeMuxSubagentTool" (mux.Contains "executeMuxSubagentTool")
    check "arch: SubagentDispatcher uses decodeToolInvocation" (dispatcher.Contains "decodeToolInvocation")
    check "arch: SubagentDispatcher uses wireDecodeFailure on decode errors" (dispatcher.Contains "wireDecodeFailure")

    check
        "arch: Mux SubagentTools must not parallelPromptsFromIntents"
        (not (mux.Contains "parallelPromptsFromIntents"))

    check "arch: Mux SubagentTools must not buildPromptsFor" (not (mux.Contains "buildPromptsFor"))

let opencodeSubagentToolsUsesToolArgsDecode () =
    let code = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    let dispatcher = requireFile "src/Shell/SubagentDispatcher.fs" |> nonCommentCode
    check "arch: Opencode SubagentTools uses dispatch" (code.Contains "dispatch")
    check "arch: Opencode SubagentTools implements IHostAdapter" (code.Contains "IHostAdapter")
    check "arch: Opencode SubagentTools must not decodeIntentsField" (not (code.Contains "decodeIntentsField"))
    check "arch: Opencode SubagentTools must not decodeMeditatorArgs" (not (code.Contains "decodeMeditatorArgs"))
    check "arch: SubagentDispatcher uses decodeToolInvocation" (dispatcher.Contains "decodeToolInvocation")
    check "arch: SubagentDispatcher uses wireDecodeFailure on decode errors" (dispatcher.Contains "wireDecodeFailure")

let subagentToolsUseSubagentSpawn () =
    let spawn = requireFile "src/Shell/SubagentSpawn.fs" |> nonCommentCode
    check "arch: SubagentSpawn defines runParallelSpawns" (spawn.Contains "let runParallelSpawns")
    check "arch: SubagentSpawn defines runParallelSpawnsWithAbort" (spawn.Contains "let runParallelSpawnsWithAbort")
    let opencode = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    check "arch: Opencode SubagentTools uses dispatch" (opencode.Contains "dispatch")

    check
        "arch: Opencode SubagentTools must not inline parallel Promise.all joinReports"
        (not (opencode.Contains "|> Promise.all"))

    check
        "arch: Opencode SubagentTools must not call joinReports for parallel coder/investigator"
        (not (opencode.Contains "joinReports"))

    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let dispatcher = requireFile "src/Shell/SubagentDispatcher.fs" |> nonCommentCode
    check "arch: SubagentDispatcher uses runParallelSpawns" (dispatcher.Contains "runParallelSpawns")

    check
        "arch: Mux SubagentTools must not inline AbortController parallel spawn"
        (not (mux.Contains "AbortController"))

    check
        "arch: Mux SubagentTools must not inline parallel Promise.all joinReports"
        (not (mux.Contains "|> Promise.all"))

    check "arch: Mux SubagentTools must not call joinReports in bindParallel" (not (mux.Contains "joinReports"))

    check
        "arch: SubagentDispatcher must not inline AbortController parallel spawn"
        (not (dispatcher.Contains "AbortController"))

let executeMuxSubagentToolUsesSpawnRoleOnly () =
    let shell = requireFile "src/Shell/MuxSubagentToolExecute.fs" |> nonCommentCode
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    check "arch: executeMuxSubagentTool uses spawn.Role for tool name" (shell.Contains "let toolName = spawn.Role")

    check
        "arch: executeMuxSubagentTool signature must not take toolName parameter"
        (not (
            System.Text.RegularExpressions
                .Regex(@"let\s+executeMuxSubagentTool[\s\S]{0,400}\(toolName:\s*string\)")
                .IsMatch
                shell
        ))

    check
        "arch: Mux SubagentTools calls executeMuxSubagentTool without toolName arg"
        (mux.Contains "executeMuxSubagentTool runMuxSubagent deps (spawnFor")

    check
        "arch: Mux SubagentTools must not pass role as extra arg after args"
        (not (
            mux.Contains
                "executeMuxSubagentTool runMuxSubagent deps (spawnFor deps toolNames agentId title aiSettingsAgentId role) role args config"
        ))

let subagentToolExecuteEmptyBatchGuard () =
    let dispatcher = requireFile "src/Shell/SubagentDispatcher.fs" |> nonCommentCode
    let copy = requireFile "src/Kernel/ToolCopy.fs" |> nonCommentCode
    check "arch: ToolCopy defines subagentIntentsMustBeNonEmpty" (copy.Contains "let subagentIntentsMustBeNonEmpty")

    check
        "arch: SubagentDispatcher calls subagentIntentsMustBeNonEmpty"
        (dispatcher.Contains "subagentIntentsMustBeNonEmpty")
