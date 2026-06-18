module VibeFs.Opencode.HookSchema

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Opencode.MagicPrompts

let private setKey (target: obj) (key: string) (value: obj) : unit = target?(key) <- value

let private objectKeys (value: obj) : string array =
    JS.Constructors.Object.keys(value) |> Seq.toArray

let private isStringArray (value: obj) : bool =
    isArray value && ((value :?> obj array) |> Array.forall (fun item -> typeIs item "string"))

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

let joinReaderIntents (intents: obj) : Result<string, string> =
    if not (isArray intents) || not (isStringArray intents) then
        Error "Invalid LLM input for reader: intents must be an array of strings"
    else
        intents
        :?> obj array
        |> Array.map string
        |> Array.toList
        |> String.concat "; "
        |> Ok

let joinCoderIntents (intents: obj) : Result<string, string> =
    if not (isArray intents) then
        Error "Invalid LLM input for coder: intents must be an array"
    else
        let labels = ResizeArray<string>()
        let mutable error = None

        for item in intents :?> obj array do
            if error.IsNone then
                let pair = item :?> obj array
                if pair.Length = 0 || not (typeIs pair.[0] "string") then
                    error <- Some "Invalid LLM input for coder: each intent must start with a string"
                else
                    labels.Add(string pair.[0])

        match error with
        | Some message -> Error message
        | None -> labels.ToArray() |> Array.toList |> String.concat "; " |> Ok

/// UI label is cosmetic (stripped before the LLM sees the schema). Validation
/// errors must surface to the LLM via the tool result, so never emit them here.
let uiLabelForTool (tool: string) (args: obj) : string option =
    if tool = "coder" then
        match joinCoderIntents (get args "intents") with Ok label -> Some label | Error _ -> None
    elif tool = "reader" then
        match joinReaderIntents (get args "intents") with Ok label -> Some label | Error _ -> None
    else
        None

let setUiLabel (args: obj) (tool: string) : unit =
    match uiLabelForTool tool args with
    | Some label -> setKey args "_ui" (box label)
    | None -> ()
