namespace Wanxiangshu.Sphinx

// WHAT[EPI-030]: pure Current fold for legacy Sphinx inquiries. No IO, no
// clock, no codec: the spine decodes durable envelopes into LegacyEnvelopeInput
// and this module only folds them into per-handle cursors.
[<RequireQualifiedAccess>]
module LegacyIntegrator =
    type LegacyInquiryCursor =
        { Revision: int
          Question: string
          Raws: obj list }

    type SphinxLegacyCurrent = Map<string, LegacyInquiryCursor>

    type LegacyObservationFields =
        { Handle: string
          Tool: string
          ArgsJson: string
          Revision: int
          Question: string }

    type LegacyEnvelopeInput =
        | LegacyObservation of LegacyObservationFields
        | OtherSphinxEvent of eventType: string

    let empty: SphinxLegacyCurrent = Map.empty

    let private isBlank (value: string) = isNull value || value.Trim() = ""

    let private newerRevision (existing: LegacyInquiryCursor) (fields: LegacyObservationFields) : int =
        if fields.Revision > existing.Revision then
            fields.Revision
        else
            existing.Revision

    let private keptQuestion (existing: LegacyInquiryCursor) (fields: LegacyObservationFields) : string =
        if isBlank existing.Question then
            fields.Question
        else
            existing.Question

    let private cursorFor
        (existing: LegacyInquiryCursor option)
        (fields: LegacyObservationFields)
        (raw: obj)
        : LegacyInquiryCursor =
        match existing with
        | None ->
            { Revision = fields.Revision
              Question = fields.Question
              Raws = [ raw ] }
        | Some(prior: LegacyInquiryCursor) ->
            { Revision = newerRevision prior fields
              Question = keptQuestion prior fields
              Raws = prior.Raws @ [ raw ] }

    let private appendObservation
        (current: SphinxLegacyCurrent)
        (fields: LegacyObservationFields)
        : Result<SphinxLegacyCurrent, string> =
        if isBlank fields.Handle then
            Error "Sphinx legacy observation needs a non-blank handle"
        elif fields.Revision < 0 then
            Error(sprintf "Sphinx legacy observation needs a non-negative revision: %d" fields.Revision)
        else
            let raw =
                box
                    {| handle = fields.Handle
                       tool = fields.Tool
                       argsJson = fields.ArgsJson
                       revision = fields.Revision |}

            Ok(Map.add fields.Handle (cursorFor (Map.tryFind fields.Handle current) fields raw) current)

    let private applyInput
        (current: SphinxLegacyCurrent)
        (input: LegacyEnvelopeInput)
        : Result<SphinxLegacyCurrent, string> =
        match input with
        | LegacyEnvelopeInput.OtherSphinxEvent _ -> Ok current
        | LegacyEnvelopeInput.LegacyObservation(fields: LegacyObservationFields) -> appendObservation current fields

    let applyOne (current: SphinxLegacyCurrent) (envelope: obj) : Result<SphinxLegacyCurrent, string> =
        match envelope with
        | :? LegacyEnvelopeInput as input -> applyInput current input
        | _ -> Error "Sphinx legacy fold received a non-envelope input"
