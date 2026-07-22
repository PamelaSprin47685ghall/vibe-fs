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
open Wanxiangshu.Runtime.ToolOutputInfo

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Hosts.Opencode.SessionIo
open Wanxiangshu.Hosts.Opencode.SubagentIoRun
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.ExecutorToolsCodec
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Runtime.SubagentDispatcher
open Wanxiangshu.Runtime.SubagentBatchSpawn
open Wanxiangshu.Runtime.Fallback.RuntimeStore

[<Global("Buffer")>]
let private nodeBuffer: obj = jsNative

let private byteLength (s: string) : int = nodeBuffer?byteLength (s, "utf-8")
let private truncateToBytes (s: string) (n: int) : string = Wanxiangshu.Runtime.SubagentPrompts.truncateUtf8ByBytes s n
let private resolveStr (text: string) : JS.Promise<string> = Promise.lift text

/// Schema for the executor tool parameters.
let private buildExecutorToolDef () : obj =
    createObj
        [ "language", enumOptWithDefault [| "shell"; "python"; "javascript" |] "shell" Params.executorLanguage
          "command", strReq Params.executorCommand
          "dependencies", strArrayOpt Params.executorDeps
          "timeout_type", enumReq [| "short"; "long" |] Params.executorTimeout
          "what_to_summarize", strReq Params.executorWhatToSummarize
          "max_bytes", numReq Params.executorMaxBytes
          "follow-tdd-and-kolmogorov-principles", warnTddParam
          "impossible-via-other-tools", warnImpossibleViaOtherToolsParam ]

/// Build the subagent prompt for executor output summarisation from ExecuteResult.
let private buildExecutorSummaryPrompt (host: Host) (result: ExecuteResult) (options: ExecuteOptions) : string =
    let evidence = buildExecutorEvidence byteLength truncateToBytes result
    let langStr = languageToString options.language

    let timeoutKind =
        match options.timeoutType with
        | Short -> Wanxiangshu.Kernel.Prompt.TimeoutKind.Short
        | Long -> Wanxiangshu.Kernel.Prompt.TimeoutKind.Long

    formatPrompt
        host
        (ExecutorSummary(evidence, langStr, options.command, options.dependencies, timeoutKind, options.whatToSummarize))
    |> List.head

/// Summarise output via subagent if needed, or format output directly.
let private summarizeOrFormat
    (host: Host)
    (registry: ChildAgentRegistry)
    (fallbackRuntime: FallbackRuntimeStore)
    (directory: string)
    (options: ExecuteOptions)
    (sessionID: string)
    (client: obj)
    (context: obj)
    (result: ExecuteResult)
    : JS.Promise<string> =
    promise {
        let output = outputFromResult result

        if not (shouldSummarize byteLength options.maxBytes output) then
            let msg = formatToolResponse result None
            return render (prependSafetyWarningForExecution msg options)
        else
            let prompt = buildExecutorSummaryPrompt host result options

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
                        directory
                        sessionID
                        context)

            let msg = formatToolResponse result (Some summary)
            return render (prependSafetyWarningForExecution msg options)
    }

/// Execute the executor tool body: decode args, resolve client, run command inside EnqueueExecutor,
/// optionally summarise via subagent outside lock, return formatted response string.
let private executeExecutorTool
    (host: Host)
    (registry: ChildAgentRegistry)
    (ctx: obj)
    (sessionScope: RuntimeScope)
    (fallbackRuntime: FallbackRuntimeStore)
    (args: obj)
    (context: obj)
    : JS.Promise<string> =
    match decodeExecutorArgs args with
    | Error e -> resolveStr (wireDomainFailure "Executor" e)
    | Ok decoded ->
        match getClientFromPluginCtx ctx with
        | Error e -> resolveStr (wireEncodeToolError "OpencodeClient" e)
        | Ok client ->
            promise {
                let runtime = fromOpencode context (pluginDirectoryFromCtx ctx)
                let sessionID = Id.sessionIdValue runtime.Execution.SessionId

                if sessionID = "" then
                    return! resolveStr executorRequiresSession
                else
                    let options = toExecuteOptions (Some runtime.Execution.Directory) decoded

                    let! result =
                        sessionScope.EnqueueExecutor(
                            sessionID,
                            fun () -> Wanxiangshu.Runtime.Executor.execute sessionScope options sessionID
                        )

                    return!
                        summarizeOrFormat
                            host
                            registry
                            fallbackRuntime
                            runtime.Execution.Directory
                            options
                            sessionID
                            client
                            context
                            result
            }

let executorTool
    (host: Host)
    (registry: ChildAgentRegistry)
    (ctx: obj)
    (sessionScope: RuntimeScope)
    (fallbackRuntime: FallbackRuntimeStore)
    : obj =
    define executor (buildExecutorToolDef ()) (executeExecutorTool host registry ctx sessionScope fallbackRuntime)
