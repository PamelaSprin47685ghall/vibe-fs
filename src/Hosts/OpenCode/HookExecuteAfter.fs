module Wanxiangshu.Hosts.Opencode.HookExecuteAfter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Runtime.TreeSitterFormat
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.ToolHookRuntime
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.ToolSequenceThrottle
open Wanxiangshu.Runtime.LivelockGuard
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.TreeSitterShell
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Hosts.Opencode.HookSchemaDecoration
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.PatchToolsCodec
open Wanxiangshu.Runtime.ToolExecute

let appendSyntaxDiagnostics (directory: string) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let tool = toolNameFromHookInput input

        if not (ToolExecute.isFileEditTool tool) then
            ()
        else
            match hookOutputString output with
            | None -> ()
            | Some s ->
                if TreeSitterFormat.hasSyntaxInOutput s then
                    ()
                else
                    let paths = extractFilePaths (argsFromHookInput input)

                    let! diagnostics =
                        paths
                        |> List.map (fun path -> readAndCheckSyntax path directory false)
                        |> List.toArray
                        |> Promise.all

                    let formatted = diagnostics |> Array.choose id |> String.concat "\n"

                    if formatted <> "" then
                        setHookOutputString output (addSyntax s formatted)
    }

let private collectTodoWriteViolations (host: Host) (output: obj) (input: obj) (decodedArgs: obj) : bool * string list =
    false, []

let private processHookResult
    (decodedArgs: obj)
    (currentOutput: string)
    (isError: bool)
    (toolCallID: string)
    (sessionID: string)
    (todoViolations: string list)
    (setOutputString: string -> unit)
    : unit =
    match ToolHookRuntime.tryGetCompliance sessionID toolCallID with
    | Some env ->
        let status =
            if env.Cancelled then
                ToolHookRuntime.ExecutionStatus.Cancelled
            elif isError then
                ToolHookRuntime.ExecutionStatus.Failure
            else
                ToolHookRuntime.ExecutionStatus.Success

        let allViolations = env.Violations @ todoViolations |> List.distinct

        if not allViolations.IsEmpty then
            let criticism = ToolHookRuntime.appendCriticism currentOutput allViolations status
            setOutputString criticism

        ToolHookRuntime.removeCompliance sessionID toolCallID
    | None ->
        let status =
            if isError then
                ToolHookRuntime.ExecutionStatus.Failure
            else
                ToolHookRuntime.ExecutionStatus.Success

        if not todoViolations.IsEmpty then
            let criticism = ToolHookRuntime.appendCriticism currentOutput todoViolations status
            setOutputString criticism

let private executeHookAfter
    (host: Host)
    (pluginDirectory: string)
    (scope: RuntimeScope)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        do! appendSyntaxDiagnostics pluginDirectory input output
        let decodedArgs = argsFromHookInput input

        let sessionID =
            Id.sessionIdValue (fromOpencode input pluginDirectory).Execution.SessionId

        let toolCallID =
            ToolHookRuntime.tryExtractToolCallId input |> Option.defaultValue ""

        let currentOutput = hookOutputText output

        let isDecodeError, todoViolations =
            collectTodoWriteViolations host output input decodedArgs

        let isError =
            isDecodeError
            || hookOutputError output <> ""
            || isNetworkErrorText currentOutput

        processHookResult
            decodedArgs
            currentOutput
            isError
            toolCallID
            sessionID
            todoViolations
            (setHookOutputString output)

        // Network-error and livelock-guard post-processing.
        let argsJson = LivelockGuard.cleanArgsJson (argsFromHookInput input)
        let finalOutput = hookOutputText output

        if isNetworkErrorText finalOutput then
            setHookError output "network connection lost"

        if hookOutputError output = "" then
            if LivelockGuard.check scope sessionID (toolNameFromHookInput input) argsJson finalOutput then
                setHookError output "livelock guard: repeated identical tool call with identical result"
    }

let toolExecuteAfterFor
    (host: Host)
    (pluginDirectory: string)
    (lifecycleObserver: Wanxiangshu.Hosts.Opencode.SessionLifecycleObserver.SessionLifecycleObserver)
    (registry: ChildAgentRegistry)
    (scope: RuntimeScope)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        do! executeHookAfter host pluginDirectory scope input output
        do! lifecycleObserver.handleToolExecuteAfter input output
    }
