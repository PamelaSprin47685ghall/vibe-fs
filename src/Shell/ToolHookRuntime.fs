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
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.ToolCatalog
open Thoth.Json

/// Schema-level type for a single tool parameter field.
/// Used by the generic coerceArgsTypes to replace the ad-hoc whitelist.
type SchemaType =
    | SString
    | SNumber
    | SBoolean
    | SArray
    | SObject

let private registry = ResizeArray<string * string * SchemaType>()

/// Register the parameter types for a given tool.
/// Each entry is (toolName, fieldName, schemaType).
let registerToolParameterTypes (entries: (string * string * SchemaType) list) : unit =
    for entry in entries do
        registry.Add entry

let private removeRegistryForTool (tool: string) : unit =
    let mutable i = registry.Count - 1

    while i >= 0 do
        let (t, _, _) = registry.[i]

        if t = tool then
            registry.RemoveAt i

        i <- i - 1

let private schemaTypeOfJsonField (fieldSchema: obj) : SchemaType option =
    if Dyn.isNullish fieldSchema then
        None
    else
        let rawType = Dyn.get fieldSchema "type"

        let typeName =
            if Dyn.isNullish rawType then
                ""
            elif Dyn.isArray rawType then
                let arr = unbox<obj array> rawType

                if arr.Length > 0 then string arr.[0] else ""
            else
                string rawType

        match typeName with
        | "integer"
        | "number" -> Some SNumber
        | "boolean" -> Some SBoolean
        | "array" -> Some SArray
        | "object" -> Some SObject
        | "string" -> Some SString
        | _ -> None

/// Walk a JSON-schema-shaped object and collect (fieldName, SchemaType) pairs.
let extractSchemaTypes (schema: obj) : (string * SchemaType) list =
    if Dyn.isNullish schema then
        []
    else
        let props = Dyn.get schema "properties"

        if Dyn.isNullish props || not (Dyn.typeIs props "object") then
            []
        else
            Dyn.keys props
            |> Array.choose (fun key ->
                match schemaTypeOfJsonField (Dyn.get props key) with
                | Some st -> Some(key, st)
                | None -> None)
            |> Array.toList

/// Replace prior registrations for this tool, then register types from schema.
let registerSchemaTypes (toolName: string) (schema: obj) : unit =
    if toolName <> "" then
        removeRegistryForTool toolName

        extractSchemaTypes schema
        |> List.map (fun (field, st) -> (toolName, field, st))
        |> registerToolParameterTypes

/// Look up the registered SchemaType for a (tool, field) pair.
let parseSchemaToTypes (tool: string) (field: string) : SchemaType option =
    registry
    |> Seq.tryPick (fun (t, f, s) -> if t = tool && f = field then Some s else None)

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

let private canonicalToolName (toolName: string) : string =
    match toolName.ToLowerInvariant() with
    | "file_read"
    | "read" -> "read"
    | "file_write"
    | "write" -> "write"
    | "task"
    | "todowrite" -> "todowrite"
    | other -> other

let private tryCoerceStringValue (expected: SchemaType) (s: string) : obj option =
    let trimmed = s.Trim()

    match expected with
    | SString -> None
    | SNumber ->
        match System.Double.TryParse trimmed with
        | true, d -> Some(box d)
        | _ -> None
    | SBoolean ->
        match trimmed.ToLowerInvariant() with
        | "true" -> Some(box true)
        | "false" -> Some(box false)
        | _ -> None
    | SObject ->
        match Decode.Auto.fromString<obj> trimmed with
        | Ok parsed ->
            if
                not (Dyn.isNullish parsed)
                && Dyn.typeIs parsed "object"
                && not (Dyn.isArray parsed)
            then
                Some parsed
            else
                None
        | Error _ -> None
    | SArray ->
        match Decode.Auto.fromString<obj> trimmed with
        | Ok parsed -> if Dyn.isArray parsed then Some parsed else None
        | Error _ -> None

/// Pre-process tool args: coerce string-encoded numbers to numbers,
/// parse JSON-stringified arrays/objects, etc.
let coerceArgsTypes (toolName: string) (args: obj) : unit =
    if not (Dyn.isNullish args) && Dyn.typeIs args "object" then
        let cleanTool = canonicalToolName toolName

        for k in Dyn.keys args do
            let v = Dyn.get args k

            if not (Dyn.isNullish v) && Dyn.typeIs v "string" then
                let s = unbox<string> v

                match parseSchemaToTypes cleanTool k with
                | Some expected ->
                    match tryCoerceStringValue expected s with
                    | Some coerced -> args?(k) <- coerced
                    | None -> ()
                | None -> ()

let private registerCoreToolSchemas () : unit =
    registerToolParameterTypes
        [ ("read", "offset", SNumber)
          ("read", "limit", SNumber)
          ("websearch", "numResults", SNumber)
          ("webfetch", "timeout", SNumber)
          ("coder", "intents", SArray)
          ("investigator", "intents", SArray)
          ("executor", "dependencies", SObject)
          ("todowrite", "todos", SArray)
          ("todowrite", "select_methodology", SArray)
          ("submit_review", "affectedFiles", SArray) ]

do registerCoreToolSchemas ()
