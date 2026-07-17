module Wanxiangshu.Hosts.Opencode.ExecutorTool

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Kernel.HostTools

open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Hosts.Opencode.SessionIo
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.ExecutorToolsCodec
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Runtime.SubagentDispatcher
open Wanxiangshu.Runtime.Fallback.RuntimeStore

[<Global("Buffer")>]
let private nodeBuffer: obj = jsNative

let private byteLength (s: string) : int = nodeBuffer?byteLength (s, "utf-8")
let private resolveStr (text: string) : JS.Promise<string> = Promise.lift text

// ARCHITECTURE_EXEMPT: split this 77-line function later
let executorTool
    (host: Host)
    (registry: ChildAgentRegistry)
    (ctx: obj)
    (sessionScope: RuntimeScope)
    (fallbackRuntime: FallbackRuntimeStore)
    : obj =
    define
        executor
        (box
            {| language = enumOptWithDefault [| "shell"; "python"; "javascript" |] "shell" Params.executorLanguage
               command = strReq Params.executorCommand
               dependencies = strArrayOpt Params.executorDeps
               timeout_type = enumReq [| "short"; "long" |] Params.executorTimeout
               mode = enumReq [| "ro"; "rw" |] Params.executorMode
               what_to_summarize = strReq Params.executorWhatToSummarize
               max_bytes = numReq Params.executorMaxBytes |})
        (fun args context ->
            match decodeExecutorArgs args with
            | Error e -> resolveStr (wireDomainFailure "Executor" e)
            | Ok decoded ->
                match getClientFromPluginCtx ctx with
                | Error e -> resolveStr (wireEncodeToolError "OpencodeClient" e)
                | Ok client ->
                    let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)
                    let sessionID = Id.sessionIdValue runtime.Execution.SessionId

                    if sessionID = "" then
                        resolveStr executorRequiresSession
                    else
                        let options = toExecuteOptions (Some runtime.Execution.Directory) decoded

                        let runWork () =
                            promise {
                                let! result = Wanxiangshu.Runtime.Executor.execute options sessionID
                                let output = outputFromResult result

                                if not (shouldSummarize byteLength options.maxBytes output) then
                                    let formatted = formatToolResponse result None
                                    return prependSafetyWarningForExecution formatted options
                                else
                                    let langStr = languageToString options.language
                                    let timeoutStr = timeoutToString options.timeoutType

                                    let prompt =
                                        formatPrompt
                                            host
                                            (ExecutorSummary(
                                                output,
                                                langStr,
                                                options.command,
                                                options.dependencies,
                                                timeoutStr,
                                                options.mode,
                                                options.whatToSummarize
                                            ))
                                        |> List.head

                                    let! summary =
                                        resolveSubagentPromise
                                            "executor"
                                            (runSubagentWithCleanup
                                                fallbackRuntime
                                                registry
                                                client
                                                "executor"
                                                "Executor summary"
                                                prompt
                                                runtime.Execution.Directory
                                                sessionID
                                                context)

                                    let formatted = formatToolResponse result (Some summary)
                                    return prependSafetyWarningForExecution formatted options
                            }

                        sessionScope.EnqueueExecutor(sessionID, options.mode, runWork))
