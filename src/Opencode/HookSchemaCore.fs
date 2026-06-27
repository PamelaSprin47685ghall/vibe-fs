module Wanxiangshu.Opencode.HookSchemaCore

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.Methodology

let selectMethodologyFieldDescription = Wanxiangshu.Kernel.Methodology.selectMethodologyFieldDescription

[<Import("Schema", "effect")>]
let private effectSchemaNs : obj = jsNative

/// Write `_ui` directly onto the host's args reference when the tool exposes a
/// UI label (coder/investigator). The host keeps the same args object it passed
/// in, so the label survives into the message history the UI reads. Replacing
/// the reference dropped `_ui` — the host never saw the new object.
let setUiLabel (args: obj) (tool: string) : unit =
    let raw = intentsRawFromArgs args
    let labelResult =
        match tool with
        | "coder" -> joinCoderUiLabel raw
        | "investigator" -> joinInvestigatorUiLabel raw
        | _ -> Result.Error ""
    match labelResult with
    | Result.Ok label when label <> "" -> args?("_ui") <- box label
    | _ -> ()

let private filterRequired (excludeKey: string) (required: obj) : obj =
    if not (isArray required) then required
    else required :?> obj array |> Array.choose (fun item -> let key = string item in if key = excludeKey then None else Some(box key)) |> box

let private requiredWithoutUi (required: obj) : obj = filterRequired "_ui" required

let private requiredWithoutTaskId (required: obj) : obj = filterRequired "task_id" required

let private appendRequiredKey (requiredKey: string) (required: obj) : obj =
    if not (isArray required) then box [| box requiredKey |]
    else
        let arr = unbox<obj[]> required
        let contains = arr |> Array.exists (fun x -> string x = requiredKey)
        if contains then required
        else Array.append arr [| box requiredKey |] |> box

let stripUiFromJsonSchema (schema: obj) : obj =
    if isNullish schema then schema
    else
        let properties = get schema "properties"
        if isNullish properties then schema
        else
            let nextProperties =
                Dyn.keys properties
                |> Array.choose (fun key -> if key = "_ui" then None else Some(key, get properties key))
                |> createObj
            createObj [ for key in Dyn.keys schema do if key = "properties" then yield key, nextProperties elif key = "required" then yield key, requiredWithoutUi (get schema "required") else yield key, get schema key ]

let private tryBuildJsonSchemaFromEffectSchema (parameters: obj) : obj =
    try
        let toJsonSchemaDocument = get effectSchemaNs "toJsonSchemaDocument"
        if isNullish toJsonSchemaDocument || not (Dyn.typeIs toJsonSchemaDocument "function") then null
        else
            let document = effectSchemaNs?("toJsonSchemaDocument")(parameters, createObj [ "additionalProperties", box true ])
            get document "schema"
    with _ -> null

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
                rewrite args |> ignore

let warnTddProperty : obj =
    createObj [
        "type", box "string"
        "minLength", box 1
        "description", box Params.warnTddDesc
        "enum", [| WarnTdd.canonicalValue |] |> box
    ]

let inlineJsonWarnTddProperty : obj =
    createObj [
        "type", box "string"
        "enum", [| WarnTdd.canonicalValue |] |> box
        "description", box Params.warnTddDesc
    ]

let private appendRequiredWarnTddInPlace (schema: obj) : unit =
    let existingRequired = get schema "required"
    if isArray existingRequired then
        let arr = unbox<obj[]> existingRequired
        if not (arr |> Array.exists (fun x -> string x = "warn_tdd")) then
            existingRequired?("push")(box "warn_tdd") |> ignore
    else
        schema?("required") <- box [| box "warn_tdd" |]

let private injectWarnTddIntoJsonSchemaInPlace (schema: obj) : unit =
    let props = get schema "properties"
    if not (isNullish props) && isNullish (get props "warn_tdd") then
        props?("warn_tdd") <- inlineJsonWarnTddProperty
        appendRequiredWarnTddInPlace schema

let private injectWarnTddIntoArgsShapeInPlace (shape: obj) : unit =
    if isNullish (get shape "warn_tdd") then
        shape?("warn_tdd") <- enumReq [| WarnTdd.canonicalValue |] Params.warnTddDesc

/// Inject warn_tdd into an Opencode tool schema in place.
let injectWarnTddIntoJsonSchema (schema: obj) : obj =
    if isNullish schema then schema
    else
        let props = get schema "properties"
        if not (isNullish props) then
            injectWarnTddIntoJsonSchemaInPlace schema
        else
            injectWarnTddIntoArgsShapeInPlace schema
        schema
