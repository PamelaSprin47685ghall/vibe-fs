module Wanxiangshu.Opencode.HookSchema

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Shell.SubagentIntentsCodec
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Opencode.ToolSchema
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.WorkBacklogSchema

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

let private stringZodProperty (description: string) : obj =
    createObj [
        "type", box "string"
        "minLength", box 1
        "description", box description
    ]

let private hasCallable (o: obj) (key: string) : bool =
    let value = get o key
    not (isNullish value) && jsType value = "function"

let private callSchemaMethodOpt (schema: obj) (methodName: string) (arg: obj option) : obj option =
    if hasCallable schema methodName then
        match arg with
        | Some a -> Some (ToolSchema.call1 schema methodName a)
        | None -> Some (ToolSchema.call0 schema methodName)
    else None

let private callSchemaMethod (schema: obj) (methodName: string) (arg: obj) : obj option =
    callSchemaMethodOpt schema methodName (Some arg)

let private callSchemaMethod0 (schema: obj) (methodName: string) : obj option =
    callSchemaMethodOpt schema methodName None

let private tryGetNestedTaskStringSchema (schema: obj) : obj option =
    try
        let shape = get schema "shape"
        if isNullish shape then None
        else
            let operation = get shape "operation"
            let options = get operation "options"
            if not (isArray options) then None
            else
                options :?> obj array
                |> Array.tryPick (fun optionSchema ->
                    let optionShape = get optionSchema "shape"
                    if isNullish optionShape then None
                    else
                        [| "summary"; "event_summary"; "id"; "session_id" |]
                        |> Array.tryPick (fun key ->
                            let candidate = get optionShape key
                            if isNullish candidate then None else Some candidate))
    with _ -> None

let private extendZodTaskSchema (schema: obj) : obj =
    try
        let shape = get schema "shape"
        if isNullish shape then schema
        else
            let keys = Dyn.keys shape
            let mutable templateStr = null
            let mutable existingReport = null
            let mutable existingMethodology = null
            for k in keys do
                let prop = get shape k
                if k = "completedWorkReport" then existingReport <- prop
                if k = "select_methodology" then existingMethodology <- prop
                let typeName = get (get prop "_def") "typeName"
                if typeName = "ZodString" then
                    templateStr <- prop
            if isNullish templateStr then
                match tryGetNestedTaskStringSchema schema with
                | Some candidate ->
                    templateStr <-
                        match callSchemaMethod0 candidate "unwrap" with
                        | Some inner -> inner
                        | None -> candidate
                | None -> ()
            if isNullish templateStr then schema
            else
                let mutable extProps : (string * obj) list = []
                if isNullish existingReport then
                    match callSchemaMethod templateStr "describe" (box reportDesc) with
                    | Some describedStr -> extProps <- ("completedWorkReport", describedStr) :: extProps
                    | None -> ()
                if isNullish existingMethodology then
                    match callSchemaMethod0 templateStr "array" with
                    | Some arr ->
                        match callSchemaMethod arr "min" (box 1) with
                        | Some minArr ->
                            match callSchemaMethod minArr "describe" (box Wanxiangshu.Kernel.Methodology.selectMethodologyFieldDescription) with
                            | Some descArr -> extProps <- ("select_methodology", descArr) :: extProps
                            | None -> ()
                        | None -> ()
                    | None -> ()
                if List.isEmpty extProps then schema
                else
                    let extension = createObj extProps
                    match callSchemaMethod schema "safeExtend" extension with
                    | Some next -> next
                    | None ->
                        match callSchemaMethod schema "extend" extension with
                        | Some next -> next
                        | None -> schema
    with _ -> schema

let buildWorkBacklogSchema () : obj = WorkBacklogSchema.buildWorkBacklogSchema ()

let mergeWorkBacklogReportIntoTaskSchema (schema: obj) : obj =
    if isNullish schema then schema
    elif hasCallable schema "safeExtend" || hasCallable schema "extend" then
        extendZodTaskSchema schema
    else
        let properties = get schema "properties"
        if isNullish properties then schema
        else
            if isNullish (get properties "completedWorkReport") then
                properties?("completedWorkReport") <- jsonStringProperty reportDesc
            if isNullish (get properties "select_methodology") then
                properties?("select_methodology") <- selectMethodologyProperty
            if not (isNullish (get properties "task_id")) then
                Dyn.keys properties
                |> Array.filter (fun key -> key <> "task_id")
                |> Array.map (fun key -> key, get properties key)
                |> createObj
                |> fun nextProperties ->
                    createObj [ for key in Dyn.keys schema do if key = "properties" then yield key, nextProperties elif key = "required" then yield key, requiredWithoutTaskId (get schema "required") |> appendRequiredKey "select_methodology" else yield key, get schema key ]
            else
                createObj [ for key in Dyn.keys schema do if key = "required" then yield key, appendRequiredKey "select_methodology" (get schema "required") else yield key, get schema key ]

let fusedTaskToolDescription = toolDescriptionFor Mimocode
