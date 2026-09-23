namespace Wanxiangshu.Sphinx.V2.Plugins

/// Ranking likelihoods, each stating what it may be used for.
///
/// WHAT[sphinx-v2-014]: a ranking is not a bag of pairs. Expanding a ranked ballot into
/// all its constituent pairs is legal only as a *composite* likelihood that keeps the
/// ballot cluster, because the pairs are dependent. Counting them as independent samples
/// is how a ten-item ballot becomes forty-five votes.

module Ranking =

    /// Sum over all ordered pairs of the presented set, in theta units. Returning None
    /// when theta is incomplete keeps the caller from silently treating a missing
    /// candidate as a zero-strength one.
    let private pairTotal (theta: Map<string, float>) (presented: string list) : float option =
        presented
        |> List.collect (fun left -> presented |> List.map (fun right -> left, right))
        |> List.filter (fun (left, right) -> left <> right)
        |> List.map (fun (left, right) ->
            match Map.tryFind left theta, Map.tryFind right theta with
            | Some tl, Some tr -> Some(System.Math.Exp(tl - tr))
            | _ -> None)
        |> fun terms ->
            match terms |> List.contains None with
            | true -> None
            | false -> Some(terms |> List.choose id |> List.sum)

    /// MaxDiff joint likelihood for one best/worst observation.
    ///
    /// P(b,w | S,theta) = exp(theta_b - theta_w) / sum over distinct (i,j) in S.
    /// The denominator is the full set of ordered pairs, not just the chosen one, which
    /// is what makes it a proper joint likelihood rather than two independent picks.
    let maxDiffLogProbability
        (theta: Map<string, float>)
        (presented: string list)
        (best: string)
        (worst: string)
        : float option =
        let chosen =
            match Map.tryFind best theta, Map.tryFind worst theta with
            | Some tb, Some tw -> Some(tb - tw)
            | _ -> None

        match chosen, pairTotal theta presented with
        | Some gap, Some total ->
            match total <= 0.0 with
            | true -> None
            | false -> Some(gap - System.Math.Log(total))
        | _ -> None

    /// Plackett–Luce for a strict complete ranking. Every item in the ranking must be
    /// present in theta; a partial ranking is a different model, not a truncated one.
    let plackettLuceLogProbability (theta: Map<string, float>) (order: string list) : float option =
        let logNormalizer (available: Set<string>) : float option =
            let strengths = available |> Set.toList |> List.choose (fun item -> Map.tryFind item theta)
            let complete = List.length strengths = Set.count available
            match complete with
            | false -> None
            | true ->
                let total = strengths |> List.sumBy System.Math.Exp
                match total <= 0.0 with
                | true -> None
                | false -> Some(System.Math.Log total)

        let rec walk (remaining: string list) (available: Set<string>) (acc: float) : float option =
            match remaining with
            | [] -> Some acc
            | item :: rest ->
                match Map.tryFind item theta, logNormalizer available with
                | Some ti, Some denominator ->
                    walk rest (Set.remove item available) (acc + ti - denominator)
                | _ -> None

        match List.length order = 0 with
        | true -> None
        | false -> walk order (Set.ofList order) 0.0

    /// The expansion of a ranked ballot into pairs, kept as a *composite* term whose
    /// cluster the caller must carry. The returned count is not a sample size.
    let compositePairs (order: string list) : (string * string) list =
        order
        |> List.mapi (fun i item -> order |> List.skip (i + 1) |> List.map (fun other -> item, other))
        |> List.concat

    /// Borda as a descriptive baseline. It is not a likelihood and carries no
    /// uncertainty; the caller must label it as descriptive and report the exposure
    /// matrix that produced it (WHAT[sphinx-v2-024]).
    let bordaCounts (ballots: string list list) (candidates: string list) : Map<string, float> =
        let points (order: string list) (item: string) =
            match List.tryFindIndex ((=) item) order with
            | Some index -> float (List.length order - index)
            | None -> 0.0

        let totals =
            candidates
            |> List.map (fun candidate ->
                candidate, ballots |> List.sumBy (fun order -> points order candidate))
            |> Map.ofList

        let exposure =
            candidates
            |> List.map (fun candidate -> candidate, ballots |> List.filter (fun order -> List.contains candidate order) |> List.length)
            |> Map.ofList

        candidates
        |> List.map (fun candidate ->
            let appearances = exposure |> Map.tryFind candidate |> Option.defaultValue 0
            let total = totals |> Map.tryFind candidate |> Option.defaultValue 0.0

            if appearances = 0 then
                candidate, 0.0
            else
                candidate, total / float appearances)
        |> Map.ofList
