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

/// Look up the required field names for a given tool from ToolSpec.
/// Falls back to empty list for unknown tools.
let tryGetRequiredFields (toolName: string) : string list =
    if toolName = "methodology" || toolName.StartsWith("methodology_") then
        [ "methodology"; "intent"; "background"; "note" ]
    else
        match specOf toolName with
        | Ok spec -> spec.requiredFields
        | Error _ -> []

/// Remove null/undefined and empty object {} values from args for keys that are NOT required fields.
/// This prevents downstream tool execution from seeing spurious null/{} values
/// that the LLM filler injected for optional parameters.
let sanitizeNullArgs (toolName: string) (args: obj) : unit =
    if not (Dyn.isNullish args) && Dyn.typeIs args "object" then
        let req = tryGetRequiredFields toolName |> Set.ofList

        for k in Dyn.keys args do
            let v = Dyn.get args k
            let isNullish = Dyn.isNullish v

            let isEmptyObject =
                not isNullish
                && Dyn.typeIs v "object"
                && not (Dyn.isArray v)
                && (Dyn.keys v).Length = 0

            if (isNullish || isEmptyObject) && not (Set.contains k req) then
                Dyn.deleteKey args k

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
            let err =
                InvalidIntent(tool, "warn_tdd", "required — acknowledge TDD + Kolmolgorov discipline")

            Result.Error(wireDomainFailure tool err)

[<Emit("Object.defineProperty($0, '_amend', { value: $1, enumerable: false, writable: true, configurable: true })")>]
let private defineHiddenAmend (args: obj) (value: obj) : unit = jsNative

let restoreAmendToArgs (args: obj) (amendVal: obj) : unit =
    if not (Dyn.isNullish args) && not (Dyn.isNullish amendVal) then
        args?("amend") <- amendVal

/// Read and delete the 'amend' field from tool args.
/// Returns Some n if a valid positive integer was present, None otherwise.
/// After this call, the 'amend' key is removed from args so downstream tool
/// execution never sees it.
let filterAmendFromArgs (args: obj) : int option =
    match DynField.optField args "amend" with
    | None -> None
    | Some v ->
        Dyn.deleteKey args "amend"

        let parsed =
            match v with
            | :? int as n when n > 0 -> Some n
            | :? float as f when f > 0.0 -> Some(int f)
            | :? string as s ->
                match System.Int32.TryParse s with
                | true, n when n > 0 -> Some n
                | _ -> None
            | _ -> None

        match parsed with
        | Some n ->
            defineHiddenAmend args (box n)
            Some n
        | None -> None

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
            let err =
                InvalidIntent(tool, "warn", "required — acknowledge this task cannot be done with other tools")

            Result.Error(wireDomainFailure tool err)

let requireWarnReuseOnArgs (tool: string) (args: obj) : Result<unit, string> =
    if not (WarnTdd.isSubagentTool tool) then
        Result.Ok()
    else
        let raw = DynField.strField args "warn_reuse" |> Option.defaultValue ""

        if WarnTdd.parseWarnReuse raw then
            Dyn.deleteKey args "warn_reuse"
            Result.Ok()
        else
            let err =
                InvalidIntent(
                    tool,
                    "warn_reuse",
                    "required — acknowledge this task is not suitable for completion via continue tool"
                )

            Result.Error(wireDomainFailure tool err)

let muxToolExecuteBefore (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let tool = toolNameFromHookInputMux input
        let args = argsFromMuxToolExecuteInput input

        if not (Dyn.isNullish args) then
            match filterAmendFromArgs args with
            | Some n ->
                output?("_amend") <- box n
                input?("_amend") <- box n
            | None -> ()

            sanitizeNullArgs tool args

            let mutable hasError = false

            match requireWarnTddOnArgs tool args with
            | Result.Error e ->
                setHookErrorMux output e
                hasError <- true
            | Result.Ok() -> ()

            if not hasError then
                match requireWarnOnArgs tool args with
                | Result.Error e ->
                    setHookErrorMux output e
                    hasError <- true
                | Result.Ok() -> ()

            if not hasError then
                match requireWarnReuseOnArgs tool args with
                | Result.Error e ->
                    setHookErrorMux output e
                    hasError <- true
                | Result.Ok() -> ()

            let rawOpt: obj option = args?intents

            let labelResult =
                match tool with
                | "coder" ->
                    match rawOpt with
                    | Some r -> joinCoderUiLabel r
                    | None -> Result.Error ""
                | "investigator" ->
                    match rawOpt with
                    | Some r -> joinInvestigatorUiLabel r
                    | None -> Result.Error ""
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

        let amendVal =
            let fromOutput = Dyn.get output "_amend"

            if not (Dyn.isNullish fromOutput) then
                fromOutput
            else
                let fromInput = Dyn.get input "_amend"

                if not (Dyn.isNullish fromInput) then
                    fromInput
                else
                    let fromArgs =
                        if not (Dyn.isNullish decoded.Args) then
                            Dyn.get decoded.Args "_amend"
                        else
                            null

                    if not (Dyn.isNullish fromArgs) then fromArgs else null

        if not (Dyn.isNullish amendVal) then
            restoreAmendToArgs decoded.Args amendVal
            let inputArgs = argsFromMuxToolExecuteInput input
            restoreAmendToArgs inputArgs amendVal
            let outputArgs = argsFromHookOutputMux output
            restoreAmendToArgs outputArgs amendVal

        let argsJson = JS.JSON.stringify decoded.Args

        if isNetworkErrorText originalOutput then
            setHookErrorMux output "network connection lost"

        if LivelockGuard.check scope sessionID tool argsJson originalOutput then
            setHookErrorMux output "livelock guard: repeated identical tool call with identical result"
    }
