namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Resources

/// The only raw Host boundary for the GrandRewrite Magic Todo account.
/// Provider input is `{ obligations: [{ name, work }] }`; the built-in Host
/// executor still receives its legacy `{ todos: [{ content,status,priority }] }`
/// sink shape. New provider semantics never round-trip through that sink.
module MagicTodoHostCodec =

    [<Emit("Array.isArray($0)")>]
    let private isArray (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isString (value: obj) : bool = jsNative

    [<Emit("JSON.parse($0)")>]
    let private parseJson (json: string) : obj = jsNative

    let private requiredText (row: obj) (field: string) : Result<string, string> =
        if isNull row || isNull row?(field) then
            Error(sprintf "todowrite obligation item requires field '%s'" field)
        else
            let value = row?(field)

            if isString value then
                let text = unbox<string> value

                if field = "name" && String.IsNullOrWhiteSpace text then
                    Error "todowrite obligation.name must be a non-empty string"
                else
                    Ok text
            else
                Error(sprintf "todowrite.%s must be a string" field)

    let private decodeObligationRow (row: obj) : Result<MagicTodo.Obligation, string> =
        match requiredText row "name", requiredText row "work" with
        | Ok name, Ok work -> Ok { Name = name; Work = work }
        | Error error, _
        | _, Error error -> Error error

    let tryDecodeObligations (args: obj) : Result<MagicTodo.ObligationList, string> =
        if isNull args || isNull args?obligations then
            Error "todowrite.obligations is required"
        elif not (isArray args?obligations) then
            Error "todowrite.obligations must be an array"
        else
            let rows = unbox<obj array> args?obligations

            let rec decode remaining acc seen =
                match remaining with
                | [] -> Ok(List.rev acc)
                | row :: tail ->
                    match decodeObligationRow row with
                    | Error error -> Error error
                    | Ok obligation ->
                        if Set.contains obligation.Name seen then
                            Error(sprintf "todowrite duplicate obligation name '%s'" obligation.Name)
                        else
                            decode tail (obligation :: acc) (Set.add obligation.Name seen)

            decode (Array.toList rows) [] Set.empty

    let canonicalInput (args: obj) : string = CanonicalJson.canonicalJson args

    let canonicalInputDigest (sha256: string -> string) (args: obj) : string = canonicalInput args |> sha256

    [<Emit("delete $0[$1]")>]
    let private deleteField (target: obj) (name: string) : unit = jsNative

    /// HOST-019: mutate fields on the original args object. Rebinding output.args
    /// is invisible to the Host executor and would also weaken alias canaries.
    let replaceCompatibilityArgs (output: obj) (rows: MagicTodoSurface.CompatibilityTodoRow list) =
        let args: obj = output?args

        if isNull args then
            invalidOp "todowrite before hook output.args is required"

        let todos =
            rows
            |> List.map (fun row ->
                createObj
                    [ "content", box row.Content
                      "status", box row.Status
                      "priority", box row.Priority ])
            |> List.toArray

        args?todos <- box todos
        deleteField args "obligations"

    let replaceEnrichedResult (output: obj) (text: string) = output?output <- box text

    let applyDefinition (lang: ProviderLanguage) (output: obj) =
        let parameters = parseJson MagicTodoSurface.todoWriteJsonSchema
        let jsonSchema = parseJson MagicTodoSurface.todoWriteJsonSchema

        output?description <- box (ProviderProse.render lang MagicTodoSurface.Path.TodoWriteDescription Map.empty)

        output?parameters <- parameters
        output?jsonSchema <- jsonSchema
