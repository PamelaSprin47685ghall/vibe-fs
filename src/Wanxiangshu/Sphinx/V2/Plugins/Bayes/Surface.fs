namespace Wanxiangshu.Sphinx.V2.Plugins

open System
open Fable.Core
open Fable.Core.JsInterop

/// The JS-native conformance surface for the Bayes exact refiner. The posterior law is
/// owned by `wanxiangli/bayes` (CUT-31 ②); this module only translates F# records.
module Surface =

    type private BayesLawLibrary =
        abstract infer: hypotheses: obj array * factors: obj array -> obj
        abstract priorOnly: hypotheses: obj array -> obj

    [<Import("BayesLaw", "wanxiangli/bayes")>]
    let private bayesLawClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : BayesLawLibrary = jsNative

    let private law = construct bayesLawClass

    [<Emit("$0[$1]")>]
    let private field (_value: obj) (_name: string) : obj = jsNative

    [<Emit("Object.keys($0)")>]
    let private keys (_value: obj) : string array = jsNative

    let private hypothesisView (hypothesis: Hypothesis) : obj =
        createObj [ "key" ==> hypothesis.Key; "prior" ==> hypothesis.Prior ]

    let private factorView (factor: Factor) : obj =
        createObj
            [ "observationId" ==> factor.ObservationId
              "dependencyKey" ==> factor.DependencyKey
              "likelihoods" ==> createObj (factor.Likelihoods |> Map.toList |> List.map (fun (k, v) -> k ==> v))
              "qualified" ==> factor.Qualified ]

    let private text (value: obj) (name: string) : string = unbox<string> (field value name)

    let private faultOf (fault: obj) : ExactFault =
        match text fault "kind" with
        | "TooFewHypotheses" -> ExactFault.TooFewHypotheses(unbox<int> (field fault "count"))
        | "BlankHypothesisKey" -> ExactFault.BlankHypothesisKey
        | "DuplicateHypothesisKey" -> ExactFault.DuplicateHypothesisKey(text fault "key")
        | "InvalidPrior" -> ExactFault.InvalidPrior(text fault "key")
        | "ZeroPriorMass" -> ExactFault.ZeroPriorMass
        | "BlankDependencyKey" -> ExactFault.BlankDependencyKey(text fault "semanticKey")
        | "NoQualifiedFactor" -> ExactFault.NoQualifiedFactor
        | "UnknownHypothesisKey" -> ExactFault.UnknownHypothesisKey(text fault "semanticKey", text fault "key")
        | "IncompleteLikelihood" -> ExactFault.IncompleteLikelihood(text fault "semanticKey", text fault "missingKey")
        | "InvalidLikelihood" -> ExactFault.InvalidLikelihood(text fault "semanticKey", text fault "key")
        | "NonPositivePartition" -> ExactFault.NonPositivePartition
        | other -> failwith (sprintf "wanxiangli/bayes returned an unknown fault kind %s" other)

    let private posteriorOf (view: obj) : Posterior =
        let probabilities = field view "probabilities"

        { Probabilities =
            keys probabilities
            |> Array.map (fun k -> k, unbox<float> (field probabilities k))
            |> Map.ofArray
          LogPartition = unbox<float> (field view "logPartition")
          UsedObservations = unbox<string array> (field view "usedObservations") |> List.ofArray
          Dropped =
            unbox<string array array> (field view "dropped")
            |> Array.map (fun pair -> pair[0], pair[1])
            |> List.ofArray }

    let private resultOf (result: obj) : Result<Posterior, ExactFault> =
        match unbox<bool> (field result "ok") with
        | true -> Ok(posteriorOf (field result "posterior"))
        | false -> Error(faultOf (field result "fault"))

    let listOfItems (items: Hypothesis list) : Hypothesis list = items |> List.ofSeq

    let stringFloatMapOf (entries: (string * float) list) : Map<string, float> = Map.ofSeq entries

    let isOk (result: Result<'value, 'error>) : bool =
        match result with
        | Ok _ -> true
        | Error _ -> false

    let isError (result: Result<'value, 'error>) : bool =
        match result with
        | Ok _ -> false
        | Error _ -> true

    let infer (hypotheses: Hypothesis list) (factors: Factor list) : Result<Posterior, ExactFault> =
        law.infer (
            hypotheses |> List.ofSeq |> List.map hypothesisView |> Array.ofList,
            factors |> List.ofSeq |> List.map factorView |> Array.ofList
        )
        |> resultOf

    /// A prior-only run. It is legal, and it says so: no new evidence, no convergence.
    let priorOnly (hypotheses: Hypothesis list) : Result<Posterior, ExactFault> =
        law.priorOnly (hypotheses |> List.ofSeq |> List.map hypothesisView |> Array.ofList)
        |> resultOf

    let okPosterior (result: Result<Posterior, ExactFault>) : Posterior =
        match result with
        | Ok posterior -> posterior
        | Error fault -> failwith (sprintf "posterior failed: %A" fault)

    let probabilityOf (posterior: Posterior) (key: string) : float =
        posterior.Probabilities |> Map.tryFind key |> Option.defaultValue Double.NaN
