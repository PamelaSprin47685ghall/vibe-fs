namespace Wanxiangshu.Sphinx.V2.Plugins

open System

/// The JS-native conformance surface for the Bayes exact refiner.
module Surface =

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
        Bayes.infer hypotheses (factors |> List.ofSeq)

    /// A prior-only run. It is legal, and it says so: no new evidence, no convergence.
    let priorOnly (hypotheses: Hypothesis list) : Result<Posterior, ExactFault> =
        Bayes.priorOnly (hypotheses |> List.ofSeq)

    let okPosterior (result: Result<Posterior, ExactFault>) : Posterior =
        match result with
        | Ok posterior -> posterior
        | Error fault -> failwith (sprintf "posterior failed: %A" fault)

    let probabilityOf (posterior: Posterior) (key: string) : float =
        posterior.Probabilities |> Map.tryFind key |> Option.defaultValue Double.NaN
