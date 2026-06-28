module Wanxiangshu.Opencode.ExecutorTool

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.HostTools

open Wanxiangshu.Kernel.Subagent
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Opencode.ToolSchema
open Wanxiangshu.Opencode.SessionIo
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.ExecutorToolsCodec
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.ToolExecute
open Wanxiangshu.Shell.SubagentToolExecute
open Wanxiangshu.Shell.FallbackRuntimeState

[<Global("Buffer")>]
let private nodeBuffer : obj = jsNative
let private byteLength (s: string) : int = nodeBuffer?byteLength(s, "utf-8")
let private resolveStr (text: string) : JS.Promise<string> = Promise.lift text

let executorTool (host: Host) (registry: ChildAgentRegistry) (ctx: obj) (sessionScope: RuntimeScope) (fallbackRuntime: FallbackRuntimeState) : obj =
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
                                let! result = Wanxiangshu.Shell.Executor.execute options sessionID
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
                                            (runSubagentWithCleanup fallbackRuntime registry client "executor" "Executor summary" prompt
                                                runtime.Execution.Directory sessionID context)
                                    let formatted = formatToolResponse result (Some summary)
                                    return prependSafetyWarningForExecution formatted options
                            }))
