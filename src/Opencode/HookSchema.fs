module VibeFs.Opencode.HookSchema

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Opencode.Magic

let private setKey (target: obj) (key: string) (value: obj) : unit = target?(key) <- value

let private objectKeys (value: obj) : string array =
    JS.Constructors.Object.keys(value) |> Seq.toArray

let private requiredWithoutUi (required: obj) : obj =
    if not (isArray required) then required
    else
        required
        :?> obj array
        |> Array.choose (fun item ->
            let key = string item
            if key = "_ui" then None else Some (box key))
        |> box

let stripUiFromJsonSchema (schema: obj) : obj =
    if isNullish schema then schema
    else
        let properties = get schema "properties"
        if isNullish properties then schema
        else
            let nextProperties =
                objectKeys properties
                |> Array.choose (fun key ->
                    if key = "_ui" then None
                    else Some (key, get properties key))
                |> createObj

            createObj
                [ for key in objectKeys schema do
                      if key = "properties" then
                          yield key, nextProperties
                      elif key = "required" then
                          yield key, requiredWithoutUi (get schema "required")
                      else
                          yield key, get schema key ]

let rewriteToolJsonSchema (rewrite: obj -> obj) (output: obj) : unit =
    let jsonSchema = get output "jsonSchema"
    if not (isNullish jsonSchema) then
        setKey output "jsonSchema" (rewrite jsonSchema)
    else
        let parameters = get output "parameters"
        if not (isNullish parameters) then
            setKey output "parameters" (rewrite parameters)

let private stringProperty (description: string) : obj =
    createObj [ "type", box "string"; "description", box description ]

// Built-in `todowrite` ships its schema as an Effect Schema (output.parameters)
// with output.jsonSchema undefined, and tool/registry only forwards a custom
// schema to the model when output.jsonSchema changes. So we hand it a complete
// JSON Schema via output.jsonSchema and leave output.parameters untouched.
let buildMagicTodoSchema () : obj =
    let todoItem =
        createObj [
            "type", box "object"
            "properties", createObj [
                "content", stringProperty todoContentDesc
                "status", stringProperty todoStatusDesc
                "priority", stringProperty todoPriorityDesc
            ]
            "required", box [| box "content"; box "status"; box "priority" |]
        ]
    createObj [
        "type", box "object"
        "properties", createObj [
            "todos", createObj [
                "type", box "array"
                "description", box todosDesc
                "items", todoItem
            ]
            "completedWorkReport", stringProperty reportDesc
        ]
        "required", box [| box "todos"; box "completedWorkReport" |]
    ]
