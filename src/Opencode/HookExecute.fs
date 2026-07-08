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
                        |> Promise.all

                    let formatted = diagnostics |> Array.choose id |> String.concat "\n"

                    if formatted <> "" then
                        setHookOutputString output (addSyntax s formatted)
    }

let toolExecuteBeforeFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let args = resolveHookExecuteArgs input output
        let tool = toolNameFromHookInput input

        ToolHookRuntime.filterAmendFromArgs args |> ignore

        ToolHookRuntime.sanitizeNullArgs tool args

        match ToolHookRuntime.requireWarnTddOnArgs tool args with
        | Result.Error e -> setHookError output e
        | Result.Ok() -> ()

        match ToolHookRuntime.requireWarnOnArgs tool args with
        | Result.Error e -> setHookError output e
        | Result.Ok() -> ()

        HookSchema.setUiLabel args tool

        if host = Mimocode then
            rewriteMimocodeApplyPatchArgsForExecute output input args
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
        let argsJson = JS.JSON.stringify (argsFromHookInput input)

        if isNetworkErrorText originalOutput then
            setHookError output "network connection lost"

        if hookOutputError output = "" then
            if LivelockGuard.check scope sessionID tool argsJson originalOutput then
                setHookError output "livelock guard: repeated identical tool call with identical result"

        do! lifecycleObserver.handleToolExecuteAfter input output
    }
