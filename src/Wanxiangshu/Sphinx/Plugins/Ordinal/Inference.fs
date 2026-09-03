namespace Wanxiangshu.Sphinx.Plugins.Ordinal

open System

module Inference =
    type Ballot =
        { Ranks: string list list }

    type BordaInput =
        { Candidates: string list
          Ballots: Ballot list }

    type BordaError =
        | EmptyCandidates
        | EmptyBallots
        | DuplicateCandidate of string
        | UnknownCandidate of int * string
        | DuplicateRank of int * string
        | EmptyTier of int
        | EmptyBallot of int

    type BordaOutcome =
        { Scores: Map<string, float>
          MeanScores: Map<string, float>
          Ranking: string list
          Exposure: Map<string, int>
          Extension: string
          Complete: bool
          Guarantees: string list
          Assumptions: Set<string> }

    type Contest =
        { First: string
          Second: string
          FirstWins: int
          SecondWins: int }

    type BtlInput =
        { Candidates: string list
          Contests: Contest list
          Regularization: float
          Tolerance: float
          MaxIterations: int }

    // L-1: Diagnostics echoes the caller-requested cap alongside used Iterations so
    // Converged=false at the internal cap reads as capped, not merely slow.
    type BtlDiagnostics =
        { Iterations: int
          Converged: bool
          LogLikelihood: float
          GradientNorm: float
          MaxAbsStrength: float
          Regularization: float
          RequestedMaxIterations: int }

    type BtlUncertainty =
        { StandardErrors: Map<string, float> }

    type BtlOutcome =
        { Strengths: Map<string, float>
          Appearances: Map<string, int>
          Diagnostics: BtlDiagnostics
          Uncertainty: BtlUncertainty
          Assumptions: Set<string> }

    type BtlError =
        | EmptyCandidates
        | DuplicateCandidate of string
        | EmptyContests
        | UnknownCandidate of string
        | SelfContest of string
        | NonPositiveWins of string
        | InvalidRegularization
        | InvalidTolerance
        | InvalidMaxIterations
        | NonFiniteEstimate
        | SingularHessian
        | Unidentifiable of string list list

    let maxNewtonIterations = 100

    let bordaErrorCode =
        function
        | EmptyCandidates -> "empty-candidates"
        | EmptyBallots -> "empty-ballots"
        | DuplicateCandidate _ -> "duplicate-candidate"
        | UnknownCandidate _ -> "unknown-candidate"
        | DuplicateRank _ -> "duplicate-rank"
        | EmptyTier _ -> "empty-tier"
        | EmptyBallot _ -> "empty-ballot"

    let btlErrorCode =
        function
        | EmptyCandidates -> "empty-candidates"
        | DuplicateCandidate _ -> "duplicate-candidate"
        | EmptyContests -> "empty-contests"
        | UnknownCandidate _ -> "unknown-candidate"
        | SelfContest _ -> "self-contest"
        | NonPositiveWins _ -> "non-positive-wins"
        | InvalidRegularization -> "invalid-regularization"
        | InvalidTolerance -> "invalid-tolerance"
        | InvalidMaxIterations -> "invalid-max-iterations"
        | NonFiniteEstimate -> "non-finite-estimate"
        | SingularHessian -> "singular-hessian-rank-deficient"
        | Unidentifiable _ -> "unidentifiable-disconnected-comparison-graph"

    let private bordaGuarantees =
        [ "ballot-order-invariance"; "candidate-label-equivariance" ]

    let private firstDuplicate (items: string list) : string option =
        let rec loop seen rest =
            match rest with
            | [] -> None
            | head :: tail ->
                if Set.contains head seen then
                    Some head
                else
                    loop (Set.add head seen) tail

        loop Set.empty items

    let borda (input: BordaInput) =
        if List.isEmpty input.Candidates then
            Error EmptyCandidates
        elif List.isEmpty input.Ballots then
            Error EmptyBallots
        else
            match firstDuplicate input.Candidates with
            | Some candidate -> Error(DuplicateCandidate candidate)
            | None ->
                let known = Set.ofList input.Candidates

                let rec validateBallots index rest =
                    match rest with
                    | [] -> Ok()
                    | ballot :: tail when List.isEmpty ballot.Ranks -> Error(EmptyBallot index)
                    | ballot :: tail ->
                        match ballot.Ranks |> List.tryFindIndex List.isEmpty with
                        | Some _ -> Error(EmptyTier index)
                        | None ->
                            let flat = ballot.Ranks |> List.concat

                            match flat |> List.tryFind (fun candidate -> not (Set.contains candidate known)) with
                            | Some candidate -> Error(UnknownCandidate(index, candidate))
                            | None ->
                                match firstDuplicate flat with
                                | Some candidate -> Error(DuplicateRank(index, candidate))
                                | None -> validateBallots (index + 1) tail

                match validateBallots 0 input.Ballots with
                | Error error -> Error error
                | Ok () ->
                    let zeroScores = input.Candidates |> List.map (fun candidate -> candidate, 0.0) |> Map.ofList
                    let zeroExposure = input.Candidates |> List.map (fun candidate -> candidate, 0) |> Map.ofList

                    let scoreBallot (scores: Map<string, float>, exposure: Map<string, int>) (ballot: Ballot) =
                        let ranked = ballot.Ranks |> List.concat |> Set.ofList
                        let field = float ranked.Count

                        let rec scoreTiers position tiers current =
                            match tiers with
                            | [] -> current
                            | tier :: rest ->
                                let size = float tier.Length
                                let points =
                                    [ position .. position + tier.Length - 1 ]
                                    |> List.sumBy (fun rank -> field - 1.0 - float rank)
                                    |> fun total -> total / size

                                let updated =
                                    tier
                                    |> List.fold
                                        (fun acc candidate -> acc |> Map.add candidate (acc.[candidate] + points))
                                        current

                                scoreTiers (position + tier.Length) rest updated

                        let scores' = scoreTiers 0 ballot.Ranks scores

                        let exposure' =
                            ranked
                            |> Set.fold (fun acc candidate -> acc |> Map.add candidate (acc.[candidate] + 1)) exposure

                        scores', exposure'

                    let scores, exposure =
                        input.Ballots |> List.fold scoreBallot (zeroScores, zeroExposure)

                    let complete =
                        input.Ballots
                        |> List.forall (fun ballot -> (ballot.Ranks |> List.concat |> Set.ofList) = known)

                    let hasTies =
                        input.Ballots |> List.exists (fun ballot -> ballot.Ranks |> List.exists (fun tier -> tier.Length > 1))

                    let extension =
                        if not complete then
                            "appearance-normalized"
                        elif hasTies then
                            "fractional-tie"
                        else
                            "complete-baseline"

                    let meanScores =
                        scores
                        |> Map.map (fun candidate total ->
                            match exposure.[candidate] with
                            | 0 -> 0.0
                            | appearances -> total / float appearances)

                    let ranking =
                        input.Candidates |> List.sortBy (fun candidate -> -(meanScores.[candidate]), candidate)

                    let assumptions =
                        [ if complete then
                              "complete-equal-exposure"
                          else
                              "incomplete-ballots-appearance-normalized"
                          if hasTies then "fractional-tie-averaging"
                          if not complete then "unranked-scores-zero" ]
                        |> Set.ofList

                    Ok
                        { Scores = scores
                          MeanScores = meanScores
                          Ranking = ranking
                          Exposure = exposure
                          Extension = extension
                          Complete = complete
                          Guarantees = bordaGuarantees
                          Assumptions = assumptions }

    let private finite value =
        not (Double.IsNaN value || Double.IsInfinity value)

    let private sigmoid value =
        if value >= 0.0 then
            1.0 / (1.0 + exp (-value))
        else
            let shifted = exp value
            shifted / (1.0 + shifted)

    let private pairKey first second =
        if first <= second then first, second else second, first

    let bradleyTerry (input: BtlInput) =
        if List.isEmpty input.Candidates then
            Error EmptyCandidates
        elif List.isEmpty input.Contests then
            Error EmptyContests
        else
            match firstDuplicate input.Candidates with
            | Some candidate -> Error(DuplicateCandidate candidate)
            | None ->
                let known = Set.ofList input.Candidates

                let rec validateContests rest =
                    match rest with
                    | [] -> Ok()
                    | contest :: tail ->
                        if not (Set.contains contest.First known) then
                            Error(UnknownCandidate contest.First)
                        elif not (Set.contains contest.Second known) then
                            Error(UnknownCandidate contest.Second)
                        elif contest.First = contest.Second then
                            Error(SelfContest contest.First)
                        elif contest.FirstWins < 0 || contest.SecondWins < 0
                             || contest.FirstWins + contest.SecondWins <= 0 then
                            Error(NonPositiveWins(sprintf "%s>%s" contest.First contest.Second))
                        else
                            validateContests tail

                match validateContests input.Contests with
                | Error error -> Error error
                | Ok () ->
                    if not (finite input.Regularization) || input.Regularization < 0.0 then
                        Error InvalidRegularization
                    elif not (finite input.Tolerance) || input.Tolerance <= 0.0 then
                        Error InvalidTolerance
                    elif input.MaxIterations <= 0 then
                        Error InvalidMaxIterations
                    else
                        let sorted = input.Candidates |> List.sort
                        let count = sorted.Length
                        let totals =
                            input.Contests
                            |> List.fold
                                (fun acc contest ->
                                    let key = pairKey contest.First contest.Second
                                    let firstWins, secondWins =
                                        acc |> Map.tryFind key |> Option.defaultValue (0, 0)

                                    let ordered =
                                        if contest.First <= contest.Second then
                                            firstWins + contest.FirstWins, secondWins + contest.SecondWins
                                        else
                                            firstWins + contest.SecondWins, secondWins + contest.FirstWins

                                    acc |> Map.add key ordered)
                                Map.empty

                        let neighbors =
                            totals
                            |> Map.toList
                            |> List.collect (fun ((first, second), _) -> [ first, second; second, first ])
                            |> List.groupBy fst
                            |> List.map (fun (candidate, pairs) -> candidate, pairs |> List.map snd |> Set.ofList)
                            |> Map.ofList

                        let rec reach visited frontier =
                            match frontier with
                            | [] -> visited
                            | candidate :: rest when Set.contains candidate visited -> reach visited rest
                            | candidate :: rest ->
                                let next =
                                    neighbors |> Map.tryFind candidate |> Option.defaultValue Set.empty

                                reach (Set.add candidate visited) (rest @ (next |> Set.toList))

                        let rec components unvisited acc =
                            match unvisited |> Set.toList |> List.sort with
                            | [] -> acc
                            | candidate :: _ ->
                                let group = reach Set.empty [ candidate ]
                                components (unvisited - group) (group :: acc)

                        let groups = components known []

                        if groups.Length > 1 then
                            groups
                            |> List.map (fun group -> group |> Set.toList |> List.sort)
                            |> List.sortBy List.head
                            |> Unidentifiable
                            |> Error
                        else
                            let appearances =
                                sorted
                                |> List.map (fun candidate ->
                                    let total =
                                        totals
                                        |> Map.toList
                                        |> List.sumBy (fun ((first, second), (winsFirst, winsSecond)) ->
                                            if candidate = first || candidate = second then
                                                winsFirst + winsSecond
                                            else
                                                0)

                                    candidate, total)
                                |> Map.ofList

                            let winsOf i j =
                                let first, second = sorted.[i], sorted.[j]
                                let key = pairKey first second

                                match totals |> Map.tryFind key with
                                | None -> 0.0, 0.0
                                | Some (winsFirst, winsSecond) ->
                                    if first = sorted.[i] then
                                        float winsFirst, float winsSecond
                                    else
                                        float winsSecond, float winsFirst

                            let expand (free: float array) =
                                Array.init count (fun i ->
                                    if i < count - 1 then
                                        free.[i]
                                    else
                                        -(free |> Array.sum))

                            let penalizedLogLikelihood (free: float array) =
                                let theta = expand free
                                let mutable total = 0.0

                                for i in 0 .. count - 1 do
                                    for j in i + 1 .. count - 1 do
                                        let winsI, winsJ = winsOf i j
                                        let difference = theta.[i] - theta.[j]

                                        if winsI > 0.0 then
                                            total <- total + winsI * log (sigmoid difference)

                                        if winsJ > 0.0 then
                                            total <- total + winsJ * log (sigmoid (-difference))

                                let penalty =
                                    (theta |> Array.sumBy (fun value -> value * value)) * input.Regularization / 2.0

                                total - penalty

                            let gradient (free: float array) =
                                let theta = expand free
                                let freeCount = count - 1
                                let raw = Array.zeroCreate count

                                for i in 0 .. count - 1 do
                                    let mutable partial = 0.0

                                    for j in 0 .. count - 1 do
                                        if i <> j then
                                            let winsI, winsJ = winsOf i j
                                            let meetings = winsI + winsJ

                                            if meetings > 0.0 then
                                                partial <- partial + winsI - meetings * sigmoid (theta.[i] - theta.[j])

                                    raw.[i] <- partial

                                Array.init freeCount (fun k ->
                                    raw.[k] - raw.[count - 1] - input.Regularization * (theta.[k] - theta.[count - 1]))

                            let hessian (free: float array) =
                                let theta = expand free
                                let freeCount = count - 1
                                let raw = Array.init count (fun _ -> Array.zeroCreate count)

                                for i in 0 .. count - 1 do
                                    for j in 0 .. count - 1 do
                                        if i <> j then
                                            let winsI, winsJ = winsOf i j
                                            let meetings = winsI + winsJ

                                            if meetings > 0.0 then
                                                let spread = sigmoid (theta.[i] - theta.[j])
                                                raw.[i].[j] <- meetings * spread * (1.0 - spread)

                                for i in 0 .. count - 1 do
                                    let mutable row = 0.0

                                    for j in 0 .. count - 1 do
                                        if i <> j then
                                            row <- row + raw.[i].[j]

                                    raw.[i].[i] <- -row

                                Array.init
                                    freeCount
                                    (fun k ->
                                        Array.init
                                            freeCount
                                            (fun l ->
                                                let baseValue =
                                                    raw.[k].[l] - raw.[k].[count - 1] - raw.[count - 1].[l]
                                                    + raw.[count - 1].[count - 1]

                                                if k = l then
                                                    baseValue - input.Regularization * 2.0
                                                else
                                                    baseValue - input.Regularization))

                            let solve (matrix: float[][]) (right: float[]) : float[] option =
                                let size = right.Length
                                let work = matrix |> Array.map Array.copy
                                let vector = Array.copy right

                                let rec eliminate column =
                                    if column >= size then
                                        Some()
                                    else
                                        let pivot =
                                            [ column .. size - 1 ] |> List.maxBy (fun row -> abs work.[row].[column])

                                        if abs work.[pivot].[column] < 1e-12 then
                                            None
                                        else
                                            for row in 0 .. size - 1 do
                                                let tmp = work.[column].[row]
                                                work.[column].[row] <- work.[pivot].[row]
                                                work.[pivot].[row] <- tmp

                                            let swap = vector.[column]
                                            vector.[column] <- vector.[pivot]
                                            vector.[pivot] <- swap

                                            for row in column + 1 .. size - 1 do
                                                let factor = work.[row].[column] / work.[column].[column]
                                                work.[row].[column] <- 0.0

                                                for col in column + 1 .. size - 1 do
                                                    work.[row].[col] <- work.[row].[col] - factor * work.[column].[col]

                                                vector.[row] <- vector.[row] - factor * vector.[column]

                                            eliminate (column + 1)

                                match eliminate 0 with
                                | None -> None
                                | Some () ->
                                    let solution = Array.zeroCreate size

                                    for row in size - 1 .. -1 .. 0 do
                                        let mutable acc = vector.[row]

                                        for col in row + 1 .. size - 1 do
                                            acc <- acc - work.[row].[col] * solution.[col]

                                        solution.[row] <- acc / work.[row].[row]

                                    Some solution

                            let iterations = min input.MaxIterations maxNewtonIterations
                            let freeCount = count - 1

                            if freeCount = 0 then
                                [ sorted.[0] ] |> Unidentifiable |> Error
                            else
                                let rec iterate (free: float array) used =
                                    let grad = gradient free
                                    let norm = grad |> Array.fold (fun peak value -> max peak (abs value)) 0.0

                                    if norm < input.Tolerance then
                                        Ok(free, used, norm, true)
                                    elif used >= iterations then
                                        Ok(free, used, norm, false)
                                    else
                                        // L-2: a singular Newton system on a connected graph is rank
                                        // deficiency, not a non-finite number: label it as such.
                                        match solve (hessian free) (grad |> Array.map (fun value -> -value)) with
                                        | None -> Error SingularHessian
                                        | Some step ->
                                            if step |> Array.exists (fun value -> not (finite value)) then
                                                Error NonFiniteEstimate
                                            else
                                                let current = penalizedLogLikelihood free

                                                let rec dampen factor attempts =
                                                    if attempts <= 0 then
                                                        None
                                                    else
                                                        let trial =
                                                            Array.init freeCount (fun k -> free.[k] + factor * step.[k])

                                                        if trial |> Array.exists (fun value -> not (finite value)) then
                                                            dampen (factor / 2.0) (attempts - 1)
                                                        else
                                                            let value = penalizedLogLikelihood trial

                                                            if value >= current - 1e-12 then
                                                                Some trial
                                                            else
                                                                dampen (factor / 2.0) (attempts - 1)

                                                match dampen 1.0 40 with
                                                | None -> Ok(free, used, norm, false)
                                                | Some next -> iterate next (used + 1)

                                match iterate (Array.zeroCreate freeCount) 0 with
                                | Error error -> Error error
                                | Ok (free, used, norm, converged) ->
                                    let theta = expand free

                                    if theta |> Array.exists (fun value -> not (finite value)) then
                                        Error NonFiniteEstimate
                                    else
                                        let strengths =
                                            sorted |> List.mapi (fun i candidate -> candidate, theta.[i]) |> Map.ofList

                                        let inverse =
                                            let size = freeCount
                                            let matrix = hessian free

                                            let rec invertColumn col acc =
                                                if col >= size then
                                                    Some acc
                                                else
                                                    let unit = Array.init size (fun row -> if row = col then 1.0 else 0.0)

                                                    match solve matrix unit with
                                                    | None -> None
                                                    | Some column -> invertColumn (col + 1) (column :: acc)

                                            invertColumn 0 []

                                        let standardErrors =
                                            match inverse with
                                            | None ->
                                                sorted |> List.map (fun candidate -> candidate, Double.NaN) |> Map.ofList
                                            | Some columns ->
                                                let rows = columns |> List.rev |> List.toArray

                                                let freeVariance k =
                                                    if k < freeCount then max 0.0 (-rows.[k].[k]) else 0.0

                                                let lastVariance =
                                                    let mutable total = 0.0

                                                    for k in 0 .. freeCount - 1 do
                                                        for l in 0 .. freeCount - 1 do
                                                            total <- total + -rows.[k].[l]

                                                    max 0.0 total

                                                sorted
                                                |> List.mapi (fun i candidate ->
                                                    let variance =
                                                        if i < freeCount then
                                                            freeVariance i
                                                        else
                                                            lastVariance

                                                    candidate, sqrt variance)
                                                |> Map.ofList

                                        if standardErrors |> Map.exists (fun _ value -> not (finite value)) then
                                            Error NonFiniteEstimate
                                        else
                                            let logLikelihood =
                                                let thetaNow = expand free
                                                let mutable total = 0.0

                                                for i in 0 .. count - 1 do
                                                    for j in i + 1 .. count - 1 do
                                                        let winsI, winsJ = winsOf i j

                                                        if winsI > 0.0 then
                                                            total <- total + winsI * log (sigmoid (thetaNow.[i] - thetaNow.[j]))

                                                        if winsJ > 0.0 then
                                                            total <- total + winsJ * log (sigmoid (thetaNow.[j] - thetaNow.[i]))

                                                total

                                            let maxAbs = theta |> Array.fold (fun peak value -> max peak (abs value)) 0.0

                                            let assumptions =
                                                [ "connected-comparison-graph"
                                                  "stable-sigmoid"
                                                  "zero-sum-gauge"
                                                  if input.Regularization > 0.0 then
                                                      "l2-regularization"
                                                  else
                                                      "unregularized-maximum-likelihood"
                                                  if input.Regularization = 0.0 then
                                                      "unregularized-requires-converged-check" ]
                                                |> Set.ofList

                                            Ok
                                                { Strengths = strengths
                                                  Appearances = appearances
                                                  Diagnostics =
                                                    { Iterations = used
                                                      Converged = converged
                                                      LogLikelihood = logLikelihood
                                                      GradientNorm = norm
                                                      MaxAbsStrength = maxAbs
                                                      Regularization = input.Regularization
                                                      RequestedMaxIterations = input.MaxIterations }
                                                  Uncertainty = { StandardErrors = standardErrors }
                                                  Assumptions = assumptions }
