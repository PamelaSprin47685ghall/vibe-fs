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

let inlineJsonWarnTddProperty: obj =
    createObj
        [ "type", box "string"
          "description",
          box "MUST acknowledge that tests are written first (TDD) and Kolmogorov discipline is followed."
          "required_", box true ]

let inlineJsonWarnProperty: obj =
    createObj
        [ "type", box "string"
          "description",
          box
              "MUST acknowledge that this task cannot be done with other tools and only run tests when static analysis cannot handle it."
          "required_", box true ]

let inlineJsonWarnReuseProperty: obj =
    createObj
        [ "type", box "string"
          "description", box "MUST acknowledge that this task is not suitable for completion via continue tool."
          "required_", box true ]

let private injectProp
    (props: obj)
    (caps: ToolCapability list)
    (cap: ToolCapability)
    (propName: string)
    (propVal: obj)
    =
    if List.contains cap caps then
        if Dyn.isNullish (Dyn.get props propName) then
            Dyn.setKey props propName propVal
        else
            let prop = Dyn.get props propName

            if not (Dyn.isNullish prop) then
                Dyn.setKey prop "required_" true

let private reportSoftWarnings (_toolName: string) (_props: obj) (_caps: ToolCapability list) (_schema: obj) = ()

let decorateAndValidateSchema (toolName: string) (schema: obj) : obj =
    if Dyn.isNullish schema then
        schema
    else
        let props = Dyn.get schema "properties"

        if Dyn.isNullish props then
            schema
        else
            let caps = getToolCapabilities toolName
            injectProp props caps FileMutation "warn_tdd" inlineJsonWarnTddProperty
            injectProp props caps ProcessExecution "warn" inlineJsonWarnProperty
            injectProp props caps SubagentDelegation "warn_reuse" inlineJsonWarnReuseProperty
            reportSoftWarnings toolName props caps schema
            schema

let registerSchemaTypes (toolName: string) (schema: obj) : unit =
    if toolName <> "" then
        removeRegistryForTool toolName
        let decorated = decorateAndValidateSchema toolName schema

        extractSchemaTypes decorated
        |> List.map (fun (field, st) -> (toolName, field, st))
        |> registerToolParameterTypes
