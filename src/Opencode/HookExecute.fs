module VibeFs.Opencode.HookExecute

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Opencode.HookSchema
open VibeFs.Opencode.MagicTodo
open VibeFs.Shell.TreeSitterShell

let private setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v
let private setOutput (o: obj) (v: string) : unit = o?output <- v

/// Stash the report keyed by callID for `tool.execute.after` to restore.
/// Capture must happen before the cleanup deletes the field so the after-hook
/// can put it back on the same args reference.
let private captureMimocodeReport (input: obj) (args: obj) : unit =
    let callID = Dyn.str input "callID"
    let operation = Dyn.get args "operation"
    let topReport = Dyn.str args "completedWorkReport"
    let nestedReport = Dyn.str operation "completedWorkReport"
    let report = if topReport <> "" then topReport else nestedReport
    if report <> "" then captureCompletedWorkReport callID report

/// Strip Mimocode `task` extras from the original args reference so the host's
/// strict task schema parses. The host re-parses this very reference, so the
/// deletes MUST land on it (and on the nested `operation` object) in place.
let private stripMimocodeTaskArgsForExecute (input: obj) (args: obj) : unit =
    let tool = normalizeToolName Mimocode (Dyn.str input "tool")
    if tool <> "todowrite" then ()
    else
        captureMimocodeReport input args
        Dyn.deleteKey args "completedWorkReport"
        Dyn.deleteKey args "task_id"
        let operation = Dyn.get args "operation"
        if not (Dyn.isNullish operation) then
            Dyn.deleteKey operation "completedWorkReport"

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

/// Restore the captured report after Mimocode's task call returns, so backlog
/// replay sees the report on the original args object.
let private restoreMimocodeTaskArgsAfterExecute (host: Host) (input: obj) (args: obj) : unit =
    if host <> Mimocode then ()
    else
        let tool = normalizeToolName host (Dyn.str input "tool")
        if tool <> "todowrite" then ()
        else
            let callID = Dyn.str input "callID"
            let cached = takeCompletedWorkReport callID
            if cached <> "" then setKey args "completedWorkReport" (box cached)

let toolExecuteBeforeFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let args = Dyn.get output "args"
        if Dyn.isNullish args then ()
        else
            let tool = Dyn.str input "tool"
            setUiLabel setKey args tool
            if host = Mimocode then
                stripMimocodeTaskArgsForExecute input args
                rewriteMimocodeApplyPatchArgsForExecute output input args
    } |> Async.StartAsPromise

let toolExecuteBefore (input: obj) (output: obj) : JS.Promise<unit> =
    toolExecuteBeforeFor opencode input output

let private appendSyntaxDiagnostics (directory: string) (input: obj) (output: obj) : Async<unit> =
    async {
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
                        |> List.map (fun path -> readAndCheckSyntax path directory false |> Async.AwaitPromise)
                        |> Async.Parallel
                    let formatted =
                        diagnostics
                        |> Array.choose id
                        |> String.concat "\n"
                    if formatted <> "" then setOutput output (s + "\n\n" + formatted)
    }

let toolExecuteAfterFor (host: Host) (directory: string) (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let args = Dyn.get input "args"
        if not (Dyn.isNullish args) then restoreMimocodeTaskArgsAfterExecute host input args
        do! appendSyntaxDiagnostics directory input output
        do! nudgeHook.handleToolExecuteAfter input output |> Async.AwaitPromise
    } |> Async.StartAsPromise

let toolExecuteAfter (directory: string) (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (input: obj) (output: obj) : JS.Promise<unit> =
    toolExecuteAfterFor opencode directory nudgeHook input output
