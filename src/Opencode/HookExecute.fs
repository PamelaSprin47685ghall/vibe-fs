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

/// Patch describing the Mimocode task arg cleanup for the OUTER args object:
/// strip `completedWorkReport` and `task_id` so the strict task schema parses.
let private outerTaskArgsCleanup : ArgsPatch =
    { setKeys = []; deleteKeys = [ "completedWorkReport"; "task_id" ] }

/// Patch describing the cleanup for the NESTED `operation` object: strip a
/// misplaced `completedWorkReport` that the model sometimes nests there.
let private nestedTaskOperationCleanup : ArgsPatch =
    { setKeys = []; deleteKeys = [ "completedWorkReport" ] }

/// Stash the report keyed by callID for `tool.execute.after` to restore.
/// Pure in spirit: we capture before mutating so the cleanup is reversible at
/// the after-hook boundary.
let private captureMimocodeReport (input: obj) (args: obj) : unit =
    let callID = Dyn.str input "callID"
    let operation = Dyn.get args "operation"
    let topReport = Dyn.str args "completedWorkReport"
    let nestedReport = Dyn.str operation "completedWorkReport"
    let report = if topReport <> "" then topReport else nestedReport
    if report <> "" then captureCompletedWorkReport callID report

/// Apply the args/operation cleanup contract for Mimocode `task` calls.  The
/// host re-parses the ORIGINAL args reference against a strict schema, so the
/// mutation MUST land on those very objects: `applyPatch` does that at one
/// boundary instead of every hook open-coding deletes.
let private stripMimocodeTaskArgsForExecute (input: obj) (args: obj) : unit =
    let tool = normalizeToolName Mimocode (Dyn.str input "tool")
    if tool <> "todowrite" then ()
    else
        captureMimocodeReport input args
        applyPatch args outerTaskArgsCleanup
        applyPatch (Dyn.get args "operation") nestedTaskOperationCleanup

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
            if cached <> "" then applyPatch args { emptyPatch with setKeys = [ "completedWorkReport", box cached ] }

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
