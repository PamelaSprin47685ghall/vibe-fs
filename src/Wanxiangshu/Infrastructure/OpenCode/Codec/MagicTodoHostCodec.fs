namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain

/// The only raw Host boundary for Magic Todo V2 arguments and V1 compatibility
/// output. Domain code receives tagged `RawTodoFields`; the built-in executor
/// receives only its legacy `{ todos: [{ content, status, priority }] }` shape.
module MagicTodoHostCodec =

    [<Emit("Array.isArray($0)")>]
    let private isArray (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isString (value: obj) : bool = jsNative

    [<Emit("JSON.parse($0)")>]
    let private parseJson (json: string) : obj = jsNative

    let private optionalText (row: obj) (field: string) : Result<string option, string> =
        if isNull row || isNull row?(field) then
            Ok None
        else
            let value = row?(field)

            if isString value then
                Ok(Some(unbox<string> value))
            else
                Error(sprintf "todowrite.%s must be a string" field)

    let private decodeRow (row: obj) : Result<MagicTodoSurface.RawTodoFields, string> =
        match optionalText row "kind", optionalText row "id", optionalText row "content", optionalText row "status", optionalText row "priority" with
        | Ok kind, Ok id, Ok content, Ok status, Ok priority ->
            Ok
                { Kind = kind
                  Id = id
                  Content = content
                  Status = status
                  Priority = priority }
        | Error error, _, _, _, _
        | _, Error error, _, _, _
        | _, _, Error error, _, _
        | _, _, _, Error error, _
        | _, _, _, _, Error error -> Error error

    let tryDecodeV2 (args: obj) : Result<MagicTodoSurface.RawTodoFields list, string> =
        if isNull args || isNull args?todos then
            Error "todowrite.todos is required"
        elif not (isArray args?todos) then
            Error "todowrite.todos must be an array"
        else
            let rows = unbox<obj array> args?todos

            let rec decode remaining acc =
                match remaining with
                | [] -> Ok(List.rev acc)
                | row :: tail ->
                    match decodeRow row with
                    | Ok decoded -> decode tail (decoded :: acc)
                    | Error error -> Error error

            decode (Array.toList rows) []

    let canonicalInput (args: obj) : string =
        CanonicalJson.canonicalJson args

    let canonicalInputDigest (sha256: string -> string) (args: obj) : string =
        canonicalInput args |> sha256

    let replaceCompatibilityArgs (output: obj) (rows: MagicTodoSurface.CompatibilityTodoRow list) =
        let todos =
            rows
            |> List.map (fun row ->
                createObj
                    [ "content", box row.Content
                      "status", box row.Status
                      "priority", box row.Priority ])
            |> List.toArray

        output?args <- createObj [ "todos", box todos ]

    let replaceEnrichedResult (output: obj) (text: string) =
        output?output <- box text

    let applyDefinition (output: obj) =
        output?description <- box MagicTodoSurface.TodoWriteDefinitionDescription
        output?parameters <- parseJson MagicTodoSurface.todoWriteJsonSchema
