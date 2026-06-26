module Wanxiangshu.Omp.HookExecute

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.SubagentIntentsCodec
module Dyn = Wanxiangshu.Shell.Dyn

/// Inject a UI label into the host-side args object so the chat UI sees a one-line
/// summary before the agent finishes. Mirrors `Opencode.HookSchema.setUiLabel`.
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

/// Shared pre-execute normalisation: patch argument unification to `patchText`
/// and `_ui` label injection for subagent intents. Called by both the
/// `tool_call` pre-execute hook and the `tool_result` post-execute hook
/// (the latter via `applyToolCallHook` to keep the logic in one place).
let applyPreExecuteHook (toolName: string) (args: obj) : unit =
    normalizePatchArgs toolName args
    setUiLabel args toolName

/// Apply the Omp pre-tool argument normalisations that must run before any
/// downstream consumer reads the args reference. pi exposes a `tool_call`
/// hook that fires pre-execute — this is the right insertion point.
let applyToolCallHook (toolName: string) (args: obj) : unit =
    applyPreExecuteHook toolName args

/// Apply the Omp post-tool argument normalisations. Runs the same normalisation
/// as a post-execute idempotency guard — the `tool_call` pre-hook might not
/// fire under race conditions, so `tool_result` is the last safe insertion point.
let applyToolResultHook (toolName: string) (args: obj) : unit =
    applyPreExecuteHook toolName args