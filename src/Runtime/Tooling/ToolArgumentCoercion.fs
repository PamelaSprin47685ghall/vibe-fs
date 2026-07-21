module Wanxiangshu.Runtime.ToolArgumentCoercion

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.DynField
open Wanxiangshu.Runtime.ToolSchemaRegistry
open Thoth.Json

/// Look up the registered SchemaType for a (tool, field) pair.
let parseSchemaToTypes (tool: string) (field: string) : SchemaType option =
    match field with
    | "follow-tdd-and-kolmogorov-principles"
    | "impossible-via-other-tools"
    | "not-suitable-via-continue-tool" -> Some SNumber
    | _ ->
        registry
        |> Seq.tryPick (fun (t, f, s) -> if (t = "" || t = tool) && f = field then Some s else None)

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

let canonicalToolName (toolName: string) : string =
    match toolName.ToLowerInvariant() with
    | "file_read"
    | "read" -> "read"
    | "file_write"
    | "write" -> "write"
    | "task"
    | "todowrite" -> "todowrite"
    | other -> other

let tryCoerceStringValue (expected: SchemaType) (s: string) : obj option =
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

type ToolCapability =
    | FileMutation
    | ProcessExecution
    | SubagentDelegation

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
          || t = "swap"
          || t.StartsWith "pty_"
      then
          yield FileMutation
      if t = "executor" || t.StartsWith "pty_" then
          yield ProcessExecution
      if t = "coder" || t = "inspector" || t = "meditator" || t = "browser" then
          yield SubagentDelegation ]

let decorateAndValidateSchema (toolName: string) (schema: obj) : obj =
    if Dyn.isNullish schema then schema else schema

let registerSchemaTypes (toolName: string) (schema: obj) : unit =
    if toolName <> "" then
        removeRegistryForTool toolName
        let decorated = decorateAndValidateSchema toolName schema

        extractSchemaTypes decorated
        |> List.map (fun (field, st) -> (toolName, field, st))
        |> registerToolParameterTypes
