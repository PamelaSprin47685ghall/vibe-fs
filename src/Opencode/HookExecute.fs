module VibeFs.Opencode.HookExecute

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Opencode.AgentConfig
open VibeFs.Opencode.HookSchema
open VibeFs.Opencode.KnowledgeGraphRuntime
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.TreeSitterShell

let private setOutput (o: obj) (v: string) : unit = o?output <- v

let private rewriteMimocodeApplyPatchArgsForExecute (output: obj) (input: obj) (args: obj) : unit =
    if Dyn.str input "tool" <> "apply_patch" then ()
    elif Dyn.typeIs args "string" then
        setKey output "args" (createObj [ "patchText", args ])
    else
        let patchText = Dyn.str args "patchText"
        if patchText <> "" then ()
        else
            let patch = Dyn.str args "patch"
            if patch <> "" then
                setKey output "args" (createObj [ "patchText", box patch ])
            else
                let text = Dyn.str args "text"
                if text <> "" then setKey output "args" (createObj [ "patchText", box text ])

let toolExecuteBeforeFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let args = Dyn.get output "args"
        if Dyn.isNullish args then ()
        else
            let tool = Dyn.str input "tool"
            setUiLabel args tool
            if host = Mimocode then
                rewriteMimocodeApplyPatchArgsForExecute output input args
    }

let toolExecuteBefore (input: obj) (output: obj) : JS.Promise<unit> =
    toolExecuteBeforeFor opencode input output

let private appendSyntaxDiagnostics (directory: string) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let tool = Dyn.str input "tool"
        if not (isFileEditTool tool) then ()
        else
            let out = Dyn.get output "output"
            if Dyn.isNullish out || not (Dyn.typeIs out "string") then ()
            else
                let s = string out
                if hasSyntaxCheckMarker s then ()
                else
                    let paths = extractFilePaths (Dyn.get input "args")
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
/// `isFileEditTool`; subagent and IO tools are listed explicitly. Pure lookups
/// (fuzzy_find/fuzzy_grep), the knowledge graph/review tools themselves, and host read
/// tools never record.
let private bookkeepingSubagentTools =
    Set [ "coder"; "investigator"; "meditator"; "browser"; "executor"; "websearch"; "webfetch"; "write"; "apply_patch"; "patch" ]

let private recordsToBookkeeper (tool: string) : bool =
    isFileEditTool tool
    || Set.contains tool bookkeepingSubagentTools

let private isReadOnlyExecutor (tool: string) (input: obj) : bool =
    tool = "executor" && Dyn.str (Dyn.get input "args") "mode" = "ro"

let private bookkeeperInput (input: obj) : string =
    let args = Dyn.get input "args"
    if Dyn.isNullish args then "" else JS.JSON.stringify args

let toolExecuteAfterFor (host: Host) (directory: string) (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (knowledgeGraphRuntime: KnowledgeGraphRuntime) (registry: ChildAgentRegistry) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        do! appendSyntaxDiagnostics directory input output
        let tool = Dyn.str input "tool"
        let sessionID = Dyn.str input "sessionID"
        let succeeded = Dyn.str output "error" = ""
        if succeeded && recordsToBookkeeper tool && not (isReadOnlyExecutor tool input) && (registry.LookupChildAgent sessionID).IsNone then
            knowledgeGraphRuntime.StartBookkeeperAppend(bookkeeperInput input, Dyn.str output "output", tool, parentSessionID = sessionID)
        do! nudgeHook.handleToolExecuteAfter input output
    }
