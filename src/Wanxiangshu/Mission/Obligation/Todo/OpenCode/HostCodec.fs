namespace Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.Persistence

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Resources
open Wanxiangshu.Resources

/// The only raw Host boundary for the GrandRewrite Magic Todo account.
/// Provider input is `{ planComplete: bool, obligations: [{ name, work }] }`; the built-in Host
/// executor still receives its legacy `{ todos: [{ content,status,priority }] }`
/// sink shape. New provider semantics never round-trip through that sink.
module MagicTodoHostCodec =

    [<Emit("Array.isArray($0)")>]
    let private isArray (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isString (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'boolean'")>]
    let private isBoolean (value: obj) : bool = jsNative

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

    let tryDecodeInput (args: obj) : Result<MagicTodo.TodoWriteInput, string> =
        if isNull args || isNull args?planComplete then
            Error "todowrite.planComplete is required"
        elif not (isBoolean args?planComplete) then
            Error "todowrite.planComplete must be a boolean"
        elif isNull args?obligations then
            Error "todowrite.obligations is required"
        elif not (isArray args?obligations) then
            Error "todowrite.obligations must be an array"
        else
            let rows = unbox<obj array> args?obligations

            let rec decode remaining acc seen =
                match remaining with
                | [] ->
                    let decoded: MagicTodo.TodoWriteInput =
                        { PlanComplete = unbox<bool> args?planComplete
                          Obligations = List.rev acc }

                    Ok decoded
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

        let items: obj = schema?properties?obligations?items
        let properties: obj = items?properties

        properties?name?description <-
            box (ProviderProse.render lang MagicTodoSurface.Path.ObligationNameDescription Map.empty)

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
