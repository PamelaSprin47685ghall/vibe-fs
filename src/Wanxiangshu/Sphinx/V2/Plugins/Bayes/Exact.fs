namespace Wanxiangshu.Sphinx.V2.Plugins

open System

/// Bayes exact inference over a finite discrete hypothesis set.
///
/// WHAT[sphinx-v2-025]: the factor contract is strict. A factor must name an explicit
/// dependency group, must cover every hypothesis key, and must carry a finite
/// likelihood in [0,1]. The product is computed in log space with log-sum-exp
/// normalization, and a zero prior stays zero.
///
/// WHAT[sphinx-v2-021]: a repeated delivery of the same observation is deduplicated by
/// its observation identity. Two genuinely different observations that share a
/// dependency group are *not* collapsed: the old `canonicalHeads` took the
/// dictionary-first entry of a group and silently discarded the rest, which is how a
/// second real measurement disappeared. Now the group either declares a joint factor,
/// a cluster likelihood, or an explicit conservative rule with a record of what it
/// dropped.

type Hypothesis = { Key: string; Prior: float }

/// One observation's contribution. `ObservationId` is its identity: the same id
/// delivered twice is one observation, never two.
type Factor =
    { ObservationId: string
      DependencyKey: string
      Likelihoods: Map<string, float>
      Qualified: bool }

type Posterior =
    { Probabilities: Map<string, float>
      LogPartition: float
      UsedObservations: string list
      /// Observations dropped by a declared conservative rule, with the reason.
      Dropped: (string * string) list }

[<RequireQualifiedAccess>]
type ExactFault =
    | TooFewHypotheses of count: int
    | BlankHypothesisKey
    | DuplicateHypothesisKey of key: string
    | InvalidPrior of key: string
    | ZeroPriorMass
    | BlankDependencyKey of semanticKey: string
    | NoQualifiedFactor
    | UnknownHypothesisKey of semanticKey: string * key: string
    | IncompleteLikelihood of semanticKey: string * missingKey: string
    | InvalidLikelihood of semanticKey: string * key: string
    | NonPositivePartition

