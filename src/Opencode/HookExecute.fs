module VibeFs.Opencode.HookExecute

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Opencode.HookSchema
open VibeFs.Opencode.MagicTodo
open VibeFs.Opencode.WikiRuntime
open VibeFs.Shell.ChildAgentRegistry
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

/// Build a fresh args object with Mimocode `task` extras removed, never mutating
/// the host's reference: a shallow copy skips the excluded keys. The result
/// replaces `output.args` so the host's strict task schema parses it. (P46-48:
/// the before-hook returns a new record instead of in-place deleteKey/restore.)
let private copyObjExcept (source: obj) (excluded: string Set) : obj =
    let result = createObj []
    for key in Dyn.keys source do
        if not (Set.contains key excluded) then setKey result key (source?(key))
    result

let private stripMimocodeTaskArgsForExecute (input: obj) (output: obj) (args: obj) : unit =
    let tool = normalizeToolName Mimocode (Dyn.str input "tool")
    if tool <> "todowrite" then ()
    else
        captureMimocodeReport input args
        let operation = Dyn.get args "operation"
        let cleaned = copyObjExcept args (Set [ "completedWorkReport"; "task_id" ])
        if not (Dyn.isNullish operation) then
            setKey cleaned "operation" (copyObjExcept operation (Set [ "completedWorkReport" ]))
        setKey output "args" cleaned

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

// P47-48: the after-hook no longer restores completedWorkReport onto
// input.args. Capture stays in MagicSessionStore keyed by callID and backlog
// replay reads it directly via BacklogInputForPart — the old setKey had no
// consumer and its takeReport actually stole the report backlog replay needed.

let toolExecuteBeforeFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let args = Dyn.get output "args"
        if Dyn.isNullish args then ()
        else
            let tool = Dyn.str input "tool"
            setUiLabel args tool
            if host = Mimocode then
                stripMimocodeTaskArgsForExecute input output args
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

/// Tools whose every user-facing invocation is durable enough to feed the wiki
/// bookkeeper as an input/output black box. Direct write tools join the set via
/// `isFileEditTool`; subagent and IO tools are listed explicitly. Pure lookups
/// (fuzzy_find/fuzzy_grep), the wiki/review tools themselves, and host read
/// tools never record.
let private bookkeepingSubagentTools =
    Set [ "coder"; "investigator"; "meditator"; "browser"; "executor"; "websearch"; "webfetch" ]

let private recordsToBookkeeper (tool: string) : bool =
    tool = "write" || tool = "apply_patch" || tool = "patch"
    || isFileEditTool tool
    || Set.contains tool bookkeepingSubagentTools

let private bookkeeperInput (input: obj) : string =
    let args = Dyn.get input "args"
    if Dyn.isNullish args then "" else JS.JSON.stringify args

let toolExecuteAfterFor (host: Host) (directory: string) (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (wikiRuntime: WikiRuntime) (registry: ChildAgentRegistry) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        do! appendSyntaxDiagnostics directory input output
        let tool = Dyn.str input "tool"
        let sessionID = Dyn.str input "sessionID"
        let succeeded = Dyn.str output "error" = ""
        if succeeded && recordsToBookkeeper tool && (registry.LookupChildAgent sessionID).IsNone then
            wikiRuntime.StartBookkeeperAppend(bookkeeperInput input, Dyn.str output "output", tool, parentSessionID = sessionID)
        do! nudgeHook.handleToolExecuteAfter input output
    }
