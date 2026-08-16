namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks
open Wanxiangshu.Repository.Programming.Js.OpenCode

/// Opaque workflow outcome. The semantic observation functions expose the
/// commit report and failure algebra without leaking JsToolOutcome's union.
type private JsWorkflowOutcomeHandle(outcome: JsToolWorkflow.JsToolOutcome) =
    member _.Value = outcome

[<RequireQualifiedAccess>]
module JsWorkflowSurface =

    let private outcomeOf (value: obj) =
        (unbox<JsWorkflowOutcomeHandle> value).Value

    let run
        (workspaceRoot: string)
        (role: string)
        (language: string)
        (program: string)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        (store: obj)
        : Task<obj> =
        task {
            match JsGeneratorSurface.typedRole role language with
            | None -> return null
            | Some surface ->
                let persistence =
                    if isNull store then
                        None
                    else
                        Some(JsTransactionSurface.persistenceOf store)

                let! outcome =
                    JsToolWorkflow.run
                        workspaceRoot
                        surface.BaseClassSource
                        program
                        deadlineMs
                        deadlineEpochMs
                        outputBoundBytes
                        persistence

                return box (JsWorkflowOutcomeHandle outcome)
        }

    let caseName (value: obj) : string =
        match outcomeOf value with
        | JsToolWorkflow.JsToolOutcome.Succeeded _ -> "Succeeded"
        | JsToolWorkflow.JsToolOutcome.Failed _ -> "Failed"

    let rewritten (value: obj) : string array =
        match outcomeOf value with
        | JsToolWorkflow.JsToolOutcome.Succeeded(_, paths, _) -> paths |> List.toArray
        | JsToolWorkflow.JsToolOutcome.Failed _ -> [||]

    let created (value: obj) : string array =
        match outcomeOf value with
        | JsToolWorkflow.JsToolOutcome.Succeeded(_, _, paths) -> paths |> List.toArray
        | JsToolWorkflow.JsToolOutcome.Failed _ -> [||]

    let failureCode (value: obj) : obj =
        match outcomeOf value with
        | JsToolWorkflow.JsToolOutcome.Succeeded _ -> null
        | JsToolWorkflow.JsToolOutcome.Failed failure -> box (JsFailure.code failure)

    let failureReason (value: obj) : obj =
        match outcomeOf value with
        | JsToolWorkflow.JsToolOutcome.Succeeded _ -> null
        | JsToolWorkflow.JsToolOutcome.Failed failure -> box (JsFailure.reason failure)

    let render (value: obj) : string =
        JsToolsResult.render (outcomeOf value)
