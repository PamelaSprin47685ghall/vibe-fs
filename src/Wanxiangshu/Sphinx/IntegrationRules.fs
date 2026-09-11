namespace Wanxiangshu.Sphinx

open Thoth.Json
open Wanxiangshu.Persistence.EventStore

/// Sphinx-owned canonical integration oracles, relocated from the persistence
/// spine. Rule `Name` strings, accepted event types, fold behavior, fault
/// scope and cut/reset semantics are unchanged.
[<RequireQualifiedAccess>]
module SphinxIntegrationRules =

    let private toFields
        (handle: string, tool: string, argsJson: string, revision: int, question: string)
        : LegacyIntegrator.LegacyObservationFields =
        { Handle = handle
          Tool = tool
          ArgsJson = argsJson
          Revision = revision
          Question = question }

    let private legacyDecoder =
        Decode.object (fun get ->
            (get.Required.Field "handle" Decode.string,
             get.Required.Field "tool" Decode.string,
             get.Required.Field "args_json" Decode.string,
             get.Required.Field "revision" Decode.int,
             get.Optional.Field "question" Decode.string |> Option.defaultValue ""))

    let private tryLegacyInput (envelope: EventEnvelope) : Result<LegacyIntegrator.LegacyEnvelopeInput, string> =
        if envelope.EventType <> SphinxEventTypes.LegacyObservation then
            Ok(LegacyIntegrator.LegacyEnvelopeInput.OtherSphinxEvent envelope.EventType)
        else
            Decode.fromValue "$" legacyDecoder envelope.Payload
            |> Result.map (toFields >> LegacyIntegrator.LegacyEnvelopeInput.LegacyObservation)
            |> Result.mapError (fun error -> sprintf "Sphinx legacy observation decode failed: %s" error)

    /// WHAT[EPI-030]: durable restart oracle for legacy Sphinx inquiries. It folds
    /// accepted sphinx-legacy observations into per-handle cursors; every other
    /// sphinx kind is forward-compatible vocabulary and leaves Current unchanged.
    let sphinxRule: IntegrationRule =
        { Name = "Sphinx"
          Initial = box LegacyIntegrator.empty
          FaultScope = fun _ -> "global"
          Accepts = fun envelope -> SphinxEventTypes.isSphinxEvent envelope.EventType
          Integrate =
            fun current envelope ->
                match tryLegacyInput envelope with
                | Error error -> Error error
                | Ok input ->
                    LegacyIntegrator.applyOne (unbox<LegacyIntegrator.SphinxLegacyCurrent> current) (box input)
                    |> Result.map box
                    |> Result.mapError (fun error -> sprintf "Sphinx integration rejected: %s" error)
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }

    let private genericDecoder =
        Decode.object (fun get ->
            (get.Required.Field "inquiry" Decode.string,
             get.Required.Field "kind" Decode.string,
             get.Required.Field "revision" Decode.int,
             get.Optional.Field "expectedRevision" Decode.int |> Option.defaultValue -1,
             get.Optional.Field "question" Decode.string |> Option.defaultValue "",
             get.Optional.Field "profile" Decode.string |> Option.defaultValue "",
             get.Optional.Field "executionMode" Decode.string |> Option.defaultValue "",
             get.Optional.Field "pluginsJson" Decode.string |> Option.defaultValue "null",
             get.Optional.Field "budgetJson" Decode.string |> Option.defaultValue "null",
             get.Optional.Field "resultsJson" Decode.string |> Option.defaultValue "[]"))

    let private toInput
        (
            inquiry: string,
            kind: string,
            revision: int,
            expectedRevision: int,
            question: string,
            profile: string,
            executionMode: string,
            pluginsJson: string,
            budgetJson: string,
            resultsJson: string
        ) : Result<GenericIntegrator.GenericEnvelopeInput, string> =
        match kind with
        | "started" ->
            Ok(
                GenericIntegrator.GenericStarted(
                    inquiry,
                    revision,
                    question,
                    profile,
                    executionMode,
                    pluginsJson,
                    budgetJson
                )
            )
        | "submitted" -> Ok(GenericIntegrator.GenericSubmitted(inquiry, revision, expectedRevision, resultsJson))
        | "cancelled" -> Ok(GenericIntegrator.GenericCancelled(inquiry, revision))
        | _ -> Error(sprintf "sphinx generic envelope carries an unknown kind: %s" kind)

    let private tryGenericInput (envelope: EventEnvelope) : Result<GenericIntegrator.GenericEnvelopeInput, string> =
        Decode.fromValue "$" genericDecoder envelope.Payload
        |> Result.mapError (fun error -> sprintf "Sphinx generic observation decode failed: %s" error)
        |> Result.bind toInput

    /// WHAT[EPI-019]: durable restart oracle for generic Sphinx inquiries. It folds
    /// accepted sphinx-generic transitions into per-inquiry cursors; the legacy
    /// Sphinx rule ignores this kind and leaves its own Current unchanged.
    let sphinxGenericRule: IntegrationRule =
        { Name = "SphinxGeneric"
          Initial = box GenericIntegrator.empty
          FaultScope = fun _ -> "global"
          Accepts = fun envelope -> envelope.EventType = SphinxEventTypes.GenericInquiry
          Integrate =
            fun current envelope ->
                match tryGenericInput envelope with
                | Error error -> Error error
                | Ok input ->
                    GenericIntegrator.applyOne (unbox<GenericIntegrator.SphinxGenericCurrent> current) input
                    |> Result.map box
                    |> Result.mapError (fun error -> sprintf "Sphinx generic integration rejected: %s" error)
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }

    let rules: IntegrationRule list = [ sphinxRule; sphinxGenericRule ]