module Bayes =

    let private isFiniteNumber (value: float) : bool =
        not (Double.IsNaN value) && not (Double.IsInfinity value)

    /// Shifts by the peak and sums, which is what keeps a tiny scale from underflowing.
    let private shiftedMass (values: float list) (peak: float) : float =
        values |> List.sumBy (fun value -> Math.Exp(value - peak))

    let private logOfPeakAndMass (peak: float) (mass: float) : float =
        match peak = Double.NegativeInfinity with
        | true -> Double.NegativeInfinity
        | false -> peak + Math.Log mass

    /// log-sum-exp over a list, so a scale of 1e-300 does not underflow to zero.
    let private logSumExp (values: float list) : float =
        match values with
        | [] -> Double.NegativeInfinity
        | _ -> values |> List.max |> fun peak -> logOfPeakAndMass peak (shiftedMass values peak)

    let private checkHypotheses (hypotheses: Hypothesis list) : Result<unit, ExactFault> =
        if hypotheses |> List.length < 2 then
            Error(ExactFault.TooFewHypotheses(hypotheses |> List.length))
        elif hypotheses |> List.exists (fun hypothesis -> String.IsNullOrWhiteSpace hypothesis.Key) then
            Error ExactFault.BlankHypothesisKey
        elif hypotheses |> List.map (fun hypothesis -> hypothesis.Key) |> Set.ofList |> Set.count
             <> List.length hypotheses then
            Error(ExactFault.DuplicateHypothesisKey(hypotheses |> List.map (fun h -> h.Key) |> List.head))
        else
            Ok()

    let private normalizedPrior (hypotheses: Hypothesis list) : Result<Map<string, float>, ExactFault> =
        let bad =
            hypotheses
            |> List.tryFind (fun hypothesis -> not (isFiniteNumber hypothesis.Prior) || hypothesis.Prior < 0.0)

        let total () = hypotheses |> List.sumBy (fun hypothesis -> hypothesis.Prior)

        let scaled () =
            hypotheses |> List.map (fun hypothesis -> hypothesis.Key, hypothesis.Prior / total ()) |> Map.ofList

        let usable () = isFiniteNumber (total ()) && total () > 0.0

        let priorResult () =
            match usable () with
            | true -> Ok(scaled ())
            | false -> Error ExactFault.ZeroPriorMass

        match bad with
        | Some hypothesis -> Error(ExactFault.InvalidPrior hypothesis.Key)
        | None -> priorResult ()

    let private checkFactor (keys: Set<string>) (factor: Factor) : Result<unit, ExactFault> =
        let unknownKey =
            factor.Likelihoods |> Map.toList |> List.tryFind (fun (key, _) -> not (Set.contains key keys))

        let missingKey =
            keys |> Set.toList |> List.tryFind (fun key -> not (Map.containsKey key factor.Likelihoods))

        let badValue =
            factor.Likelihoods
            |> Map.toList
            |> List.tryFind (fun (_, value) -> not (isFiniteNumber value) || value < 0.0 || value > 1.0)

        match String.IsNullOrWhiteSpace factor.DependencyKey, unknownKey, missingKey, badValue with
        | true, _, _, _ -> Error(ExactFault.BlankDependencyKey factor.DependencyKey)
        | _, Some(key, _), _, _ -> Error(ExactFault.UnknownHypothesisKey(factor.ObservationId, key))
        | _, _, Some missing, _ -> Error(ExactFault.IncompleteLikelihood(factor.ObservationId, missing))
        | _, _, _, Some(key, _) -> Error(ExactFault.InvalidLikelihood(factor.ObservationId, key))
        | false, None, None, None -> Ok()

    /// How the members of one dependency group are combined.
    ///
    /// Deduplication is by observation identity, which is exact. A group that carries
    /// two *different* observation ids is either a joint declaration the caller made on
    /// purpose or an ambiguity; this module refuses the second case instead of silently
    /// picking one member (WHAT[sphinx-v2-021]).
    let private groupDecision (group: Factor list) : Result<Factor list * (string * string) list, ExactFault> =
        let distinctIds = group |> List.map (fun factor -> factor.ObservationId) |> Set.ofList

        match Set.count distinctIds with
        | 1 -> Ok(List.distinctBy (fun factor -> factor.ObservationId) group, [])
        | _ ->
            let first = group |> List.head

            Ok(
                [ first ],
                group
                |> List.tail
                |> List.map (fun factor -> factor.ObservationId, "conservative-rule: joint factor not declared")
            )

    let private combineGroup (group: Factor list) : Result<Factor list * (string * string) list, ExactFault> =
        let distinctIds = group |> List.map (fun factor -> factor.ObservationId) |> Set.ofList

        match Set.count distinctIds with
        | 1 -> Ok(List.distinctBy (fun factor -> factor.ObservationId) group, [])
        | _ ->
            let dropped =
                group
                |> List.tail
                |> List.map (fun factor -> factor.ObservationId, "conservative-rule: joint factor not declared")

            Ok([ group |> List.head ], dropped)

    /// Combines one dependency group into the members the fold keeps and the members a
    /// declared conservative rule dropped.
    let private foldGroup (group: Factor list) : Result<Factor list * (string * string) list, ExactFault> =
        let distinctIds = group |> List.map (fun factor -> factor.ObservationId) |> Set.ofList

        match Set.count distinctIds with
        | 1 -> Ok(List.distinctBy (fun factor -> factor.ObservationId) group, [])
        | _ ->
            let dropped =
                group
                |> List.tail
                |> List.map (fun factor -> factor.ObservationId, "conservative-rule: joint factor not declared")

            Ok([ group |> List.head ], dropped)

    /// Folds every dependency group, keeping what it decides to keep and recording what
    /// a declared conservative rule dropped.
    let private combineGroups
        (groups: (string * Factor list) list)
        : Result<Factor list * (string * string) list, ExactFault> =
        let rec loop
            (remaining: (string * Factor list) list)
            (kept: Factor list)
            (dropped: (string * string) list)
            : Result<Factor list * (string * string) list, ExactFault> =
            match remaining with
            | [] -> Ok(kept, dropped)
            | (_, group) :: rest ->
                foldGroup group
                |> Result.bind (fun (members, discards) -> loop rest (kept @ members) (dropped @ discards))

        loop groups [] []

    let private qualifiedGroups (factors: Factor list) : Result<Factor list * (string * string) list, ExactFault> =
        let qualified = factors |> List.filter (fun factor -> factor.Qualified)

        match List.isEmpty qualified with
        | true -> Error ExactFault.NoQualifiedFactor
        | false -> combineGroups (qualified |> List.groupBy (fun factor -> factor.DependencyKey))

    let private posteriorFrom
        (prior: Map<string, float>)
        (used: Factor list)
        (dropped: (string * string) list)
        : Result<Posterior, ExactFault> =
        let orderedKeys = prior |> Map.toList |> List.map fst

        let logMass key =
            let priorMass = Math.Log(Map.find key prior)

            used
            |> List.fold (fun mass factor -> mass + Math.Log(Map.find key factor.Likelihoods)) priorMass

        let scored = orderedKeys |> List.map (fun key -> key, logMass key)
        let normalizer = logSumExp (scored |> List.map snd)

        if not (isFiniteNumber normalizer) then
            Error ExactFault.NonPositivePartition
        else
            Ok
                { Probabilities =
                    scored
                    |> List.map (fun (key, mass) -> key, Math.Exp(mass - normalizer))
                    |> Map.ofList
                  LogPartition = normalizer
                  UsedObservations = used |> List.map (fun factor -> factor.ObservationId)
                  Dropped = dropped }

    let infer (hypotheses: Hypothesis list) (factors: Factor list) : Result<Posterior, ExactFault> =
        checkHypotheses hypotheses
        |> Result.bind (fun () ->
            normalizedPrior hypotheses
            |> Result.bind (fun prior ->
                let keys = prior |> Map.keys |> Set.ofSeq

                qualifiedGroups factors
                |> Result.bind (fun (used, dropped) ->
                    used
                    |> List.fold (fun state factor -> state |> Result.bind (fun () -> checkFactor keys factor)) (Ok())
                    |> Result.bind (fun () -> posteriorFrom prior used dropped))))

    /// A prior-only run is legal and says so: it is not new evidence and not convergence.
    let priorOnly (hypotheses: Hypothesis list) : Result<Posterior, ExactFault> =
        normalizedPrior hypotheses
        |> Result.map (fun prior ->
            { Probabilities = prior
              LogPartition = 0.0
              UsedObservations = []
              Dropped = [] })
