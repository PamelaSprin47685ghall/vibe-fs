namespace Wanxiangshu.Participant.Cognition

open Fable.Core.JsInterop

/// JS-native boundary for the `assume` argument admission.
///
/// The whole point of this surface is that the refusal reasons are reachable without
/// a Host: a caller must be able to prove that a retired ledger argument is refused
/// before any jq program runs.
[<RequireQualifiedAccess>]
module AdmissionSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let tryDecode (args: obj) : obj =
        match AssumeAdmission.tryDecode args with
        | Ok(update, todos) ->
            box
                {| ok = true
                   value =
                    {| update = update
                       todos =
                        todos
                        |> List.map (fun (content, status, priority) ->
                            box
                                {| content = content
                                   status = TodoStatus.wire status
                                   priority = TodoPriority.wire priority |})
                        |> List.toArray |} |}
        | Error rejection ->
            box
                {| ok = false
                   error = AssumeAdmission.Rejection.message rejection |}

    /// The exact shape of a wrongly-typed argument, so a caller can tell "field
    /// missing" from "field is the wrong type" without parsing the message.
    let rejectsBecause (args: obj) : string =
        match AssumeAdmission.tryDecode args with
        | Ok _ -> ""
        | Error rejection ->
            match rejection with
            | AssumeAdmission.Rejection.MissingUpdate -> "MissingUpdate"
            | AssumeAdmission.Rejection.UpdateNotString -> "UpdateNotString"
            | AssumeAdmission.Rejection.MissingTodos -> "MissingTodos"
            | AssumeAdmission.Rejection.TodosNotArray -> "TodosNotArray"
            | AssumeAdmission.Rejection.TodoRowNotObject _ -> "TodoRowNotObject"
            | AssumeAdmission.Rejection.TodoContentMissing _ -> "TodoContentMissing"
            | AssumeAdmission.Rejection.TodoContentNotString _ -> "TodoContentNotString"
            | AssumeAdmission.Rejection.TodoContentBlank _ -> "TodoContentBlank"
            | AssumeAdmission.Rejection.TodoStatusMissing _ -> "TodoStatusMissing"
            | AssumeAdmission.Rejection.TodoStatusUnknown _ -> "TodoStatusUnknown"
            | AssumeAdmission.Rejection.TodoPriorityUnknown _ -> "TodoPriorityUnknown"
            | AssumeAdmission.Rejection.UnknownArgument _ -> "UnknownArgument"
