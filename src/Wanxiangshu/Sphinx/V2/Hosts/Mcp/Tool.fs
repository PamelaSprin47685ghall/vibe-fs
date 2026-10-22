namespace Wanxiangshu.Sphinx.V2.Hosts

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Wire

/// The MCP tool contract: one strict decoder per tool.
///
/// WHAT[sphinx-v2-036]: the five public tools and their argument shapes.
/// Required arguments are not defaulted; steps and a failure reason are optional.
///
/// WHAT[sphinx-v2-018]: a work submit decodes the answer bytes and nothing else. A
/// certificate patch, a budget debit, an event write or a goal revision in the same
/// call is refused by name, because dropping it silently would let a worker believe
/// it changed something it did not.
///
/// WHAT[sphinx-v2-009]: decoding is the whole job here. This module never reads state
/// and never decides what a call does next.
type ToolRefusal =
    { Code: string
      Path: string
      Message: string }

/// One step of the plan the user selected. The Runtime checks its shape and its
/// resources; it never re-ranks it.
type PlanStep =
    { Id: string
      Title: string
      DependsOn: string list }

type StartArgs =
    { CommandId: string
      GoalText: string
      Constraints: string list
      MaterialRefs: string list
      AuthorizationRef: string
      ProfileRef: string
      Steps: PlanStep list }

/// What the worker reports about the step it holds.
[<RequireQualifiedAccess>]
type WorkOutcome =
    | Succeeded
    | Failed

type WorkSubmitArgs =
    { CommandId: string
      InquiryId: string
      WorkId: string
      Attempt: Attempt
      Fence: Fence
      CanonicalResult: string
      Outcome: WorkOutcome
      Reason: string option }

type StatusArgs = { InquiryId: string }

type CancelArgs =
    { CommandId: string
      InquiryId: string
      Reason: string }

[<RequireQualifiedAccess>]
type ExportMode =
    | Summary
    | Full

type ExportArgs = { InquiryId: string; Mode: ExportMode }

