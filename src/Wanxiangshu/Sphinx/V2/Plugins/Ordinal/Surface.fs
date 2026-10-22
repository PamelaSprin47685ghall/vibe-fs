namespace Wanxiangshu.Sphinx.V2.Plugins

open System
open Fable.Core
open Fable.Core.JsInterop

/// The JS-native conformance surface for the question vocabulary and the pairwise
/// likelihood (the likelihood itself is owned by wanxiangli/ordinal). Pure functions only; no store, no clock, no model call.
module Surface =

    /// The `wanxiangli/ordinal` owner (CUT-31 ①) of the pairwise and ranking likelihoods.
    type private OrdinalLawLibrary =
        abstract logSigmoid: x: float -> float
        abstract logSumExp: a: float * b: float -> float
        abstract compositePairs: order: string array -> string array array

    [<Import("OrdinalLaw", "wanxiangli/ordinal")>]
    let private ordinalLawClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : OrdinalLawLibrary = jsNative

    let private law = construct ordinalLawClass

    let stringSetOf (items: string list) : Set<string> = items |> List.ofSeq |> Set.ofSeq

    let stringFloatMapOf (entries: (string * float) list) : Map<string, float> = entries |> List.ofSeq |> Map.ofSeq

    let listOfItems (items: string list) : string list = items |> List.ofSeq

    let listCount (items: string list) : int = List.length (items |> List.ofSeq)

    let isOk (result: Result<'value, 'error>) : bool =
        match result with
        | Ok _ -> true
        | Error _ -> false

    let isError (result: Result<'value, 'error>) : bool =
        match result with
        | Ok _ -> false
        | Error _ -> true

    let judgmentOf (tag: string) : Judgment =
        match tag with
        | "prefer-left" -> Judgment.PreferLeft
        | "prefer-right" -> Judgment.PreferRight
        | "tie" -> Judgment.Tie
        | "abstain" -> Judgment.Abstain "worker abstained"
        | "conditional" -> Judgment.Conditional "condition"
        | other -> failwith (sprintf "unknown judgment: %s" other)

    let isDirectional (judgment: Judgment) : bool =
        QuestionnaireModel.isDirectional judgment

    let isTie (judgment: Judgment) : bool = QuestionnaireModel.isTie judgment

    let isAbstention (judgment: Judgment) : bool =
        QuestionnaireModel.isAbstention judgment

    let isConditional (judgment: Judgment) : bool =
        QuestionnaireModel.isConditional judgment

    let labelsWithin (presented: Set<string>) (response: PairwiseResponse) (cited: Set<string>) : Result<unit, string> =
        // The response's own labels are replaced by the ones this call supplies, so a
        // caller can test a citation set it controls without fabricating a response.
        let candidate =
            { response with
                SourceLabels = cited |> Set.toList }

        QuestionnaireModel.labelsWithin presented candidate

    let logSigmoid (x: float) : float = law.logSigmoid x

    let logSumExp (a: float) (b: float) : float = law.logSumExp (a, b)

    let compositePairs (order: string list) : (string * string) list =
        law.compositePairs (order |> List.ofSeq |> Array.ofList)
        |> Array.map (fun pair -> pair[0], pair[1])
        |> List.ofArray

    let decodeMaxDiff (raw: obj) : Result<MaxDiffObservation, DecodeError> =
        // A JS object reaches the decoder directly; the decoder reads only plain fields.
        Decode.decodeMaxDiff raw
