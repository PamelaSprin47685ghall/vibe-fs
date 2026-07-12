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

type ToolCapability =
    | FileMutation
    | ProcessExecution
    | SubagentDelegation

type ControlEnvelope =
    { WarnTdd: string option
      Warn: string option
      WarnReuse: string option
      Amend: int option }

let getToolCapabilities (toolName: string) : ToolCapability list =
    let t = toolName.ToLowerInvariant().Trim()

    [ if
          t = "coder"
          || t = "edit"
          || t = "write"
          || t = "apply_patch"
          || t = "patch"
          || t = "ast_edit"
          || t = "ast_grep_replace"
          || t = "file_edit_replace_string"
          || t = "file_edit_insert"
          || t = "executor"
          || t.StartsWith("pty_")
      then
          yield FileMutation
      if t = "executor" || t.StartsWith("pty_") then
          yield ProcessExecution
      if t = "coder" || t = "investigator" || t = "meditator" || t = "browser" then
          yield SubagentDelegation ]

let inlineJsonWarnTddProperty: obj =
    createObj
        [ "type", box "string"
          "enum", box [| box "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles-and-kept-todo-updated" |]
          "description", box "Acknowledge that tests are written first (TDD) and Kolmogorov discipline is followed." ]

let inlineJsonWarnProperty: obj =
    createObj
        [ "type", box "string"
          "enum",
          box
              [| box
                     "it-is-not-possible-to-do-it-using-other-tools-and-only-run-tests-when-static-analysis-cannot-handle-it" |]
          "description",
          box
              "Acknowledge that this task cannot be done with other tools and only run tests when static analysis cannot handle it." ]

let inlineJsonWarnReuseProperty: obj =
    createObj
        [ "type", box "string"
          "enum", box [| box "this-task-is-not-suitable-to-be-completed-via-continue-tool" |]
          "description", box "Acknowledge that this task is not suitable for completion via continue tool." ]

let inlineJsonAmendProperty: obj =
    createObj
        [ "type", box "integer"
          "minimum", box 1
          "description", box "Undo/amend the last N tool call chains by backtracking." ]

let decorateAndValidateSchema (toolName: string) (schema: obj) : obj =
    if Dyn.isNullish schema then
        schema
    else
        let props = Dyn.get schema "properties"

        if Dyn.isNullish props then
            schema
        else
            let caps = getToolCapabilities toolName

            // Injections
            if List.contains FileMutation caps then
                if Dyn.isNullish (Dyn.get props "warn_tdd") then
                    Dyn.setKey props "warn_tdd" inlineJsonWarnTddProperty

                let req = Dyn.get schema "required"

                if Dyn.isArray req then
                    let arr = req :?> obj array

                    if not (Array.exists (fun x -> string x = "warn_tdd") arr) then
                        let nextReq = Array.append arr [| box "warn_tdd" |]
                        Dyn.setKey schema "required" nextReq
                else
                    Dyn.setKey schema "required" [| box "warn_tdd" |]

            if List.contains ProcessExecution caps then
                if Dyn.isNullish (Dyn.get props "warn") then
                    Dyn.setKey props "warn" inlineJsonWarnProperty

                let req = Dyn.get schema "required"

                if Dyn.isArray req then
                    let arr = req :?> obj array

                    if not (Array.exists (fun x -> string x = "warn") arr) then
                        let nextReq = Array.append arr [| box "warn" |]
                        Dyn.setKey schema "required" nextReq
                else
                    Dyn.setKey schema "required" [| box "warn" |]

            if List.contains SubagentDelegation caps then
                if Dyn.isNullish (Dyn.get props "warn_reuse") then
                    Dyn.setKey props "warn_reuse" inlineJsonWarnReuseProperty

                let req = Dyn.get schema "required"

                if Dyn.isArray req then
                    let arr = req :?> obj array

                    if not (Array.exists (fun x -> string x = "warn_reuse") arr) then
                        let nextReq = Array.append arr [| box "warn_reuse" |]
                        Dyn.setKey schema "required" nextReq
                else
                    Dyn.setKey schema "required" [| box "warn_reuse" |]

            if Dyn.isNullish (Dyn.get props "amend") then
                Dyn.setKey props "amend" inlineJsonAmendProperty

            // Validation (Fail Closed)
            let requiredList =
                let req = Dyn.get schema "required"

                if Dyn.isArray req then
                    (req :?> obj array) |> Array.map string |> Array.toList
                else
                    []

            for reqField in requiredList do
                if Dyn.isNullish (Dyn.get props reqField) then
                    failwith
                        $"Schema Validation Failed: required field '{reqField}' is not defined in properties of tool '{toolName}'"

            if List.contains FileMutation caps && not (List.contains "warn_tdd" requiredList) then
                failwith
                    $"Schema Validation Failed: FileMutation tool '{toolName}' is missing required field 'warn_tdd'"

            if List.contains ProcessExecution caps && not (List.contains "warn" requiredList) then
                failwith
                    $"Schema Validation Failed: ProcessExecution tool '{toolName}' is missing required field 'warn'"

            if
                List.contains SubagentDelegation caps
                && not (List.contains "warn_reuse" requiredList)
            then
                failwith
                    $"Schema Validation Failed: SubagentDelegation tool '{toolName}' is missing required field 'warn_reuse'"

            schema

