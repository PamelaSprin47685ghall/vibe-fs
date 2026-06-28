module Wanxiangshu.Opencode.HookExecute

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Opencode.AgentConfig
open Wanxiangshu.Opencode.HookSchema
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.TreeSitterShell
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Shell.PatchToolsCodec
open Wanxiangshu.Shell.ToolExecute
open Wanxiangshu.Shell.LivelockGuard
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.Dyn

let private rewriteMimocodeApplyPatchArgsForExecute (output: obj) (input: obj) (args: obj) : unit =
    if toolNameFromHookInput input <> "apply_patch" then ()
    else
        match decodeApplyPatchFields args with
        | Result.Ok fields -> setHookArgs output (createObj [ "patchText", box fields.PatchText ])
        | Result.Error e -> setHookError output (wireEncodeToolError "apply_patch" e)

let private requireWarnTdd (tool: string) (args: obj) (output: obj) : unit =
    if not (WarnTdd.isModificationTool tool) then ()
    else
        let raw = Dyn.str args "warn_tdd"
        match WarnTdd.parseWarnTdd raw with
        | Some _ -> Dyn.deleteKey args "warn_tdd"
        | None -> setHookError output (wireDomainFailure tool (Domain.InvalidIntent(tool, "warn_tdd", "required — acknowledge TDD + Kolmolgorov discipline")))

let private requireWarn (tool: string) (args: obj) (output: obj) : unit =
    if not (WarnTdd.isWarnRequiredTool tool) then ()
    else
        let raw = Dyn.str args "warn"
        if WarnTdd.parseWarn raw then
            Dyn.deleteKey args "warn"
        else
            setHookError output (wireDomainFailure tool (Domain.InvalidIntent(tool, "warn", "required — acknowledge this task cannot be done with other tools")))

let toolExecuteBeforeFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let args = argsFromHookOutput output
        if Dyn.isNullish args then ()
        else
            let tool = toolNameFromHookInput input
            requireWarnTdd tool args output
            requireWarn tool args output
            setUiLabel args tool
            if host = Mimocode then
                rewriteMimocodeApplyPatchArgsForExecute output input args
    }

let toolExecuteBefore (input: obj) (output: obj) : JS.Promise<unit> =
    toolExecuteBeforeFor opencode input output

let private appendSyntaxDiagnostics (directory: string) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let tool = toolNameFromHookInput input
        if not (isFileEditTool tool) then ()
        else
            match hookOutputString output with
            | None -> ()
            | Some s ->
                if hasSyntaxInOutput s then ()
                else
                    let paths = extractFilePaths (argsFromHookInput input)
                    let! diagnostics =
                        paths
                        |> List.map (fun path -> readAndCheckSyntax path directory false)
                        |> Promise.all
                    let formatted =
                        diagnostics
                        |> Array.choose id
                        |> String.concat "\n"
                    if formatted <> "" then setHookOutputString output (addSyntax s formatted)
    }

let toolExecuteAfterFor (host: Host) (pluginDirectory: string) (lifecycleObserver: Wanxiangshu.Opencode.SessionLifecycleObserver.SessionLifecycleObserver) (registry: ChildAgentRegistry) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        do! appendSyntaxDiagnostics pluginDirectory input output
        let tool = toolNameFromHookInput input
        let sessionID = Wanxiangshu.Kernel.Domain.Id.sessionIdValue (fromOpencode input pluginDirectory).Execution.SessionId
        let originalOutput = hookOutputText output
        if Wanxiangshu.Shell.FallbackMessageCodec.isNetworkErrorText originalOutput then
            setHookError output "network connection lost"
        let succeeded = hookOutputError output = ""
        if check sessionID tool (JS.JSON.stringify (argsFromHookInput input)) originalOutput then
            setHookError output "livelock guard: repeated identical tool call with identical result"
        do! lifecycleObserver.handleToolExecuteAfter input output
    }