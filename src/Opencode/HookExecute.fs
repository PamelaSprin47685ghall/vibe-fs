module VibeFs.Opencode.HookExecute

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.ToolOutputInfo
open VibeFs.Kernel.ToolCatalog
open VibeFs.Kernel.KnowledgeGraphBookkeeperPolicy
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Opencode.AgentConfig
open VibeFs.Opencode.HookSchema
open VibeFs.Opencode.KnowledgeGraphRuntime
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.OpencodeHookInputCodec
open VibeFs.Shell.TreeSitterShell
open VibeFs.Shell.ToolRuntimeContext
open VibeFs.Shell.PatchToolsCodec
open VibeFs.Shell.ToolExecute
open VibeFs.Kernel.ToolResult
open VibeFs.Shell.Dyn

let private rewriteMimocodeApplyPatchArgsForExecute (output: obj) (input: obj) (args: obj) : unit =
    if toolNameFromHookInput input <> "apply_patch" then ()
    else
        match decodeApplyPatchFields args with
        | Result.Ok fields -> setHookArgs output (createObj [ "patchText", box fields.PatchText ])
        | Result.Error e -> setHookError output (wireEncodeToolError "apply_patch" e)

let toolExecuteBeforeFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let args = argsFromHookOutput output
        if Dyn.isNullish args then ()
        else
            let tool = toolNameFromHookInput input
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

let private isReadOnlyExecutor (tool: string) (input: obj) : bool =
    tool = "executor" && executorModeFromHookInput input = "ro"

let private bookkeeperInput (input: obj) : string =
    let args = argsFromHookInput input
    if Dyn.isNullish args then "" else JS.JSON.stringify args

let toolExecuteAfterFor (host: Host) (pluginDirectory: string) (lifecycleObserver: VibeFs.Opencode.SessionLifecycleObserver.SessionLifecycleObserver) (knowledgeGraphRuntime: KnowledgeGraphRuntime) (registry: ChildAgentRegistry) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        do! appendSyntaxDiagnostics pluginDirectory input output
        let tool = toolNameFromHookInput input
        let sessionID = VibeFs.Kernel.Domain.Id.sessionIdValue (fromOpencode input pluginDirectory).Execution.SessionId
        let succeeded = hookOutputError output = ""
        let originalOutput = hookOutputText output
        if succeeded && recordsToBookkeeper tool && not (isReadOnlyExecutor tool input) && (registry.LookupChildAgent sessionID).IsNone then
            knowledgeGraphRuntime.StartBookkeeperAppend(bookkeeperInput input, bodyForBookkeeper originalOutput, tool, parentSessionID = sessionID)
            setHookOutputString output (withBookkeepingHints originalOutput)
        do! lifecycleObserver.handleToolExecuteAfter input output
    }