namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Repository.Programming.Js.OpenCode

/// Opaque workflow outcome. The semantic observation functions expose the
/// commit report and failure algebra without leaking JsToolOutcome's union.
[<RequireQualifiedAccess>]
module JsWorkflowSurface =

    type private JsWorkflowOutcomeHandle(outcome: JsToolWorkflow.JsToolOutcome) =
        member _.Value = outcome

    let private outcomeOf (value: obj) =
        (unbox<JsWorkflowOutcomeHandle> value).Value

    [<Emit("$0($1,$2)")>]
    let private apply2 (callback: obj) (readPaths: obj) (effectPaths: obj) : obj = jsNative

    [<Emit("Promise.resolve($0)")>]
    let private promiseOf (value: obj) : JS.Promise<obj> = jsNative

    let private observationOf callback readPaths effectPaths =
        task {
            let! _ =
                unbox<Task<obj>> (
                    promiseOf (apply2 callback (box (List.toArray readPaths)) (box (List.toArray effectPaths)))
                )

            return ()
        }

    let private runWithObservation
        (workspaceRoot: string)
        (role: string)
        (language: string)
        (program: string)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        (store: obj)
        (fileAccessObservation: JsToolWorkflow.FileAccessObservation option)
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
                    match fileAccessObservation with
                    | None ->
                        JsToolWorkflow.run
                            workspaceRoot
                            surface.BaseClassSource
                            program
                            deadlineMs
                            deadlineEpochMs
                            outputBoundBytes
                            persistence
                    | Some observe ->
                        JsToolWorkflow.runWithFileAccessObservation
                            workspaceRoot
                            surface.BaseClassSource
                            program
                            deadlineMs
                            deadlineEpochMs
                            outputBoundBytes
                            persistence
                            observe

                return box (JsWorkflowOutcomeHandle outcome)
        }

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
        runWithObservation workspaceRoot role language program deadlineMs deadlineEpochMs outputBoundBytes store None

    let runObserved
        (workspaceRoot: string)
        (role: string)
        (language: string)
        (program: string)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        (store: obj)
        (fileAccessObservation: obj)
        : Task<obj> =
        runWithObservation
            workspaceRoot
            role
            language
            program
            deadlineMs
            deadlineEpochMs
            outputBoundBytes
            store
            (Some(observationOf fileAccessObservation))

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

    let render (value: obj) : string = JsToolsResult.render (outcomeOf value)
