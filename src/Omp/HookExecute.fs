module VibeFs.Omp.HookExecute

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Shell.Dyn
open VibeFs.Shell.SubagentIntentsCodec
module Dyn = VibeFs.Shell.Dyn

/// Inject a UI label into the host-side args object so the chat UI sees a one-line
/// summary before the agent finishes. Mirrors `Opencode.HookSchema.setUiLabel`
/// but writes directly to the host args reference because pi does not expose a
/// `tool.execute.before` hook distinct from `tool_result`.
let private setUiLabel (args: obj) (toolName: string) : unit =
    let labelResult =
        match toolName with
        | "coder" -> joinCoderUiLabel (Dyn.get args "intents")
        | "investigator" -> joinInvestigatorUiLabel (Dyn.get args "intents")
        | _ -> Result.Error ""
    match labelResult with
    | Result.Ok label when label <> "" -> args?("_ui") <- box label
    | _ -> ()

/// pi accepts three names for the patch tool. Normalise them to a single
/// `patchText` shape so downstream consumers (reviewer child, executor logs)
/// always see one canonical key. pi may also arrive with `args` as a raw
/// string (the entire patch body is the args), in which case there is nothing
/// to rewrite — we leave it alone.
let private normalizePatchArgs (toolName: string) (args: obj) : unit =
    if Dyn.isNullish args then ()
    elif not (Dyn.typeIs args "object") then ()
    elif toolName <> "apply_patch" && toolName <> "patch" then ()
    else
        let existing = Dyn.str args "patchText"
        if existing <> "" then ()
        else
            let fromPatch = Dyn.str args "patch"
            if fromPatch <> "" then
                args?patchText <- box fromPatch
            else
                let fromText = Dyn.str args "text"
                if fromText <> "" then
                    args?patchText <- box fromText

/// Apply the Omp post-tool argument normalisations that must run before any
/// downstream consumer (reviewer child, executor logs, kg bookkeeper) reads
/// the args reference. Currently: patch argument unification to `patchText`
/// and `_ui` label injection for subagent intents. Pure argument mutation,
/// no IO; called from `SessionLifecycle.tool_result` since pi does not
/// expose a `tool.execute.before` hook distinct from `tool_result`.
let applyToolResultHook (toolName: string) (args: obj) : unit =
    normalizePatchArgs toolName args
    setUiLabel args toolName