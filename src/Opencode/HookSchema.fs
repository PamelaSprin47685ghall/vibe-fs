module VibeFs.Opencode.HookSchema

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.SubagentIntents
open VibeFs.Opencode.MagicTodo
open VibeFs.Opencode.ToolSchema

let setUiLabel (setKey: obj -> string -> obj -> unit) (args: obj) (tool: string) : unit =
    let labelResult =
        match tool with
        | "coder" -> joinCoderUiLabel (Dyn.get args "intents")
        | "investigator" -> joinInvestigatorUiLabel (Dyn.get args "intents")
        | _ -> Result.Error ""
    match labelResult with
    | Result.Ok label when label <> "" -> setKey args "_ui" (box label)
    | _ -> ()

let private objectKeys (o: obj) : string array =
    JS.Constructors.Object.keys(o) |> Seq.toArray

let private requiredWithoutUi (required: obj) : obj =
    if not (isArray required) then required
    else required :?> obj array |> Array.choose (fun item -> let key = string item in if key = "_ui" then None else Some(box key)) |> box

let private requiredWithoutTaskId (required: obj) : obj =
    if not (isArray required) then required
    else required :?> obj array |> Array.choose (fun item -> let key = string item in if key = "task_id" then None else Some(box key)) |> box

let stripUiFromJsonSchema (schema: obj) : obj =
    if isNullish schema then schema
    else
        let properties = get schema "properties"
        if isNullish properties then schema
        else
            let nextProperties =
                objectKeys properties
                |> Array.choose (fun key -> if key = "_ui" then None else Some(key, get properties key))
                |> createObj
            createObj [ for key in objectKeys schema do if key = "properties" then yield key, nextProperties elif key = "required" then yield key, requiredWithoutUi (get schema "required") else yield key, get schema key ]

let rewriteToolJsonSchema (setKey: obj -> string -> obj -> unit) (rewrite: obj -> obj) (output: obj) : unit =
    let jsonSchema = get output "jsonSchema"
    if not (isNullish jsonSchema) then setKey output "jsonSchema" (rewrite jsonSchema)
    else
        let parameters = get output "parameters"
        if not (isNullish parameters) then setKey output "parameters" (rewrite parameters)

let private stringProperty (description: string) : obj =
    createObj [ "type", box "string"; "description", box description ]

let private stringZodProperty (description: string) : obj =
    createObj [
        "type", box "string"
        "minLength", box 1
        "description", box description
    ]

let private hasCallable (o: obj) (key: string) : bool =
    let value = get o key
    not (isNullish value) && jsType value = "function"

let private callSchemaMethod (schema: obj) (methodName: string) (arg: obj) : obj option =
    if hasCallable schema methodName then Some (call1 schema methodName arg)
    else None

let private callSchemaMethod0 (schema: obj) (methodName: string) : obj option =
    if hasCallable schema methodName then Some (call0 schema methodName)
    else None

let private tryGetNestedTaskStringSchema (schema: obj) : obj option =
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

let private buildZodReportField (schema: obj) : obj option =
    let shape = get schema "shape"
    let existing = if isNullish shape then null else get shape "completedWorkReport"
    if not (isNullish existing) then Some existing
    else
        match tryGetNestedTaskStringSchema schema with
        | None -> None
        | Some candidate ->
            let baseSchema =
                match callSchemaMethod0 candidate "unwrap" with
                | Some inner -> inner
                | None -> candidate
            match callSchemaMethod baseSchema "describe" (box reportDesc) with
            | None -> None
            | Some described ->
                match callSchemaMethod0 described "optional" with
                | Some optionalField -> Some optionalField
                | None -> Some described

let private extendZodTaskSchema (schema: obj) : obj =
    match buildZodReportField schema with
    | None -> schema
    | Some reportField ->
        let extension = createObj [ "completedWorkReport", reportField ]
        match callSchemaMethod schema "safeExtend" extension with
        | Some next -> next
        | None ->
            match callSchemaMethod schema "extend" extension with
            | Some next -> next
            | None -> schema

let buildMagicTodoSchema () : obj =
    let todoItem =
        createObj [
            "type", box "object"
            "properties", createObj [ "content", stringProperty todoContentDesc; "status", stringProperty todoStatusDesc; "priority", stringProperty todoPriorityDesc ]
            "required", box [| box "content"; box "status"; box "priority" |]
        ]
    createObj [
        "type", box "object"
        "properties", createObj [ "todos", createObj [ "type", box "array"; "description", box todosDesc; "items", todoItem ]; "completedWorkReport", stringProperty reportDesc ]
        "required", box [| box "todos"; box "completedWorkReport" |]
    ]

let mergeMagicReportIntoTaskSchema (schema: obj) : obj =
    if isNullish schema then schema
    elif hasCallable schema "safeExtend" || hasCallable schema "extend" then
        extendZodTaskSchema schema
    else
        let properties = get schema "properties"
        if isNullish properties then schema
        else
            if isNullish (get properties "completedWorkReport") then
                properties?("completedWorkReport") <- stringProperty reportDesc
            if not (isNullish (get properties "task_id")) then
                JS.Constructors.Object.keys(properties)
                |> Seq.toArray
                |> Array.filter (fun key -> key <> "task_id")
                |> Array.map (fun key -> key, get properties key)
                |> createObj
                |> fun nextProperties ->
                    createObj [ for key in objectKeys schema do if key = "properties" then yield key, nextProperties elif key = "required" then yield key, requiredWithoutTaskId (get schema "required") else yield key, get schema key ]
            else schema

let fusedTaskToolDescription =
    toolDescriptionFor Mimocode
    + "\n\n"
    + "This host also exposes the native session task registry: every call must include an `operation` object "
    + "(actions: create, list, get, start, block, unblock, done, abandon, rename). "
    + "Include `completedWorkReport` on calls where you made or planned meaningful progress so Magic Todo can fold context; "
    + "read-only operations such as list/get may omit it. "
    + "Consecutive `task` tool results without intervening user messages are merged into one backlog entry for context folding (OpenCode `todowrite` does not merge — one call, one entry)."
