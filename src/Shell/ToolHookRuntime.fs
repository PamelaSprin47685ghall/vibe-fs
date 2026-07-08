module Wanxiangshu.Shell.ToolHookRuntime

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.WarnTdd
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.DynField
open Wanxiangshu.Shell.ToolExecute
open Wanxiangshu.Shell.ToolContextCodec
open Wanxiangshu.Shell.SubagentIntentsCodec
open Wanxiangshu.Shell.MuxHookInputCodec
open Wanxiangshu.Shell.LivelockGuard
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Kernel.ToolCatalog

/// Validate warn_tdd on tool args: parse and delete if valid, else produce domain error string.
let requireWarnTddOnArgs (tool: string) (args: obj) : Result<unit, string> =
    if not (WarnTdd.isModificationTool tool) then
        Result.Ok()
    else
        let raw = DynField.strField args "warn_tdd" |> Option.defaultValue ""

        match WarnTdd.parseWarnTdd raw with
        | Some _ ->
            Dyn.deleteKey args "warn_tdd"
            Result.Ok()
        | None ->
            Result.Error(
                wireDomainFailure
                    tool
                    (InvalidIntent(tool, "warn_tdd", "required — acknowledge TDD + Kolmolgorov discipline"))
            )

/// Read and delete the 'amend' field from tool args.
/// Returns Some n if a valid positive integer was present, None otherwise.
/// After this call, the 'amend' key is removed from args so downstream tool
/// execution never sees it.
let filterAmendFromArgs (args: obj) : int option =
    match DynField.optField args "amend" with
    | None -> None
    | Some v ->
        Dyn.deleteKey args "amend"
        match v with
        | :? int as n when n > 0 -> Some n
        | :? float as f when f > 0.0 -> Some(int f)
        | :? string as s ->
            match System.Int32.TryParse s with
            | true, n when n > 0 -> Some n
            | _ -> None
        | _ -> None

/// Validate warn on tool args: parse and delete if valid, else produce domain error string.
let requireWarnOnArgs (tool: string) (args: obj) : Result<unit, string> =
    if not (WarnTdd.isWarnRequiredTool tool) then
        Result.Ok()
    else
        let raw = DynField.strField args "warn" |> Option.defaultValue ""

        if WarnTdd.parseWarn raw then
            Dyn.deleteKey args "warn"
            Result.Ok()
        else
            Result.Error(
                wireDomainFailure
                    tool
                    (InvalidIntent(tool, "warn", "required — acknowledge this task cannot be done with other tools"))
            )

let muxToolExecuteBefore (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let tool = toolNameFromHookInputMux input
        let args = argsFromMuxToolExecuteInput input

        if not (Dyn.isNullish args) then
            filterAmendFromArgs args |> ignore

            match requireWarnTddOnArgs tool args with
            | Result.Error e -> setHookErrorMux output e
            | Result.Ok() -> ()

            match requireWarnOnArgs tool args with
            | Result.Error e -> setHookErrorMux output e
            | Result.Ok() -> ()

            let raw = args?intents

            let labelResult =
                match tool with
                | "coder" -> joinCoderUiLabel (Option.defaultValue null raw |> box)
                | "investigator" -> joinInvestigatorUiLabel (Option.defaultValue null raw |> box)
                | _ -> Result.Error ""

            match labelResult with
            | Result.Ok label when label <> "" -> args?("_ui") <- box label
            | _ -> ()
    }

let muxToolExecuteAfter (scope: RuntimeScope) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let decoded = decodeMuxToolExecuteAfterInput input (box null)
        let tool = decoded.Tool
        let sessionID = decoded.SessionID
        let originalOutput = hookOutputTextMux output
        let argsJson = JS.JSON.stringify decoded.Args

        if isNetworkErrorText originalOutput then
            setHookErrorMux output "network connection lost"

        if LivelockGuard.check scope sessionID tool argsJson originalOutput then
            setHookErrorMux output "livelock guard: repeated identical tool call with identical result"
    }
