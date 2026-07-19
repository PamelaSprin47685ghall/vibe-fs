module Wanxiangshu.Hosts.Omp.HookExecute

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.SubagentIntentsCodec

module Dyn = Wanxiangshu.Runtime.Dyn

/// Inject a UI label into the host-side args object so the chat UI sees a one-line
/// summary before the agent finishes. Mirrors `Opencode.HookSchemaDecoration.setUiLabel`.
let private setUiLabel (args: obj) (toolName: string) : unit =
    let labelResult =
        match toolName with
        | "coder" -> joinCoderUiLabel (Dyn.get args "intents")
        | "inspector" -> joinInspectorUiLabel (Dyn.get args "intents")
        | _ -> Result.Error ""

    match labelResult with
    | Result.Ok label when label <> "" -> args?("ui_") <- box label
    | _ -> ()

/// pi accepts three names for the patch tool. Normalise them to a single
/// `patchText` shape so downstream consumers (reviewer child, executor logs)
/// always see one canonical key. pi may also arrive with `args` as a raw
/// string (the entire patch body is the args), in which case there is nothing
/// to rewrite — we leave it alone.
let private normalizePatchArgs (toolName: string) (args: obj) : unit =
    if Dyn.isNullish args then
        ()
    elif not (Dyn.typeIs args "object") then
        ()
    elif toolName <> "apply_patch" && toolName <> "patch" then
        ()
    else
        let existing = Dyn.str args "patchText"

        if existing <> "" then
            ()
        else
            let fromPatch = Dyn.str args "patch"

            if fromPatch <> "" then
                args?patchText <- box fromPatch
            else
                let fromText = Dyn.str args "text"

                if fromText <> "" then
                    args?patchText <- box fromText

let applyPreExecuteHookWithIds
    (toolName: string)
    (args: obj)
    (sessionIdOpt: string option)
    (toolCallIdOpt: string option)
    : string option =
    if Dyn.isNullish args then
        None
    else
        match ToolHookRuntime.executeBeforeGateway toolName args with
        | Result.Error e -> Some e
        | Result.Ok(nextArgs, env) ->
            let sessionId = sessionIdOpt |> Option.defaultValue ""
            let toolCallId = toolCallIdOpt |> Option.defaultValue ""
            ToolHookRuntime.saveCompliance sessionId toolCallId env

            for k in Dyn.keys args do
                Dyn.deleteKey args k

            Dyn.assignInto args nextArgs |> ignore

            normalizePatchArgs toolName args
            setUiLabel args toolName
            None

/// Shared pre-execute normalisation: patch argument unification to `patchText`
/// and `ui_` label injection for subagent intents. Called by both the
/// `tool_call` pre-execute hook and the `tool_result` post-execute hook
/// (the latter via `applyToolCallHook` to keep the logic in one place).
let applyPreExecuteHook (toolName: string) (args: obj) : string option =
    applyPreExecuteHookWithIds toolName args None None

/// Apply the Omp pre-tool argument normalisations that must run before any
/// downstream consumer reads the args reference. pi exposes a `tool_call`
/// hook that fires pre-execute — this is the right insertion point.
/// Returns Some error message if the tool call should be blocked.
let applyToolCallHook (toolName: string) (args: obj) : string option = applyPreExecuteHook toolName args

let applyToolCallHookWithIds (toolName: string) (args: obj) (sessionId: string) (toolCallId: string) : string option =
    applyPreExecuteHookWithIds toolName args (Some sessionId) (Some toolCallId)

let applyToolResultHook (toolName: string) (args: obj) : unit =
    if not (Dyn.isNullish args) then
        normalizePatchArgs toolName args
        setUiLabel args toolName
