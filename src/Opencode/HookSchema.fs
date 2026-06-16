module VibeFs.Opencode.HookSchema

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn

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

let withUiParameterStripped (parameters: obj) : obj =
    let properties = get parameters "properties"
    if isNullish properties then parameters
    else
        let nextProperties =
            objectKeys properties
            |> Array.choose (fun key ->
                if key = "_ui" then None
                else Some (key, get properties key))
            |> createObj

        createObj
            [ for key in objectKeys parameters do
                  if key = "properties" then
                      yield key, nextProperties
                  elif key = "required" then
                      yield key, requiredWithoutUi (get parameters "required")
                  else
                      yield key, get parameters key ]

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
                let firstItem = get item "0"
                if not (typeIs firstItem "string") then
                    error <- Some "Invalid LLM input for coder: each intent must start with a string"
                else
                    labels.Add(string firstItem)

        match error with
        | Some message -> Error message
        | None -> labels.ToArray() |> Array.toList |> String.concat "; " |> Ok

let uiLabelForTool (tool: string) (args: obj) : string option =
    let existingUi = get args "_ui"
    if not (isNullish existingUi) && not (typeIs existingUi "string") then
        Some $"Invalid LLM input for {tool}: _ui must be a string, received {jsType existingUi}"
    elif tool = "coder" then
        match joinCoderIntents (get args "intents") with
        | Ok label -> Some label
        | Error message -> Some message
    elif tool = "reader" then
        match joinReaderIntents (get args "intents") with
        | Ok label -> Some label
        | Error message -> Some message
    else
        None

let setUiLabel (args: obj) (tool: string) : unit =
    match uiLabelForTool tool args with
    | Some label -> setKey args "_ui" (box label)
    | None -> ()
