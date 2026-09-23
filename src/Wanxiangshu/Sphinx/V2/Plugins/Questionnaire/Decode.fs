namespace Wanxiangshu.Sphinx.V2.Plugins

open Fable.Core.JsInterop

/// Strict decoding of a worker's response.
///
/// WHAT[sphinx-v2-018]: nothing is defaulted into shape. A missing judgment is a
/// missing judgment; an unknown label is refused; the judgment vocabulary is closed.
/// The old four-stage decoder filled in defaults and guessed at form, which turned a
/// malformed response into a plausible-looking observation.

type DecodeError = { Code: string; Message: string }

module Decode =

    let private error code message : Result<'value, DecodeError> =
        Error { Code = code; Message = message }

    let private field (name: string) (raw: obj) : Result<obj, DecodeError> =
        let value = emitJsExpr (raw, name) "$0[$1]"
        let present = emitJsExpr value "$0 !== undefined && $0 !== null"

        match present with
        | true -> Ok value
        | false -> error "invalid-schema" (sprintf "field %s is required" name)

    let private stringField (name: string) (raw: obj) : Result<string, DecodeError> =
        field name raw
        |> Result.bind (fun value ->
            let isString = emitJsExpr value "typeof $0 === 'string'"

            match isString with
            | true -> Ok(unbox<string> value)
            | false -> error "invalid-schema" (sprintf "field %s must be a string" name))

    let private arrayField (name: string) (raw: obj) : Result<obj array, DecodeError> =
        field name raw
        |> Result.bind (fun value ->
            let isArray = emitJsExpr value "Array.isArray($0)"

            match isArray with
            | true -> Ok(unbox<obj array> value)
            | false -> error "invalid-schema" (sprintf "field %s must be an array" name))

    /// The judgment vocabulary is closed. Anything else is a schema violation, not an
    /// invitation to map the nearest synonym.
    let private judgmentOf (text: string) : Result<Judgment, DecodeError> =
        match text with
        | "prefer-left" -> Ok Judgment.PreferLeft
        | "prefer-right" -> Ok Judgment.PreferRight
        | "tie" -> Ok Judgment.Tie
        | "abstain" -> Ok(Judgment.Abstain "worker abstained")
        | "conditional" -> Ok(Judgment.Conditional "condition-not-stated")
        | other -> error "invalid-schema" (sprintf "unknown judgment: %s" other)

    /// Reads a string out of a nested object, returning "" for anything else. Used only
    /// for optional proposal fields where a malformed entry is dropped rather than
    /// failing the response.
    let private textOf (name: string) (raw: obj) : string =
        let value = emitJsExpr (raw, name) "$0[$1]"
        let present = emitJsExpr value "$0 !== undefined && $0 !== null && typeof $0 === 'string'"

        match present with
        | true -> unbox<string> value
        | false -> ""

    let private alternativeOf (raw: obj) : ProposedAlternative =
        { LocalId = textOf "localId" raw
          Description = textOf "description" raw
          ExpectedContribution = textOf "expectedContribution" raw }

    /// The canonical payload is carried alongside the typed reading so a replay sees
    /// what the model actually said (WHAT[sphinx-v2-021]).
    let decodePairwise (canonicalResult: string) (raw: obj) : Result<PairwiseResponse, DecodeError> =
        stringField "judgment" raw
        |> Result.bind judgmentOf
        |> Result.bind (fun judgment ->
            stringField "rationale" raw
            |> Result.bind (fun rationale ->
                arrayField "sourceLabels" raw
                |> Result.map (fun items -> items |> Array.map string |> Array.toList)
                |> Result.bind (fun labels ->
                    arrayField "proposedAlternatives" raw
                    |> Result.map (fun items ->
                        items
                        |> Array.toList
                        |> List.map alternativeOf
                        |> List.filter (fun alternative -> alternative.LocalId <> ""))
                    |> Result.map (fun alternatives ->
                        { Judgment = judgment
                          Rationale = rationale
                          SourceLabels = labels
                          ProposedAlternatives = alternatives }))))

    let decodeRanking (raw: obj) : Result<RankingResponse, DecodeError> =
        arrayField "presentedSet" raw
        |> Result.map (fun items -> items |> Array.map string |> Array.toList)
        |> Result.bind (fun presented ->
            arrayField "rankedTiers" raw
            |> Result.map (fun items ->
                items
                |> Array.toList
                |> List.map (fun tier -> unbox<obj array> tier |> Array.map string |> Array.toList))
            |> Result.bind (fun tiers ->
                arrayField "unjudged" raw
                |> Result.map (fun items -> items |> Array.map string |> Array.toList)
                |> Result.map (fun unjudged ->
                    { PresentedSet = presented
                      RankedTiers = tiers
                      Best = None
                      Worst = None
                      Unjudged = unjudged })))

    /// best/worst is a joint observation, so an input where they coincide is refused
    /// rather than expanded into a pair of independent votes (WHAT[sphinx-v2-014]).
    let decodeMaxDiff (raw: obj) : Result<MaxDiffObservation, DecodeError> =
        stringField "best" raw
        |> Result.bind (fun best ->
            stringField "worst" raw
            |> Result.bind (fun worst ->
                if best = worst then
                    error "invalid-schema" "best and worst must differ"
                else
                    Ok
                        { PresentedSet = []
                          Best = best
                          Worst = worst }))
