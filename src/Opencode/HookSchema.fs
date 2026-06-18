module VibeFs.Opencode.HookSchema

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Opencode.MagicTodo

let private joinReaderIntents (intents: obj) : Result<string, string> =
    if not (Dyn.isArray intents) then Result.Error "Invalid LLM input for reader: intents must be an array of strings"
    else intents :?> obj array |> Array.map string |> Array.toList |> String.concat "; " |> Result.Ok

let private joinCoderIntents (intents: obj) : Result<string, string> =
    if not (Dyn.isArray intents) then Result.Error "Invalid LLM input for coder: intents must be an array"
    else
        let labels = ResizeArray<string>()
        let mutable error = None
        for item in intents :?> obj array do
            if error.IsNone then
                let pair = item :?> obj array
                if pair.Length = 0 || not (Dyn.typeIs pair.[0] "string") then error <- Some "Invalid LLM input for coder: each intent must start with a string"
                else labels.Add(string pair.[0])
        match error with
        | Some message -> Result.Error message
        | None -> labels.ToArray() |> Array.toList |> String.concat "; " |> Result.Ok

let setUiLabel (setKey: obj -> string -> obj -> unit) (args: obj) (tool: string) : unit =
    let labelResult =
        match tool with
        | "coder" -> joinCoderIntents (Dyn.get args "intents")
        | "reader" -> joinReaderIntents (Dyn.get args "intents")
        | _ -> Result.Error ""
    match labelResult with
    | Result.Ok label when label <> "" -> setKey args "_ui" (box label)
    | _ -> ()

let private objectKeys (o: obj) : string array =
    JS.Constructors.Object.keys(o) |> Seq.toArray

let private requiredWithoutUi (required: obj) : obj =
    if not (isArray required) then required
    else required :?> obj array |> Array.choose (fun item -> let key = string item in if key = "_ui" then None else Some(box key)) |> box

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