[<RequireQualifiedAccess>]
module Tool =

    let private fromWire (error: WireError) : ToolRefusal =
        { Code = error.Code
          Path = error.Path
          Message = error.Message }

    let private fromText (path: string) (message: string) : ToolRefusal =
        { Code = "INVALID_SCHEMA"
          Path = path
          Message = message }

    [<Emit("Object.prototype.hasOwnProperty.call($0, $1)")>]
    let private hasField (raw: obj) (name: string) : bool = jsNative

    [<Emit("$0[$1]")>]
    let private field (raw: obj) (name: string) : obj = jsNative

    let refusalCode (refusal: ToolRefusal) : string = refusal.Code

    let refusalPath (refusal: ToolRefusal) : string = refusal.Path

    let refusalMessage (refusal: ToolRefusal) : string = refusal.Message

    let private inquiryIdField raw =
        Decode.stringField raw "inquiryId"
        |> Result.mapError fromWire
        |> Result.bind (fun value ->
            InquiryId.tryCreate value
            |> Result.map InquiryId.value
            |> Result.mapError (fromText "inquiryId"))

    let private materialRefsField raw =
        let validate references =
            let rec collect index accepted remaining =
                match remaining with
                | [] -> Ok(List.rev accepted)
                | reference :: rest ->
                    ArtifactRef.tryCreate reference
                    |> Result.mapError (fromText (sprintf "materialRefs[%d]" index))
                    |> Result.bind (fun reference -> collect (index + 1) (ArtifactRef.value reference :: accepted) rest)

            collect 0 [] references

        Decode.uniqueStringListField raw "materialRefs"
        |> Result.mapError fromWire
        |> Result.bind validate

    /// One plan step: an id, a description and the ids it depends on. Decoding only
    /// checks shape; the Runtime resolves the dependency graph and refuses a cycle.
    let private stepsField raw : Result<PlanStep list, ToolRefusal> =
        if not (hasField raw "steps") then
            Ok []
        else
            let stepsValue = field raw "steps"

            let isRecord =
                emitJsExpr stepsValue "Array.isArray($0)"

            if not isRecord then
                Error(fromText "steps" "steps must be an array of {id, title, dependsOn}")
            else
                let stepAt index (entry: obj) : Result<PlanStep, ToolRefusal> =
                    let isObject =
                        emitJsExpr entry "typeof $0 === 'object' && $0 !== null && !Array.isArray($0)"

                    if not isObject then
                        Error(fromText (sprintf "steps[%d]" index) "each step must be an object")
                    else
                        Decode.stringField entry "id"
                        |> Result.mapError fromWire
                        |> Result.bind (fun id ->
                            WorkId.tryCreate id
                            |> Result.mapError (fromText (sprintf "steps[%d].id" index))
                            |> Result.bind (fun stepId ->
                                Decode.stringField entry "title"
                                |> Result.mapError fromWire
                                |> Result.bind (fun title ->
                                    Decode.uniqueStringListField entry "dependsOn"
                                    |> Result.mapError fromWire
                                    |> Result.map (fun dependsOn ->
                                        { Id = WorkId.value stepId
                                          Title = title
                                          DependsOn = dependsOn }))))

                let raw = unbox<obj array> stepsValue

                let rec collect index accepted remaining =
                    match remaining with
                    | [] -> Ok(List.rev accepted)
                    | entry :: rest ->
                        stepAt index entry
                        |> Result.bind (fun step -> collect (index + 1) (step :: accepted) rest)

                collect 0 [] (Array.toList raw)

    let decodeStart (raw: obj) : Result<StartArgs, ToolRefusal> =
        Decode.stringField raw "commandId"
        |> Result.mapError fromWire
        |> Result.bind (fun commandId ->
            Decode.stringField raw "goalText"
            |> Result.mapError fromWire
            |> Result.bind (fun goalText ->
                Decode.uniqueStringListField raw "constraints"
                |> Result.mapError fromWire
                |> Result.bind (fun constraints ->
                    materialRefsField raw
                    |> Result.bind (fun materialRefs ->
                        Decode.stringField raw "authorizationRef"
                        |> Result.mapError fromWire
                        |> Result.bind (fun authorizationRef ->
                            Decode.stringField raw "profileRef"
                            |> Result.mapError fromWire
                            |> Result.bind (fun profileRef ->
                                stepsField raw
                                |> Result.map (fun steps ->
                                    { CommandId = commandId
                                      GoalText = goalText
                                      Constraints = constraints
                                      MaterialRefs = materialRefs
                                      AuthorizationRef = authorizationRef
                                      ProfileRef = profileRef
                                      Steps = steps })))))))

    /// The keys a worker must never smuggle in with its answer.
    let private forbiddenResultFields =
        [ "certificatePatches"
          "budgetDebit"
          "events"
          "goalRevision"
          "goalAmendment" ]

    /// 只拒绝明确夹带的变动或命令，不把所有冗余键当作非法入参。
    let private rejectReadMutation (raw: obj) : Result<unit, ToolRefusal> =
        match
            (forbiddenResultFields @ [ "command"; "commands" ])
            |> List.tryFind (fun name -> hasField raw name)
        with
        | Some name ->
            Error(
                fromText
                    name
                    (sprintf
                        "field %s requests an additional mutation or command; status and export only read an inquiry"
                        name)
            )
        | None -> Ok()

    let decodeWorkSubmit (raw: obj) : Result<WorkSubmitArgs, ToolRefusal> =
        match forbiddenResultFields |> List.tryFind (fun name -> hasField raw name) with
        | Some name ->
            Error
                { Code = "WORK_RESULT_EXCEEDS_ROLE"
                  Path = name
                  Message =
                    sprintf
                        "field %s is outside what a work result may carry: a worker submits answers, not certificates, budgets, events or goal revisions"
                        name }
        | None ->
            let outcome () =
                let commandId =
                    Decode.stringField raw "commandId" |> Result.mapError fromWire

                let inquiryId = inquiryIdField raw

                let workId =
                    Decode.stringField raw "workId" |> Result.mapError fromWire

                let attempt =
                    Decode.nonNegativeIntegerField raw "attempt"
                    |> Result.mapError fromWire
                    |> Result.bind (Attempt.tryCreate >> Result.mapError (fromText "attempt"))

                let fence =
                    Decode.stringField raw "fence"
                    |> Result.mapError fromWire
                    |> Result.bind (Fence.tryCreate >> Result.mapError (fromText "fence"))

                let outcome =
                    if hasField raw "outcome" then
                        Decode.stringField raw "outcome" |> Result.mapError fromWire
                    else
                        Ok "succeeded"

                /// A failed step has no result bytes; a blank canonical result is
                /// only lawful when the worker reports failure.
                let result commandId inquiryId workId attempt fence outcome =
                    match outcome with
                    | "succeeded" ->
                        Decode.stringField raw "canonicalResult"
                        |> Result.mapError fromWire
                        |> Result.map (fun canonicalResult ->
                            { CommandId = commandId
                              InquiryId = inquiryId
                              WorkId = workId
                              Attempt = attempt
                              Fence = fence
                              CanonicalResult = canonicalResult
                              Outcome = WorkOutcome.Succeeded
                              Reason = None })
                    | "failed" ->
                        Decode.stringField raw "reason"
                        |> Result.mapError fromWire
                        |> Result.bind (fun reason ->
                            let value = field raw "canonicalResult"
                            let isString = emitJsExpr value "typeof $0 === 'string'"

                            if not (hasField raw "canonicalResult") then
                                Ok
                                    { CommandId = commandId
                                      InquiryId = inquiryId
                                      WorkId = workId
                                      Attempt = attempt
                                      Fence = fence
                                      CanonicalResult = ""
                                      Outcome = WorkOutcome.Failed
                                      Reason = Some reason }
                            elif isString then
                                Ok
                                    { CommandId = commandId
                                      InquiryId = inquiryId
                                      WorkId = workId
                                      Attempt = attempt
                                      Fence = fence
                                      CanonicalResult = unbox<string> value
                                      Outcome = WorkOutcome.Failed
                                      Reason = Some reason }
                            else
                                Error(fromText "canonicalResult" "canonicalResult must be a string"))
                    | _ -> Error(fromText "outcome" "outcome must be succeeded or failed")

                commandId
                |> Result.bind (fun commandId ->
                    inquiryId
                    |> Result.bind (fun inquiryId ->
                        workId
                        |> Result.bind (fun workId ->
                            attempt
                            |> Result.bind (fun attempt ->
                                fence
                                |> Result.bind (fun fence ->
                                    outcome
                                    |> Result.bind (fun outcome ->
                                        result commandId inquiryId workId attempt fence outcome))))))

            outcome ()

    let decodeStatus (raw: obj) : Result<StatusArgs, ToolRefusal> =
        rejectReadMutation raw
        |> Result.bind (fun () -> inquiryIdField raw |> Result.map (fun inquiryId -> { InquiryId = inquiryId }))

    let decodeCancel (raw: obj) : Result<CancelArgs, ToolRefusal> =
        Decode.stringField raw "commandId"
        |> Result.mapError fromWire
        |> Result.bind (fun commandId ->
            inquiryIdField raw
            |> Result.bind (fun inquiryId ->
                Decode.stringField raw "reason"
                |> Result.mapError fromWire
                |> Result.map (fun reason ->
                    { CommandId = commandId
                      InquiryId = inquiryId
                      Reason = reason })))

    let decodeExport (raw: obj) : Result<ExportArgs, ToolRefusal> =
        rejectReadMutation raw
        |> Result.bind (fun () -> inquiryIdField raw)
        |> Result.bind (fun inquiryId ->
            Decode.stringField raw "mode"
            |> Result.mapError fromWire
            |> Result.bind (fun mode ->
                match mode with
                | "summary" ->
                    Ok
                        { InquiryId = inquiryId
                          Mode = ExportMode.Summary }
                | "full" ->
                    Ok
                        { InquiryId = inquiryId
                          Mode = ExportMode.Full }
                | _ ->
                    Error
                        { Code = "EXPORT_MODE_UNKNOWN"
                          Path = "mode"
                          Message = sprintf "mode must be summary or full, not %s" mode }))

