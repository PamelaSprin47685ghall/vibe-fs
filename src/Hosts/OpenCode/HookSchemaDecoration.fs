module Wanxiangshu.Hosts.Opencode.HookSchemaDecoration

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime

open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.SubagentIntentsCodec
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Runtime.Dyn

let selectMethodologyFieldDescription =
    Wanxiangshu.Kernel.Methodology.Api.selectMethodologyFieldDescription

[<Import("Schema", "effect")>]
let private effectSchemaNs: obj = jsNative

[<Import("toJSONSchema", "zod/v4")>]
let private zodToJsonSchema (schema: obj) (opts: obj) : obj = jsNative

/// Write `ui_` directly onto the host's args reference when the tool exposes a
/// UI label (coder/inspector). The host keeps the same args object it passed
/// in, so the label survives into the message history the UI reads. Replacing
/// the reference dropped `ui_` — the host never saw the new object.
let setUiLabel (args: obj) (tool: string) : unit =
    let raw = intentsRawFromArgs args

    let labelResult =
        match tool with
        | "coder" -> joinCoderUiLabel raw
        | "inspector" -> joinInspectorUiLabel raw
        | _ -> Result.Error ""

    match labelResult with
    | Result.Ok label when label <> "" -> args?("ui_") <- box label
    | _ -> ()

let filterRequired (excludeKey: string) (required: obj) : obj =
    if not (isArray required) then
        required
    else
        required :?> obj array
        |> Array.choose (fun item -> let key = string item in if key = excludeKey then None else Some(box key))
        |> box

let requiredWithoutUi (required: obj) : obj = filterRequired "ui_" required

let requiredWithoutTaskId (required: obj) : obj = filterRequired "task_id" required

let appendRequiredKey (requiredKey: string) (required: obj) : obj =
    if not (isArray required) then
        box [| box requiredKey |]
    else
        let arr = unbox<obj[]> required
        let contains = arr |> Array.exists (fun x -> string x = requiredKey)

        if contains then
            required
        else
            Array.append arr [| box requiredKey |] |> box

let stripUiFromJsonSchema (schema: obj) : obj =
    if isNullish schema then
        schema
    else
        let properties = get schema "properties"

        if isNullish properties then
            schema
        else
            let nextProperties =
                Dyn.keys properties
                |> Array.choose (fun key -> if key = "ui_" then None else Some(key, get properties key))
                |> createObj

            createObj
                [ for key in Dyn.keys schema do
                      if key = "properties" then
                          yield key, nextProperties
                      elif key = "required" then
                          yield key, requiredWithoutUi (get schema "required")
                      else
                          yield key, get schema key ]

let tryBuildJsonSchemaFromEffectSchema (parameters: obj) : obj =
    try
        let toJsonSchemaDocument = get effectSchemaNs "toJsonSchemaDocument"

        if
            isNullish toJsonSchemaDocument
            || not (Dyn.typeIs toJsonSchemaDocument "function")
        then
            null
        else
            let document =
                effectSchemaNs?("toJsonSchemaDocument") (parameters, createObj [ "additionalProperties", box true ])

            let schema = get document "schema"
            let definitions = get document "definitions"

            if isNullish definitions || (keys definitions).Length = 0 then
                schema
            else
                let result = cloneShallow schema
                result?("$defs") <- definitions
                result
    with _ ->
        null

/// Convert a Zod v4 shape (plain object whose values are Zod schemas) into a
/// JSON Schema document. Returns null when the input is not a Zod shape.
let tryBuildJsonSchemaFromZodShape (shape: obj) : obj =
    try
        if isNullish shape then
            null
        else
            let wrapped = ToolSchema.call1 ToolSchema.schema "object" shape

            let generated =
                zodToJsonSchema wrapped (createObj [ "unrepresentable", box "any"; "target", box "draft-2020-12" ])

            if isNullish generated then null else generated
    with _ ->
        null

let rewriteToolJsonSchema (setKey: obj -> string -> obj -> unit) (rewrite: obj -> obj) (output: obj) : unit =
    let jsonSchema = get output "jsonSchema"

    if not (isNullish jsonSchema) then
        rewrite jsonSchema |> ignore
    else
        let parameters = get output "parameters"

        if not (isNullish parameters) then
            let generated = tryBuildJsonSchemaFromEffectSchema parameters

            if not (isNullish generated) then
                setKey output "jsonSchema" (rewrite generated)
            else
                rewrite parameters |> ignore
        else
            let args = get output "args"

            if not (isNullish args) then
                let fromEffect = tryBuildJsonSchemaFromEffectSchema args

                let generated =
                    if not (isNullish fromEffect) then
                        fromEffect
                    else
                        tryBuildJsonSchemaFromZodShape args

                if not (isNullish generated) then
                    setKey output "jsonSchema" (rewrite generated)
                else
                    rewrite args |> ignore
            else
                ()

let warnTddProperty: obj =
    createObj [ "type", box "string"; "description", box Params.warnTddDesc ]

let inlineJsonWarnTddProperty: obj =
    createObj [ "type", box "string"; "description", box Params.warnTddDesc ]

let inlineJsonWarnProperty: obj =
    createObj [ "type", box "string"; "description", box WarnTdd.warnDescription ]

let inlineJsonWarnReuseProperty: obj =
    createObj [ "type", box "string"; "description", box WarnTdd.warnReuseDescription ]
