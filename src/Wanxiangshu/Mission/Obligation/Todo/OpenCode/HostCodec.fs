namespace Wanxiangshu.Mission.Obligation.Todo.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.OpenCode
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Participant.Provider

/// The only raw Host boundary for the GrandRewrite Magic Todo account.
/// Provider input is `{ planComplete: bool, workingOn: string, obligations: [{ name, horizon, work }] }`;
/// the built-in Host executor still receives its legacy `{ todos: [{ content,status,priority }] }`
/// sink shape. New provider semantics never round-trip through that sink.
module MagicTodoHostCodec =

    type ProviderInputRejection(message: string) =
        inherit Exception(message)

    let isProviderInputRejection (error: obj) = error :? ProviderInputRejection

    [<Emit("Array.isArray($0)")>]
    let private isArray (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isString (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'boolean'")>]
    let private isBoolean (value: obj) : bool = jsNative

    [<Emit("JSON.parse($0)")>]
    let private parseJson (json: string) : obj = jsNative

    let private nonEmptyNameOrText (field: string) (text: string) : Result<string, string> =
        if field = "name" && String.IsNullOrWhiteSpace text then
            Error "todowrite obligation.name must be a non-empty string"
        else
            Ok text

    let private requiredTextValue (field: string) (value: obj) : Result<string, string> =
        if isString value then
            nonEmptyNameOrText field (unbox<string> value)
        else
            Error(sprintf "todowrite.%s must be a string" field)

    let private requiredText (row: obj) (field: string) : Result<string, string> =
        if isNull row || isNull row?(field) then
            Error(sprintf "todowrite obligation item requires field '%s'" field)
        else
            requiredTextValue field (row?(field))

    let private requiredHorizon (row: obj) : Result<MagicTodo.ObligationHorizon, string> =
        requiredText row "horizon"
        |> Result.bind (fun value ->
            match MagicTodo.ObligationHorizon.tryParse value with
            | Some horizon -> Ok horizon
            | None -> Error "todowrite.horizon must be one of near, mid, far")

    let private decodeObligationRow (row: obj) : Result<MagicTodo.Obligation, string> =
        match requiredText row "name", requiredHorizon row, requiredText row "work" with
        | Ok name, Ok horizon, Ok work ->
            Ok
                { Name = name
                  Horizon = horizon
                  Work = work }
        | Error error, _, _
        | _, Error error, _
        | _, _, Error error -> Error error

    let private finalizeDecoded
        (args: obj)
        (workingOn: string)
        (acc: MagicTodo.Obligation list)
        : Result<MagicTodo.TodoWriteInput, string> =
        let obligations = List.rev acc

        let input: MagicTodo.TodoWriteInput =
            { PlanComplete = unbox<bool> args?planComplete
              WorkingOn = workingOn
              Obligations = obligations }

        match MagicTodo.validateTodoWriteInput input with
        | Ok decoded -> Ok decoded
        | Error _ -> Error "todowrite input failed semantic validation"

    let rec private decodeRows
        (args: obj)
        (workingOn: string)
        (remaining: obj list)
        (acc: MagicTodo.Obligation list)
        (seen: Set<string>)
        : Result<MagicTodo.TodoWriteInput, string> =
        match remaining with
        | [] -> finalizeDecoded args workingOn acc
        | row :: tail -> decodeNextRow args workingOn tail acc seen row

    and private decodeNextRow
        (args: obj)
        (workingOn: string)
        (tail: obj list)
        (acc: MagicTodo.Obligation list)
        (seen: Set<string>)
        (row: obj)
        =
        match decodeObligationRow row with
        | Error error -> Error error
        | Ok obligation -> decodeRowWithUniqueness args workingOn tail acc seen obligation

    and private decodeRowWithUniqueness
        (args: obj)
        (workingOn: string)
        (tail: obj list)
        (acc: MagicTodo.Obligation list)
        (seen: Set<string>)
        (obligation: MagicTodo.Obligation)
        =
        if Set.contains obligation.Name seen then
            Error(sprintf "todowrite duplicate obligation name '%s'" obligation.Name)
        else
            decodeRows args workingOn tail (obligation :: acc) (Set.add obligation.Name seen)

    let tryDecodeInput (args: obj) : Result<MagicTodo.TodoWriteInput, string> =
        if isNull args || isNull args?planComplete then
            Error "todowrite.planComplete is required"
        elif not (isBoolean args?planComplete) then
            Error "todowrite.planComplete must be a boolean"
        elif isNull args?workingOn then
            Error "todowrite.workingOn is required"
        elif not (isString args?workingOn) then
            Error "todowrite.workingOn must be a string"
        elif isNull args?obligations then
            Error "todowrite.obligations is required"
        elif not (isArray args?obligations) then
            Error "todowrite.obligations must be an array"
        else
            let workingOn = unbox<string> args?workingOn
            let rows = unbox<obj array> args?obligations

            decodeRows args workingOn (Array.toList rows) [] Set.empty

    let decodeInputOrReject (args: obj) : MagicTodo.TodoWriteInput =
        match tryDecodeInput args with
        | Ok input -> input
        | Error reason -> raise (ProviderInputRejection reason)

    let canonicalInput (args: obj) : string =
        Wanxiangshu.Foundation.CanonicalJson.canonicalJson args

    let canonicalInputDigest (sha256: string -> string) (args: obj) : string = canonicalInput args |> sha256

    [<Emit("Object.defineProperty($0, 'todos', { value: $1, enumerable: false, configurable: true, writable: true })")>]
    let private defineCompatibilityTodos (target: obj) (todos: obj) : unit = jsNative

    /// HOST-019: expose the V1 compatibility view without changing the provider
    /// wire that the Host still needs to materialize. `todos` is deliberately
    /// non-enumerable: Effect Schema can decode it, while JSON persistence keeps
    /// the original enumerable `obligations` bytes.
    let replaceCompatibilityArgs (output: obj) (rows: MagicTodoSurface.CompatibilityTodoRow list) =
        let args: obj = output?args

        if isNull args then
            Diagnostic.fatal
                "magic-todo-infrastructure-failed"
                [ "result", "todowrite before hook output.args is required" ]

            failwith "unreachable after Diagnostic.fatal"

        let todos =
            rows
            |> List.map (fun row ->
                createObj
                    [ "content", box row.Content
                      "status", box row.Status
                      "priority", box row.Priority ])
            |> List.toArray

        defineCompatibilityTodos args (box todos)

    let replaceEnrichedResult (output: obj) (text: string) = output?output <- box text

    let private applyDescriptions (lang: ProviderLanguage) (schema: obj) =
        schema?properties?planComplete?description <-
            box (ProviderProse.render lang MagicTodoSurface.Path.PlanCompleteDescription Map.empty)

        schema?properties?workingOn?description <-
            box (ProviderProse.render lang MagicTodoSurface.Path.WorkingOnDescription Map.empty)

        let items: obj = schema?properties?obligations?items
        let properties: obj = items?properties

        properties?name?description <-
            box (ProviderProse.render lang MagicTodoSurface.Path.ObligationNameDescription Map.empty)

        properties?horizon?description <-
            box (ProviderProse.render lang MagicTodoSurface.Path.ObligationHorizonDescription Map.empty)

        properties?work?description <-
            box (ProviderProse.render lang MagicTodoSurface.Path.ObligationWorkDescription Map.empty)

    let applyDefinition (lang: ProviderLanguage) (output: obj) =
        let parameters = parseJson MagicTodoSurface.todoWriteJsonSchema
        let jsonSchema = parseJson MagicTodoSurface.todoWriteJsonSchema

        applyDescriptions lang parameters
        applyDescriptions lang jsonSchema

        output?description <- box (ProviderProse.render lang MagicTodoSurface.Path.TodoWriteDescription Map.empty)

        output?parameters <- parameters
        output?jsonSchema <- jsonSchema
