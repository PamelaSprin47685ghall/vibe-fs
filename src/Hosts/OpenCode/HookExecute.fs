module Wanxiangshu.Hosts.Opencode.HookExecute

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Runtime.TreeSitterFormat
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Hosts.Opencode.AgentConfig
open Wanxiangshu.Hosts.Opencode.HookSchema
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.TreeSitterShell
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.PatchToolsCodec
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Runtime.LivelockGuard
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ToolHookRuntime

open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.ToolSequenceThrottle

/// Shared per-process tool sequence throttle for PTY read delay.
let private toolSequenceThrottle = ToolSequenceThrottle()

/// Apply PTY read throttle before executing the tool.
let private applyPtyReadThrottle (tool: string) (nextArgs: obj) (sessionID: string) : JS.Promise<unit> =
    promise {
        let terminalId =
            if tool = "pty_read" then
                let id = Dyn.str nextArgs "id"
                if id = "" then None else Some id
            else
                None

        do! toolSequenceThrottle.BeforeExecution(sessionID, tool, terminalId)
    }

let private rewriteMimocodeApplyPatchArgsForExecute (output: obj) (input: obj) (args: obj) : unit =
    if toolNameFromHookInput input <> "apply_patch" then
        ()
    else
        match decodeApplyPatchFields args with
        | Ok fields -> setHookArgs output (createObj [ "patchText", box fields.PatchText ])
        | Error e -> setHookError output (wireEncodeToolError "apply_patch" e)

let private appendSyntaxDiagnostics (directory: string) (input: obj) (output: obj) : JS.Promise<unit> =
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

let toolExecuteBeforeFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let tool = toolNameFromHookInput input
        let inputArgs = argsFromHookInput input
        let outputArgs = argsFromHookOutput output

        if
            (isNull inputArgs || Dyn.isNullish inputArgs)
            && (isNull outputArgs || Dyn.isNullish outputArgs)
        then
            raise (System.Exception("Tool validation error: arguments are null"))

        let rawArgs = resolveHookExecuteArgs input output

        if host = Mimocode && tool = "apply_patch" then
            rewriteMimocodeApplyPatchArgsForExecute output input rawArgs

        let args = resolveHookExecuteArgs input output

        if isNull args || Dyn.isNullish args then
            raise (System.Exception("Tool validation error: resolved arguments are null"))

        let inputArgs = argsFromHookInput input

        // Host runtimes may expose a distinct output rewriter object while
        // retaining the original input args reference for the real execute
        // call.  Transfer controls to the gateway's execution object, then
        // remove them from the original reference too.
        if
            not (Dyn.isNullish inputArgs)
            && Dyn.typeIs inputArgs "object"
            && Dyn.typeIs args "object"
        then
            for k in [| "warn_tdd"; "warn"; "warn_reuse" |] do
                if Dyn.has inputArgs k then
                    args?(k) <- inputArgs?(k)

            if not (obj.ReferenceEquals(inputArgs, args)) then
                for k in [| "warn_tdd"; "warn"; "warn_reuse" |] do
                    Dyn.deleteKey inputArgs k

        match ToolHookRuntime.executeBeforeGateway tool args with
        | Result.Error e ->
            setHookError output e
            raise (System.Exception("Tool validation error: " + e))
        | Result.Ok(nextArgs, env) ->
            setHookArgs output nextArgs

            let sessionID = ToolHookRuntime.tryExtractSessionId input |> Option.defaultValue ""

            let toolCallID =
                ToolHookRuntime.tryExtractToolCallId input |> Option.defaultValue ""

            ToolHookRuntime.saveCompliance sessionID toolCallID env

            do! applyPtyReadThrottle tool nextArgs sessionID

            HookSchema.setUiLabel args tool
            HookSchema.setUiLabel nextArgs tool
    }

let toolExecuteBefore (input: obj) (output: obj) : JS.Promise<unit> =
    toolExecuteBeforeFor opencode input output

/// Collect todo-write violations for a tool-execute-after hook call.
/// Returns (isDecodeError, violations). On decode error the hook output
/// error string is set as a side-effect.
let private collectTodoWriteViolations (host: Host) (output: obj) (input: obj) (decodedArgs: obj) : bool * string list =
    if
        toolNameFromHookInput input <> todoWriteToolName host
        || Dyn.isNullish decodedArgs
    then
        false, []
    else
        match Wanxiangshu.Runtime.WorkBacklogToolsCodec.decodeTodoWriteArgs (host = Mimocode) decodedArgs with
        | Ok(_, viols) -> false, viols
        | Error err ->
            let errStr = $"DECODE_FAILED: %A{err}"
            setHookError output errStr
            true, []

/// Evaluate the compliance entry for this tool call and apply violations and
/// warn-field restoration.
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

        // Restore warn fields so the LLM can see what it submitted.
        if not (Dyn.isNullish decodedArgs) then
            ToolHookRuntime.restoreWarnToArgs decodedArgs env

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

/// Core hook-after logic: syntax diagnostics, compliance/violation processing,
/// and network-error / livelock-guard post-processing.
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
