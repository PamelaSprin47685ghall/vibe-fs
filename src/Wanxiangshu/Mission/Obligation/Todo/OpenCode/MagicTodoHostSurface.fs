namespace Wanxiangshu.Mission.Obligation.Todo.OpenCode

open System
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Participant.Provider

/// JS-native owner for the Magic Todo Host boundary.
/// Provider input and compatibility rows cross as plain objects; Host codec
/// validation and one-way sink projection remain production-owned.
[<RequireQualifiedAccess>]
module MagicTodoHostSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private decodedInput (input: MagicTodo.TodoWriteInput) : obj =
        box
            {| planComplete = input.PlanComplete
               workingOn = input.WorkingOn
               obligations =
                input.Obligations
                |> List.map (fun item ->
                    box
                        {| name = item.Name
                           horizon = MagicTodo.ObligationHorizon.wire item.Horizon
                           work = item.Work |})
                |> List.toArray |}

    let decodeInput (args: obj) : obj =
        match MagicTodoHostCodec.tryDecodeInput args with
        | Ok input ->
            box
                {| ok = true
                   value = decodedInput input |}
        | Error error -> box {| ok = false; error = error |}

    let decodeInputOrReject (args: obj) : obj =
        MagicTodoHostCodec.decodeInputOrReject args |> decodedInput

    let isProviderInputRejection (error: obj) =
        MagicTodoHostCodec.isProviderInputRejection error

    let projectCompatibilityRows (workingOn: string) (obligations: obj array) : obj array =
        obligations
        |> Array.toList
        |> List.map (fun row ->
            let horizonText = text (row?horizon)

            let horizon =
                if String.IsNullOrWhiteSpace horizonText then
                    MagicTodo.ObligationHorizon.Near
                else
                    horizonText
                    |> MagicTodo.ObligationHorizon.tryParse
                    |> Option.defaultWith (fun () -> invalidArg "horizon" "expected near, mid, or far")

            let item: MagicTodo.Obligation =
                { Name = text (row?name)
                  Horizon = horizon
                  Work = text (row?work) }

            item)
        |> MagicTodoSurface.obligationsToCompatibilityRows workingOn
        |> List.map (fun row ->
            box
                {| content = row.Content
                   status = row.Status
                   priority = row.Priority |})
        |> List.toArray

    let canonicalInput (args: obj) : string = MagicTodoHostCodec.canonicalInput args

    let canonicalInputDigest (sha256: string -> string) (args: obj) : string =
        MagicTodoHostCodec.canonicalInputDigest sha256 args

    let replaceCompatibilityArgs (output: obj) (rows: obj array) : unit =
        let compatibilityRows =
            if isNull rows then
                []
            else
                rows
                |> Array.toList
                |> List.map (fun row ->
                    let row: MagicTodoSurface.CompatibilityTodoRow =
                        { Content = text (row?content)
                          Status = text (row?status)
                          Priority = text (row?priority) }

                    row)

        MagicTodoHostCodec.replaceCompatibilityArgs output compatibilityRows

    let replaceEnrichedResult (output: obj) (text: string) : unit =
        MagicTodoHostCodec.replaceEnrichedResult output text

    let applyDefinition (output: obj) : unit =
        MagicTodoHostCodec.applyDefinition ProviderLanguage.English output
