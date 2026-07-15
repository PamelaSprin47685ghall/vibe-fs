module Wanxiangshu.Opencode.HookExecute

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Opencode.AgentConfig
open Wanxiangshu.Opencode.HookSchema
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.TreeSitterShell
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Shell.PatchToolsCodec
open Wanxiangshu.Shell.ToolExecute
open Wanxiangshu.Shell.LivelockGuard
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.ToolHookRuntime

open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.Dyn

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
                if TreeSitterKernel.hasSyntaxInOutput s then
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


            HookSchema.setUiLabel args tool
            HookSchema.setUiLabel nextArgs tool
    }

let toolExecuteBefore (input: obj) (output: obj) : JS.Promise<unit> =
    toolExecuteBeforeFor opencode input output

let toolExecuteAfterFor
    (host: Host)
    (pluginDirectory: string)
    (lifecycleObserver: Wanxiangshu.Opencode.SessionLifecycleObserver.SessionLifecycleObserver)
    (registry: ChildAgentRegistry)
    (scope: RuntimeScope)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        do! appendSyntaxDiagnostics pluginDirectory input output
        let tool = toolNameFromHookInput input

        let sessionID =
            Id.sessionIdValue (fromOpencode input pluginDirectory).Execution.SessionId

        let originalOutput = hookOutputText output

        let decodedArgs = argsFromHookInput input


        let todoViolationsResult =
            if tool = todoWriteToolName host && not (Dyn.isNullish decodedArgs) then
                match Wanxiangshu.Shell.WorkBacklogToolsCodec.decodeTodoWriteArgs (host = Mimocode) decodedArgs with
                | Ok(_, viols) -> Ok viols
                | Error err -> Error err
            else
                Ok []

        let isDecodeError, todoViolations =
            match todoViolationsResult with
            | Ok viols -> false, viols
            | Error err ->
                let errStr = $"DECODE_FAILED: %A{err}"
                setHookError output errStr
                true, []

        let currentOutput = hookOutputText output

        let isError =
            isDecodeError
            || hookOutputError output <> ""
            || isNetworkErrorText currentOutput

        let toolCallID =
            ToolHookRuntime.tryExtractToolCallId input |> Option.defaultValue ""

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
                setHookOutputString output criticism

            ToolHookRuntime.removeCompliance sessionID toolCallID
        | None ->
            let status =
                if isError then
                    ToolHookRuntime.ExecutionStatus.Failure
                else
                    ToolHookRuntime.ExecutionStatus.Success

            if not todoViolations.IsEmpty then
                let criticism = ToolHookRuntime.appendCriticism currentOutput todoViolations status
                setHookOutputString output criticism

        let argsJson = LivelockGuard.cleanArgsJson (argsFromHookInput input)

        let finalOutput = hookOutputText output

        if isNetworkErrorText finalOutput then
            setHookError output "network connection lost"

        if hookOutputError output = "" then
            if LivelockGuard.check scope sessionID tool argsJson currentOutput then
                setHookError output "livelock guard: repeated identical tool call with identical result"

        do! lifecycleObserver.handleToolExecuteAfter input output
    }
