// WHAT[EPI-019]: pure Current fold for generic Sphinx inquiries. No IO, no
// clock, no codec: the spine decodes durable envelopes into
// GenericEnvelopeInput and this module only folds them into per-inquiry
// cursors with an unbroken revision chain.

namespace Wanxiangshu.Sphinx

open System

[<RequireQualifiedAccess>]
module GenericIntegrator =
    type GenericCursor =
        { Revision: int
          Cancelled: bool
          Question: string
          Profile: string
          ExecutionMode: string
          PluginsJson: string
          BudgetJson: string
          ResultsJson: string list }

    type GenericEnvelopeInput =
        | GenericStarted of
            inquiry: string *
            revision: int *
            question: string *
            profile: string *
            executionMode: string *
            pluginsJson: string *
            budgetJson: string
        | GenericSubmitted of inquiry: string * revision: int * expectedRevision: int * resultsJson: string
        | GenericCancelled of inquiry: string * revision: int

    type SphinxGenericCurrent = Map<string, GenericCursor>

    let empty: SphinxGenericCurrent = Map.empty

    let private isBlank (value: string) = isNull value || value.Trim() = ""

    let private applyStarted
        (current: SphinxGenericCurrent)
        (inquiry: string)
        (revision: int)
        (question: string)
        (profile: string)
        (executionMode: string)
        (pluginsJson: string)
        (budgetJson: string)
        : Result<SphinxGenericCurrent, string> =
        if revision <> 0 then
            Error(sprintf "sphinx generic start for %s must land on revision 0" inquiry)
        elif current |> Map.containsKey inquiry then
            Error(sprintf "sphinx generic start meets a duplicate inquiry: %s" inquiry)
        else
            Ok(
                Map.add
                    inquiry
                    { Revision = 0
                      Cancelled = false
                      Question = question
                      Profile = profile
                      ExecutionMode = executionMode
                      PluginsJson = pluginsJson
                      BudgetJson = budgetJson
                      ResultsJson = [] }
                    current
            )

    let private submitAdvanced
        (current: SphinxGenericCurrent)
        (inquiry: string)
        (cursor: GenericCursor)
        (revision: int)
        (expectedRevision: int)
        (resultsJson: string)
        : Result<SphinxGenericCurrent, string> =
        if cursor.Cancelled then
            Error(sprintf "sphinx generic submit meets a cancelled inquiry: %s" inquiry)
        elif revision <> expectedRevision + 1 then
            Error(sprintf "sphinx generic envelope %s breaks the revision chain" inquiry)
        elif expectedRevision <> cursor.Revision then
            Error(sprintf "sphinx generic submit for %s conflicts at revision %d" inquiry cursor.Revision)
        else
            Ok(
                Map.add
                    inquiry
                    { cursor with
                        Revision = revision
                        ResultsJson = cursor.ResultsJson @ [ resultsJson ] }
                    current
            )

    let private applySubmitted
        (current: SphinxGenericCurrent)
        (inquiry: string)
        (revision: int)
        (expectedRevision: int)
        (resultsJson: string)
        : Result<SphinxGenericCurrent, string> =
        match Map.tryFind inquiry current with
        | None -> Error(sprintf "sphinx generic submit meets an unknown inquiry: %s" inquiry)
        | Some cursor -> submitAdvanced current inquiry cursor revision expectedRevision resultsJson

    let private cancelChecked
        (current: SphinxGenericCurrent)
        (inquiry: string)
        (cursor: GenericCursor)
        (revision: int)
        : Result<SphinxGenericCurrent, string> =
        if cursor.Cancelled then
            Error(sprintf "sphinx generic cancel meets a cancelled inquiry: %s" inquiry)
        elif revision <> cursor.Revision then
            Error(sprintf "sphinx generic cancel for %s breaks the revision chain" inquiry)
        else
            Ok(Map.add inquiry { cursor with Cancelled = true } current)

    let private applyCancelled
        (current: SphinxGenericCurrent)
        (inquiry: string)
        (revision: int)
        : Result<SphinxGenericCurrent, string> =
        match Map.tryFind inquiry current with
        | None -> Error(sprintf "sphinx generic cancel meets an unknown inquiry: %s" inquiry)
        | Some cursor -> cancelChecked current inquiry cursor revision

    let applyOne (current: SphinxGenericCurrent) (input: GenericEnvelopeInput) : Result<SphinxGenericCurrent, string> =
        match input with
        | GenericStarted(inquiry, revision, question, profile, executionMode, pluginsJson, budgetJson) ->
            applyStarted current inquiry revision question profile executionMode pluginsJson budgetJson
        | GenericSubmitted(inquiry, revision, expectedRevision, resultsJson) ->
            applySubmitted current inquiry revision expectedRevision resultsJson
        | GenericCancelled(inquiry, revision) -> applyCancelled current inquiry revision