/// Replace prior registrations for this tool, then register types from schema.
let registerSchemaTypes (toolName: string) (schema: obj) : unit =
    if toolName <> "" then
        removeRegistryForTool toolName

        let decorated = decorateAndValidateSchema toolName schema

        extractSchemaTypes decorated
        |> List.map (fun (field, st) -> (toolName, field, st))
        |> registerToolParameterTypes

[<Emit("Object.defineProperty($0, '_amend', { value: $1, enumerable: false, writable: true, configurable: true })")>]
let private defineHiddenAmend (args: obj) (value: obj) : unit = jsNative

let restoreAmendToArgs (args: obj) (amendVal: obj) : unit =
    if not (Dyn.isNullish args) && not (Dyn.isNullish amendVal) then
        args?("amend") <- amendVal

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

let executeBeforeGateway (tool: string) (args: obj) : Result<obj * ControlEnvelope, string> =
    if Dyn.isNullish args then
        let empty = createObj []

        Result.Ok(
            empty,
            { WarnTdd = None
              Warn = None
              WarnReuse = None
              Amend = None }
        )
    else
        coerceArgsTypes tool args
        sanitizeNullArgs tool args

        let caps = getToolCapabilities tool

        // Extract control fields from the original args object (in-place)
        let warnTddVal =
            match DynField.strField args "warn_tdd" with
            | Some v ->
                Dyn.deleteKey args "warn_tdd"
                Some v
            | None -> None

        let warnVal =
            match DynField.strField args "warn" with
            | Some v ->
                Dyn.deleteKey args "warn"
                Some v
            | None -> None

        let warnReuseVal =
            match DynField.strField args "warn_reuse" with
            | Some v ->
                Dyn.deleteKey args "warn_reuse"
                Some v
            | None -> None

        let amendVal =
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

                parsed

        let hasControlFields =
            warnTddVal.IsSome || warnVal.IsSome || warnReuseVal.IsSome || amendVal.IsSome

        // Shallow clone the purified args to nextArgs if needed
        let nextArgs =
            if not caps.IsEmpty || hasControlFields then
                Dyn.cloneShallow args
            else
                args

        // Validate applicability & correctness
        let checkWarnTdd =
            if List.contains FileMutation caps then
                match warnTddVal with
                | Some v when
                    v.ToLowerInvariant() = "i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles-and-kept-todo-updated"
                    ->
                    Result.Ok()
                | _ ->
                    let err =
                        InvalidIntent(tool, "warn_tdd", "required — acknowledge TDD + Kolmogorov discipline")

                    Result.Error(wireDomainFailure tool err)
            else
                Result.Ok()

        let checkWarn =
            if List.contains ProcessExecution caps then
                match warnVal with
                | Some v when
                    v = "it-is-not-possible-to-do-it-using-other-tools-and-only-run-tests-when-static-analysis-cannot-handle-it"
                    ->
                    Result.Ok()
                | _ ->
                    let err =
                        InvalidIntent(tool, "warn", "required — acknowledge this task cannot be done with other tools")

                    Result.Error(wireDomainFailure tool err)
            else
                Result.Ok()

        let checkWarnReuse =
            if List.contains SubagentDelegation caps then
                match warnReuseVal with
                | Some v when
                    v.ToLowerInvariant().Trim() = "this-task-is-not-suitable-to-be-completed-via-continue-tool"
                    ->
                    Result.Ok()
                | _ ->
                    let err =
                        InvalidIntent(
                            tool,
                            "warn_reuse",
                            "required — acknowledge this task is not suitable for completion via continue tool"
                        )

                    Result.Error(wireDomainFailure tool err)
            else
                Result.Ok()

        match checkWarnTdd with
        | Result.Error e -> Result.Error e
        | Result.Ok() ->
            match checkWarn with
            | Result.Error e -> Result.Error e
            | Result.Ok() ->
                match checkWarnReuse with
                | Result.Error e -> Result.Error e
                | Result.Ok() ->
                    match amendVal with
                    | Some n ->
                        defineHiddenAmend args (box n)
                        defineHiddenAmend nextArgs (box n)
                    | None -> ()

                    let env =
                        { WarnTdd = warnTddVal
                          Warn = warnVal
                          WarnReuse = warnReuseVal
                          Amend = amendVal }

                    Result.Ok(nextArgs, env)
