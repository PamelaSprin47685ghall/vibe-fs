module VibeFs.Opencode.HookExecute

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.ToolCatalog
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

let private setOutput (o: obj) (v: string) : unit = o?output <- v

let private rewriteMimocodeApplyPatchArgsForExecute (output: obj) (input: obj) (args: obj) : unit =
    if toolNameFromHookInput input <> "apply_patch" then ()
    else
        match decodeApplyPatchFields args with
        | Result.Ok fields -> setKey output "args" (createObj [ "patchText", box fields.PatchText ])
        | Result.Error e -> setKey output "error" (box (wireEncodeToolError "apply_patch" e))

let toolExecuteBeforeFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let args = Dyn.get output "args"
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
            let out = Dyn.get output "output"
            if Dyn.isNullish out || not (Dyn.typeIs out "string") then ()
            else
                let s = string out
                if hasSyntaxCheckMarker s then ()
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
                    if formatted <> "" then setOutput output (s + "\n\n" + formatted)
    }

/// Tools whose every user-facing invocation is durable enough to feed the knowledge graph
/// bookkeeper as an input/output black box. Direct write tools join the set via
/// `ToolCatalog.isFileEditTool`; subagent and IO tools are listed explicitly. Pure lookups
/// (fuzzy_find/fuzzy_grep), the knowledge graph/review tools themselves, and host read
/// tools never record.
let private bookkeepingSubagentTools =
    Set [ "coder"; "investigator"; "meditator"; "browser"; "executor"; "websearch"; "webfetch"; "write"; "apply_patch"; "patch" ]

let private recordsToBookkeeper (tool: string) : bool =
    isFileEditTool tool
    || Set.contains tool bookkeepingSubagentTools

let private isReadOnlyExecutor (tool: string) (input: obj) : bool =
    tool = "executor" && executorModeFromHookInput input = "ro"

let private bookkeeperInput (input: obj) : string =
    let args = argsFromHookInput input
    if Dyn.isNullish args then "" else JS.JSON.stringify args

let toolExecuteAfterFor (host: Host) (pluginDirectory: string) (lifecycleObserver: VibeFs.Opencode.SessionLifecycleObserver.SessionLifecycleObserver) (knowledgeGraphRuntime: KnowledgeGraphRuntime) (registry: ChildAgentRegistry) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        do! appendSyntaxDiagnostics pluginDirectory input output
        let tool = toolNameFromHookInput input
        let sessionID = (fromOpencode input pluginDirectory).Execution.SessionId
        let succeeded = hookOutputError output = ""
        let originalOutput = hookOutputText output
        if succeeded && recordsToBookkeeper tool && not (isReadOnlyExecutor tool input) && (registry.LookupChildAgent sessionID).IsNone then
            knowledgeGraphRuntime.StartBookkeeperAppend(bookkeeperInput input, originalOutput, tool, parentSessionID = sessionID)
            setOutput output (VibeFs.Kernel.WorkBacklog.withTodoHint originalOutput)
        do! lifecycleObserver.handleToolExecuteAfter input output
    }