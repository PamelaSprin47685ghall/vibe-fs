module Wanxiangshu.Tests.ArchitectureTestsRuntimeSession

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let private sessionIoSubagentCode () =
    (requireFile "src/Opencode/SessionIo.fs" |> nonCommentCode)
    + (requireFile "src/Opencode/SessionIoSubagent.fs" |> nonCommentCode)
    + (requireFile "src/Opencode/SubagentSpawn.fs" |> nonCommentCode)
    + (requireFile "src/Opencode/SubagentIo.fs" |> nonCommentCode)
    + (requireFile "src/Opencode/SubagentTypes.fs" |> nonCommentCode)

let sessionIoUsesOpencodeClientCodec () =
    let code = sessionIoSubagentCode ()
    check "arch: SessionIo opens OpencodeClientCodec" (code.Contains "OpencodeClientCodec")
    check "arch: SessionIo uses getSessionApiFromClient" (code.Contains "getSessionApiFromClient")
    check "arch: SessionIo must not Dyn.get client session" (not (code.Contains "Dyn.get client \"session\""))
    check "arch: SessionIo opens ToolResult" (code.Contains "ToolResult")

    check
        "arch: SessionIo promptWithAbort client failure uses wireEncodeToolError OpencodeClient"
        (code.Contains "wireEncodeToolError \"OpencodeClient\"")

    check "arch: SessionIo must not formatDomainError" (not (code.Contains "formatDomainError"))

let sessionIoUsesSubagentResultPath () =
    let sessionIo = sessionIoSubagentCode ()
    let spawn = requireFile "src/Shell/SessionIoSpawn.fs" |> nonCommentCode
    check "arch: SessionIo defines runSubagentCoreResult" (sessionIo.Contains "let runSubagentCoreResult")

    check
        "arch: SessionIo spawn path uses Result<string, DomainError>"
        (sessionIo.Contains "Result<string, DomainError>")

    check "arch: SessionIoSpawn defines formatSubagentReport" (spawn.Contains "let formatSubagentReport")

    check
        "arch: SessionIo runSubagent is Result public API"
        (sessionIo.Contains "let runSubagent"
         && sessionIo.Contains "JS.Promise<Result<string, DomainError>>")

let private opencodeClientSessionDynCtxRe =
    System.Text.RegularExpressions.Regex(@"Dyn\.get\s+ctx\s+""client""")

let private opencodeClientSessionDynClientRe =
    System.Text.RegularExpressions.Regex(@"Dyn\.get\s+client\s+""session""")

let opencodeNoDirectClientSessionDyn () =
    for f in fsFiles "src/Opencode" do
        if f <> "OpencodeClientCodec.fs" then
            let code = requireFile ("src/Opencode/" + f) |> nonCommentCode
            check ("arch: Opencode/" + f + " no Dyn.get ctx client") (not (opencodeClientSessionDynCtxRe.IsMatch code))

            check
                ("arch: Opencode/" + f + " no Dyn.get client session")
                (not (opencodeClientSessionDynClientRe.IsMatch code))

let sessionExecutorCreateForScope () =
    let code = requireFile "src/Shell/SessionExecutor.fs" |> nonCommentCode
    check "arch: SessionExecutor exposes createForScope" (code.Contains "createForScope")

let pluginInjectsSessionScopeForExecutor () =
    let muxCatalog = requireFile "src/Mux/PluginCatalog.fs" |> nonCommentCode
    let muxHost = requireFile "src/Mux/HostTools.fs" |> nonCommentCode

    check
        "arch: Mux Plugin createToolCatalog passes sessionScope to executorTool"
        (muxCatalog.Contains "executorTool deps toolNames sessionScope")

    check
        "arch: Mux HostTools executor uses sessionScope.EnqueueExecutor"
        (muxHost.Contains "sessionScope.EnqueueExecutor")

    let pluginCore = requireFile "src/Opencode/PluginCoreServices.fs" |> nonCommentCode
    let tools = requireFile "src/Opencode/Tools.fs" |> nonCommentCode
    let executor = requireFile "src/Opencode/ExecutorTool.fs" |> nonCommentCode

    check
        "arch: Opencode PluginCore createTools passes scope"
        (pluginCore.Contains "createTools host childAgentRegistry finderCache ctx reviewStore scope fallbackRuntime")

    check
        "arch: Opencode Tools passes host and sessionScope to executorTool"
        (tools.Contains "executorTool host registry ctx sessionScope")

    check
        "arch: Opencode ExecutorTool uses sessionScope.EnqueueExecutor"
        (executor.Contains "sessionScope.EnqueueExecutor")

let runtimeScopeNoGetDefault () =
    let code = requireFile "src/Shell/RuntimeScope.fs" |> nonCommentCode
    check "arch: RuntimeScope must not define getDefault" (not (code.Contains "let getDefault"))
    check "arch: RuntimeScope must not call getDefault" (not (code.Contains "getDefault"))

let sessionExecutorNoModuleMutableQueues () =
    let code = requireFile "src/Shell/SessionExecutor.fs" |> nonCommentCode

    check
        "arch: SessionExecutor must not define module enqueuePerSession"
        (not (System.Text.RegularExpressions.Regex(@"let\s+enqueuePerSession\b").IsMatch code))

    check "arch: SessionExecutor must not call getDefault" (not (code.Contains "getDefault"))
    check "arch: SessionExecutor no module-level mutable queues" (not (code.Contains "mutable queues"))
    let scope = requireFile "src/Shell/RuntimeScope.fs" |> nonCommentCode
    check "arch: RuntimeScope holds sessionLocks map" (scope.Contains "sessionLocks")
    check "arch: RuntimeScope defines EnqueuePerSession" (scope.Contains "member _.EnqueuePerSession")
    check "arch: RuntimeScope defines ClearSessionQueues" (scope.Contains "member _.ClearSessionQueues")
