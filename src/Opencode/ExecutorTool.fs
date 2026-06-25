module VibeFs.Opencode.ExecutorTool

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.Domain
open VibeFs.Kernel.Executor
open VibeFs.Kernel.HostTools

open VibeFs.Kernel.Subagent
open VibeFs.Kernel.ToolCatalog
open VibeFs.Kernel.ToolCopy
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.SessionIo
open VibeFs.Kernel.ToolResult
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.ExecutorToolsCodec
open VibeFs.Shell.RuntimeScope
open VibeFs.Shell.ToolRuntimeContext
open VibeFs.Shell.Dyn
open VibeFs.Shell.OpencodeClientCodec
open VibeFs.Shell.ToolExecute
open VibeFs.Shell.SubagentToolExecute

[<Global("Buffer")>]
let private nodeBuffer : obj = jsNative
let private byteLength (s: string) : int = nodeBuffer?byteLength(s, "utf-8")
let private resolveStr (text: string) : JS.Promise<string> = Promise.lift text

let executorTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (sessionScope: RuntimeScope) : obj =
    define executor
        (box {|
            language = enumReq [| "shell"; "python"; "javascript" |] Params.executorLanguage
            program = strReq Params.executorProgram
            dependencies = strArrayOpt Params.executorDeps
            timeout_type = enumReq [| "short"; "long"; "last-resort" |] Params.executorTimeout
            mode = enumReq [| "ro"; "rw" |] Params.executorMode
        |})
        (fun args context ->
            match decodeExecutorArgs args with
            | Error e -> resolveStr (wireDomainFailure "Executor" e)
            | Ok decoded ->
                match getClientFromPluginCtx ctx with
                | Error e -> resolveStr (wireEncodeToolError "OpencodeClient" e)
                | Ok client ->
                    let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)
                    let sessionID = Id.sessionIdValue runtime.Execution.SessionId
                    if sessionID = "" then resolveStr executorRequiresSession
                    else
                        sessionScope.EnqueuePerSession(sessionID, fun () ->
                            let options = toExecuteOptions (Some runtime.Execution.Directory) decoded
                            promise {
                                let! result = VibeFs.Shell.Executor.execute options sessionID
                                let output = outputFromResult result
                                if not (shouldSummarize byteLength output) then
                                    let formatted = formatToolResponse result None
                                    return prependSafetyWarningForExecution formatted options
                                else
                                    let langStr = languageToString options.language
                                    let timeoutStr = timeoutToString options.timeoutType
                                    let prompt = formatPrompt host (ExecutorSummary(output, langStr, options.program, options.dependencies, timeoutStr, options.mode)) |> List.head
                                    let! summary =
                                        resolveSubagentPromise "executor"
                                            (runSubagentWithCleanup registry client "executor" "Executor summary" prompt
                                                runtime.Execution.Directory sessionID context)
                                    let formatted = formatToolResponse result (Some summary)
                                    return prependSafetyWarningForExecution formatted options
                            }))
