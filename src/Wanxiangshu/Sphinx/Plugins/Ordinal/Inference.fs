namespace Wanxiangshu.Sphinx.Plugins.Ordinal

open System
open FsToolkit.ErrorHandling

module Inference =
    type Ballot = { Ranks: string list list }

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

    type BtlUncertainty = { StandardErrors: Map<string, float> }

    type BtlOutcome =
        { Strengths: Map<string, float>
          Appearances: Map<string, int>
          Diagnostics: BtlDiagnostics
          Uncertainty: BtlUncertainty
          Assumptions: Set<string> }

    /// DSL-class: Vocabulary — stable typed failure-code taxonomy
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

    type private NewtonProgress =
        | NewtonConverged
        | NewtonMaxIterations
        | NewtonAdvance

    let maxNewtonIterations = 100

    let bordaErrorCode (error: BordaError) =
        match error with
        | BordaError.EmptyCandidates -> "empty-candidates"
        | BordaError.EmptyBallots -> "empty-ballots"
        | BordaError.DuplicateCandidate _ -> "duplicate-candidate"
        | BordaError.UnknownCandidate _ -> "unknown-candidate"
        | BordaError.DuplicateRank _ -> "duplicate-rank"
        | BordaError.EmptyTier _ -> "empty-tier"
        | BordaError.EmptyBallot _ -> "empty-ballot"

    let btlErrorCode (error: BtlError) =
        match error with
        | BtlError.EmptyCandidates -> "empty-candidates"
        | BtlError.DuplicateCandidate _ -> "duplicate-candidate"
        | BtlError.EmptyContests -> "empty-contests"
        | BtlError.UnknownCandidate _ -> "unknown-candidate"
        | BtlError.SelfContest _ -> "self-contest"
        | BtlError.NonPositiveWins _ -> "non-positive-wins"
        | BtlError.InvalidRegularization -> "invalid-regularization"
        | BtlError.InvalidTolerance -> "invalid-tolerance"
        | BtlError.InvalidMaxIterations -> "invalid-max-iterations"
        | BtlError.NonFiniteEstimate -> "non-finite-estimate"
        | BtlError.SingularHessian -> "singular-hessian-rank-deficient"
        | BtlError.Unidentifiable _ -> "unidentifiable-disconnected-comparison-graph"

    let private bordaGuarantees =
        [ "ballot-order-invariance"; "candidate-label-equivariance" ]

    let private duplicateFolder (state: Set<string> * string option) (head: string) : Set<string> * string option =
        let seen, found = state

        if found.IsSome then seen, found
        elif Set.contains head seen then seen, Some head
        else Set.add head seen, None

    let private firstDuplicate (items: string list) : string option =
        let _, found = items |> List.fold duplicateFolder (Set.empty, None)
        found

    let private checkBordaCandidatesNonEmpty (candidates: string list) : Result<unit, BordaError> =
        if List.isEmpty candidates then
            Error BordaError.EmptyCandidates
        else
            Ok()

    let private checkBordaCandidatesUnique (candidates: string list) : Result<unit, BordaError> =
        match firstDuplicate candidates with
        | Some candidate -> Error(BordaError.DuplicateCandidate candidate)
        | None -> Ok()

    let private validateBordaCandidates (candidates: string list) : Result<unit, BordaError> =
        result {
            do! checkBordaCandidatesNonEmpty candidates
            do! checkBordaCandidatesUnique candidates
        }

    let private checkBordaBallotsNonEmpty (ballots: Ballot list) : Result<unit, BordaError> =
        if List.isEmpty ballots then
            Error BordaError.EmptyBallots
        else
            Ok()

    let private checkBallotRanksNonEmpty (index: int) (ballot: Ballot) : Result<unit, BordaError> =
        if List.isEmpty ballot.Ranks then
            Error(BordaError.EmptyBallot index)
        else
            Ok()

    let private checkBallotTiersNonEmpty (index: int) (ballot: Ballot) : Result<unit, BordaError> =
        match ballot.Ranks |> List.tryFindIndex List.isEmpty with
        | Some _ -> Error(BordaError.EmptyTier index)
        | None -> Ok()

    let private checkBallotCandidatesKnown
        (known: Set<string>)
        (index: int)
        (ballot: Ballot)
        : Result<unit, BordaError> =
        match
            ballot.Ranks
            |> List.concat
            |> List.tryFind (fun candidate -> not (Set.contains candidate known))
        with
        | Some candidate -> Error(BordaError.UnknownCandidate(index, candidate))
        | None -> Ok()

    let private checkBallotRanksUnique (index: int) (ballot: Ballot) : Result<unit, BordaError> =
        match ballot.Ranks |> List.concat |> firstDuplicate with
        | Some candidate -> Error(BordaError.DuplicateRank(index, candidate))
        | None -> Ok()

    let private validateSingleBordaBallot
        (known: Set<string>)
        (index: int)
        (ballot: Ballot)
        : Result<unit, BordaError> =
        result {
            do! checkBallotRanksNonEmpty index ballot
            do! checkBallotTiersNonEmpty index ballot
            do! checkBallotCandidatesKnown known index ballot
            do! checkBallotRanksUnique index ballot
        }

    let private validateAllBordaBallots (known: Set<string>) (ballots: Ballot list) : Result<unit, BordaError> =
        ballots
        |> List.mapi (fun index (ballot: Ballot) -> index, ballot)
        |> List.traverseResultM (fun (index, (ballot: Ballot)) -> validateSingleBordaBallot known index ballot)
        |> Result.map (fun _ -> ())

    let private validateBordaBallots (known: Set<string>) (ballots: Ballot list) : Result<unit, BordaError> =
        result {
            do! checkBordaBallotsNonEmpty ballots
            do! validateAllBordaBallots known ballots
        }

    let private bordaTierPoints (field: float) (position: int) (tier: string list) : float =
        let size = float tier.Length

        let total =
            [ position .. position + tier.Length - 1 ]
            |> List.sumBy (fun rank -> field - 1.0 - float rank)

        total / size

    let private addTierScores (scores: Map<string, float>) (points: float) (tier: string list) : Map<string, float> =
        tier
        |> List.fold (fun acc candidate -> acc |> Map.add candidate (acc.[candidate] + points)) scores

    let rec private scoreBordaTiers
        (position: int)
        (tiers: string list list)
        (current: Map<string, float>)
        (field: float)
        : Map<string, float> =
        match tiers with
        | [] -> current
        | tier :: rest ->
            let points = bordaTierPoints field position tier
            let updated = addTierScores current points tier
            scoreBordaTiers (position + tier.Length) rest updated field

    let private scoreSingleBordaBallot
        (state: Map<string, float> * Map<string, int>)
        (ballot: Ballot)
        : Map<string, float> * Map<string, int> =
        let scores, exposure = state
        let ranked = ballot.Ranks |> List.concat |> Set.ofList
        let field = float ranked.Count
        let advancedScores = scoreBordaTiers 0 ballot.Ranks scores field

        let advancedExposure =
            ranked
            |> Set.fold (fun acc candidate -> acc |> Map.add candidate (acc.[candidate] + 1)) exposure

        advancedScores, advancedExposure

    let private buildBordaScores
        (candidates: string list)
        (ballots: Ballot list)
        : Map<string, float> * Map<string, int> =
        let zeroScores =
            candidates |> List.map (fun candidate -> candidate, 0.0) |> Map.ofList

        let zeroExposure =
            candidates |> List.map (fun candidate -> candidate, 0) |> Map.ofList

        ballots |> List.fold scoreSingleBordaBallot (zeroScores, zeroExposure)

    let private bordaBallotComplete (known: Set<string>) (ballot: Ballot) : bool =
        (ballot.Ranks |> List.concat |> Set.ofList) = known

    let private bordaIsComplete (known: Set<string>) (ballots: Ballot list) : bool =
        ballots |> List.forall (bordaBallotComplete known)

    let private bordaBallotHasTie (ballot: Ballot) : bool =
        ballot.Ranks |> List.exists (fun tier -> tier.Length > 1)

    let private bordaHasTies (ballots: Ballot list) : bool =
        ballots |> List.exists bordaBallotHasTie

    let private decideBordaExtension (complete: bool) (hasTies: bool) : string =
        if not complete then "appearance-normalized"
        elif hasTies then "fractional-tie"
        else "complete-baseline"

    let private bordaMeanFor (exposure: Map<string, int>) (candidate: string) (total: float) : float =
        match exposure.[candidate] with
        | 0 -> 0.0
        | appearances -> total / float appearances

    let private buildBordaMeans (scores: Map<string, float>) (exposure: Map<string, int>) : Map<string, float> =
        scores |> Map.map (fun candidate total -> bordaMeanFor exposure candidate total)

    let private buildBordaRanking (candidates: string list) (means: Map<string, float>) : string list =
        candidates |> List.sortBy (fun candidate -> -(means.[candidate]), candidate)

    let private bordaCompletenessAssumption (complete: bool) : string list =
        if complete then
            [ "complete-equal-exposure" ]
        else
            [ "incomplete-ballots-appearance-normalized" ]

    let private bordaTieAssumption (hasTies: bool) : string list =
        if hasTies then [ "fractional-tie-averaging" ] else []

    let private bordaUnrankedAssumption (complete: bool) : string list =
        if not complete then [ "unranked-scores-zero" ] else []

    let private buildBordaAssumptions (complete: bool) (hasTies: bool) : Set<string> =
        bordaCompletenessAssumption complete
        @ bordaTieAssumption hasTies
        @ bordaUnrankedAssumption complete
        |> Set.ofList

    let private buildBordaOutcome (input: BordaInput) (known: Set<string>) : BordaOutcome =
        let scores, exposure = buildBordaScores input.Candidates input.Ballots
        let complete = bordaIsComplete known input.Ballots
        let hasTies = bordaHasTies input.Ballots
        let extension = decideBordaExtension complete hasTies
        let means = buildBordaMeans scores exposure
        let ranking = buildBordaRanking input.Candidates means
        let assumptions = buildBordaAssumptions complete hasTies

        { Scores = scores
          MeanScores = means
          Ranking = ranking
          Exposure = exposure
          Extension = extension
          Complete = complete
          Guarantees = bordaGuarantees
          Assumptions = assumptions }

    let borda (input: BordaInput) : Result<BordaOutcome, BordaError> =
        result {
            do! validateBordaCandidates input.Candidates
            let known = Set.ofList input.Candidates
            do! validateBordaBallots known input.Ballots
            return buildBordaOutcome input known
        }

    let private finite (value: float) : bool =
        not (Double.IsNaN value || Double.IsInfinity value)

    let private sigmoid (value: float) : float =
        if value >= 0.0 then
            1.0 / (1.0 + exp (-value))
        else
            let shifted = exp value
            shifted / (1.0 + shifted)

    let private pairKey (first: string) (second: string) : string * string =
        if first <= second then first, second else second, first

    let private checkBtlCandidatesNonEmpty (candidates: string list) : Result<unit, BtlError> =
        if List.isEmpty candidates then
            Error BtlError.EmptyCandidates
        else
            Ok()

    let private checkBtlCandidatesUnique (candidates: string list) : Result<unit, BtlError> =
        match firstDuplicate candidates with
        | Some candidate -> Error(BtlError.DuplicateCandidate candidate)
        | None -> Ok()

    let private validateBtlCandidates (candidates: string list) : Result<unit, BtlError> =
        result {
            do! checkBtlCandidatesNonEmpty candidates
            do! checkBtlCandidatesUnique candidates
        }

    let private checkBtlContestsNonEmpty (contests: Contest list) : Result<unit, BtlError> =
        if List.isEmpty contests then
            Error BtlError.EmptyContests
        else
            Ok()

    let private validateSingleContest (known: Set<string>) (contest: Contest) : Result<unit, BtlError> =
        if not (Set.contains contest.First known) then
            Error(BtlError.UnknownCandidate contest.First)
        elif not (Set.contains contest.Second known) then
            Error(BtlError.UnknownCandidate contest.Second)
        elif contest.First = contest.Second then
            Error(BtlError.SelfContest contest.First)
        elif
            contest.FirstWins < 0
            || contest.SecondWins < 0
            || contest.FirstWins + contest.SecondWins <= 0
        then
            Error(BtlError.NonPositiveWins(sprintf "%s>%s" contest.First contest.Second))
        else
            Ok()

    let private validateAllContests (known: Set<string>) (contests: Contest list) : Result<unit, BtlError> =
        contests
        |> List.traverseResultM (fun (contest: Contest) -> validateSingleContest known contest)
        |> Result.map (fun _ -> ())

    let private validateBtlContests (known: Set<string>) (contests: Contest list) : Result<unit, BtlError> =
        result {
            do! checkBtlContestsNonEmpty contests
            do! validateAllContests known contests
        }

    let private checkBtlRegularization (regularization: float) : Result<unit, BtlError> =
        if not (finite regularization) || regularization < 0.0 then
            Error BtlError.InvalidRegularization
        else
            Ok()

    let private checkBtlTolerance (tolerance: float) : Result<unit, BtlError> =
        if not (finite tolerance) || tolerance <= 0.0 then
            Error BtlError.InvalidTolerance
        else
            Ok()

    let private checkBtlMaxIterations (maxIterations: int) : Result<unit, BtlError> =
        if maxIterations <= 0 then
            Error BtlError.InvalidMaxIterations
        else
            Ok()

    let private validateBtlScalars (input: BtlInput) : Result<unit, BtlError> =
        result {
            do! checkBtlRegularization input.Regularization
            do! checkBtlTolerance input.Tolerance
            do! checkBtlMaxIterations input.MaxIterations
        }

    let private orderContestWins (firstWins: int) (secondWins: int) (firstBeforeSecond: bool) : int * int =
        if firstBeforeSecond then
            firstWins, secondWins
        else
            secondWins, firstWins

    let private addContestToTotals
        (acc: Map<string * string, int * int>)
        (contest: Contest)
        : Map<string * string, int * int> =
        let key = pairKey contest.First contest.Second
        let priorFirst, priorSecond = acc |> Map.tryFind key |> Option.defaultValue (0, 0)

        let orderedFirst, orderedSecond =
            orderContestWins contest.FirstWins contest.SecondWins (contest.First <= contest.Second)

        acc |> Map.add key (priorFirst + orderedFirst, priorSecond + orderedSecond)

    let private buildContestTotals (contests: Contest list) : Map<string * string, int * int> =
        contests |> List.fold addContestToTotals Map.empty

    let private buildNeighborMap (totals: Map<string * string, int * int>) : Map<string, Set<string>> =
        totals
        |> Map.toList
        |> List.collect (fun ((first, second), _) -> [ first, second; second, first ])
        |> List.groupBy fst
        |> List.map (fun (candidate, pairs) -> candidate, pairs |> List.map snd |> Set.ofList)
        |> Map.ofList

    let rec private reachConnected
        (neighbors: Map<string, Set<string>>)
        (visited: Set<string>)
        (frontier: string list)
        : Set<string> =
        match frontier with
        | [] -> visited
        | candidate :: rest ->
            let advancedVisited = reachFrontier neighbors visited candidate rest
            advancedVisited

    and private reachFrontier
        (neighbors: Map<string, Set<string>>)
        (visited: Set<string>)
        (candidate: string)
        (rest: string list)
        : Set<string> =
        if Set.contains candidate visited then
            reachConnected neighbors visited rest
        else
            let next = neighbors |> Map.tryFind candidate |> Option.defaultValue Set.empty
            reachConnected neighbors (Set.add candidate visited) (rest @ (next |> Set.toList))

    let rec private gatherComponents
        (neighbors: Map<string, Set<string>>)
        (unvisited: Set<string>)
        (acc: Set<string> list)
        : Set<string> list =
        match unvisited |> Set.toList |> List.sort with
        | [] -> acc
        | candidate :: _ ->
            let group = reachConnected neighbors Set.empty [ candidate ]
            gatherComponents neighbors (unvisited - group) (group :: acc)

    let private buildComparisonGroups (known: Set<string>) (neighbors: Map<string, Set<string>>) : Set<string> list =
        gatherComponents neighbors known []

    let private formatDisconnectedGroups (groups: Set<string> list) : string list list =
        groups
        |> List.map (fun group -> group |> Set.toList |> List.sort)
        |> List.sortBy List.head

    let private checkConnectivity (groups: Set<string> list) : Result<unit, BtlError> =
        if groups.Length > 1 then
            groups |> formatDisconnectedGroups |> BtlError.Unidentifiable |> Error
        else
            Ok()

    let private contestAppearanceContribution
        (candidate: string)
        (first: string)
        (second: string)
        (winsFirst: int)
        (winsSecond: int)
        : int =
        if candidate = first || candidate = second then
            winsFirst + winsSecond
        else
            0

    let private appearanceTotalFor (candidate: string) (totals: Map<string * string, int * int>) : int =
        totals
        |> Map.toList
        |> List.sumBy (fun ((first, second), (winsFirst, winsSecond)) ->
            contestAppearanceContribution candidate first second winsFirst winsSecond)

    let private buildAppearances (sorted: string list) (totals: Map<string * string, int * int>) : Map<string, int> =
        sorted
        |> List.map (fun candidate -> candidate, appearanceTotalFor candidate totals)
        |> Map.ofList

    let private orderBtlWins (first: string) (second: string) (winsFirst: int) (winsSecond: int) : float * float =
        if first <= second then
            float winsFirst, float winsSecond
        else
            float winsSecond, float winsFirst

    let private btlWinsOf
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (i: int)
        (j: int)
        : float * float =
        let first, second = sorted.[i], sorted.[j]
        let key = pairKey first second

        match totals |> Map.tryFind key with
        | None -> 0.0, 0.0
        | Some(winsFirst, winsSecond) -> orderBtlWins first second winsFirst winsSecond

    let private expandEntry (free: float array) (count: int) (i: int) : float =
        if i < count - 1 then free.[i] else -(free |> Array.sum)

    let private expandFree (free: float array) (count: int) : float array =
        Array.init count (fun i -> expandEntry free count i)

    let private upperPairIndices (count: int) : (int * int) list =
        List.init count id
        |> List.collect (fun i -> List.init (count - i - 1) (fun k -> i, i + 1 + k))

    let private otherIndices (count: int) (i: int) : int list =
        List.init count id |> List.filter (fun j -> j <> i)

    let private accumulateFirstWin (acc: float) (winsI: float) (difference: float) : float =
        if winsI > 0.0 then
            acc + winsI * log (sigmoid difference)
        else
            acc

    let private accumulateSecondWin (acc: float) (winsJ: float) (difference: float) : float =
        if winsJ > 0.0 then
            acc + winsJ * log (sigmoid (-difference))
        else
            acc

    let private accumulatePairLogLikelihood
        (theta: float array)
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (acc: float)
        (pair: int * int)
        : float =
        let i, j = pair
        let winsI, winsJ = btlWinsOf totals sorted i j
        let difference = theta.[i] - theta.[j]
        let accFirst = accumulateFirstWin acc winsI difference
        accumulateSecondWin accFirst winsJ difference

    let private sumPairLogLikelihood
        (theta: float array)
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (count: int)
        : float =
        upperPairIndices count
        |> List.fold (accumulatePairLogLikelihood theta totals sorted) 0.0

    let private penalizedLogLikelihoodFor
        (theta: float array)
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (count: int)
        (regularization: float)
        : float =
        let total = sumPairLogLikelihood theta totals sorted count

        let penalty =
            (theta |> Array.sumBy (fun value -> value * value)) * regularization / 2.0

        total - penalty

    let private addGradientMeeting
        (acc: float)
        (winsI: float)
        (meetings: float)
        (theta: float array)
        (i: int)
        (j: int)
        : float =
        if meetings > 0.0 then
            acc + winsI - meetings * sigmoid (theta.[i] - theta.[j])
        else
            acc

    let private accumulateGradientFor
        (theta: float array)
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (i: int)
        (acc: float)
        (j: int)
        : float =
        let winsI, winsJ = btlWinsOf totals sorted i j
        let meetings = winsI + winsJ
        addGradientMeeting acc winsI meetings theta i j

    let private gradientRawFor
        (theta: float array)
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (count: int)
        (i: int)
        : float =
        otherIndices count i
        |> List.fold (accumulateGradientFor theta totals sorted i) 0.0

    let private buildGradientRaw
        (theta: float array)
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (count: int)
        : float array =
        Array.init count (fun i -> gradientRawFor theta totals sorted count i)

    let private gradientEntry
        (raw: float array)
        (theta: float array)
        (count: int)
        (regularization: float)
        (k: int)
        : float =
        raw.[k] - raw.[count - 1] - regularization * (theta.[k] - theta.[count - 1])

    let private buildGradient
        (free: float array)
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (count: int)
        (regularization: float)
        : float array =
        let theta = expandFree free count
        let freeCount = count - 1
        let raw = buildGradientRaw theta totals sorted count
        Array.init freeCount (fun k -> gradientEntry raw theta count regularization k)

    let private scaleHessianMeeting (meetings: float) (theta: float array) (i: int) (j: int) : float =
        if meetings <= 0.0 then
            0.0
        else
            let spread = sigmoid (theta.[i] - theta.[j])
            meetings * spread * (1.0 - spread)

    let private hessianOffDiag
        (theta: float array)
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (i: int)
        (j: int)
        : float =
        let winsI, winsJ = btlWinsOf totals sorted i j
        let meetings = winsI + winsJ
        scaleHessianMeeting meetings theta i j

    let private buildHessianRaw
        (theta: float array)
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (count: int)
        : float[][] =
        Array.init count (fun i -> Array.init count (fun j -> hessianOffDiag theta totals sorted i j))

    let private hessianRowSum (raw: float[][]) (count: int) (i: int) : float =
        List.init count id
        |> List.filter (fun j -> j <> i)
        |> List.fold (fun acc j -> acc + raw.[i].[j]) 0.0

    let private hessianRawEntry (raw: float[][]) (i: int) (j: int) (rowSum: float) : float =
        if i = j then -rowSum else raw.[i].[j]

    let private buildHessianRawWithDiag (raw: float[][]) (count: int) : float[][] =
        Array.init count (fun i -> Array.init count (fun j -> hessianRawEntry raw i j (hessianRowSum raw count i)))

    let private adjustFreeHessian (baseValue: float) (regularization: float) (isDiag: bool) : float =
        if isDiag then
            baseValue - regularization * 2.0
        else
            baseValue - regularization

    let private freeHessianEntry (raw: float[][]) (count: int) (regularization: float) (k: int) (l: int) : float =
        let baseValue =
            raw.[k].[l] - raw.[k].[count - 1] - raw.[count - 1].[l]
            + raw.[count - 1].[count - 1]

        adjustFreeHessian baseValue regularization (k = l)

    let private buildHessian
        (free: float array)
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (count: int)
        (regularization: float)
        : float[][] =
        let theta = expandFree free count
        let freeCount = count - 1
        let raw = buildHessianRaw theta totals sorted count
        let withDiag = buildHessianRawWithDiag raw count

        Array.init freeCount (fun k ->
            Array.init freeCount (fun l -> freeHessianEntry withDiag count regularization k l))

    let private findPivotRow (work: float[][]) (size: int) (column: int) : int =
        [ column .. size - 1 ] |> List.maxBy (fun row -> abs work.[row].[column])

    let private isPivotSingular (work: float[][]) (pivot: int) (column: int) : bool = abs work.[pivot].[column] < 1e-12

    let private swapPivotRow (work: float[][]) (vector: float[]) (size: int) (column: int) (pivot: int) : unit =
        // DSL-MUTABLE: algorithm-scratch
        for row in 0 .. size - 1 do
            let tmp = work.[column].[row]
            work.[column].[row] <- work.[pivot].[row]
            work.[pivot].[row] <- tmp

        let swap = vector.[column]
        vector.[column] <- vector.[pivot]
        vector.[pivot] <- swap

    let private eliminateRowCells (work: float[][]) (size: int) (column: int) (row: int) (factor: float) : unit =
        // DSL-MUTABLE: algorithm-scratch
        for col in column + 1 .. size - 1 do
            work.[row].[col] <- work.[row].[col] - factor * work.[column].[col]

    let private eliminateSingleRow (work: float[][]) (vector: float[]) (size: int) (column: int) (row: int) : unit =
        let factor = work.[row].[column] / work.[column].[column]
        work.[row].[column] <- 0.0
        eliminateRowCells work size column row factor
        vector.[row] <- vector.[row] - factor * vector.[column]

    let private eliminateColumnRows (work: float[][]) (vector: float[]) (size: int) (column: int) : unit =
        // DSL-MUTABLE: algorithm-scratch
        for row in column + 1 .. size - 1 do
            eliminateSingleRow work vector size column row

    let rec private runEliminate (work: float[][]) (vector: float[]) (size: int) (column: int) : unit option =
        if column >= size then
            Some()
        else
            eliminateAtColumn work vector size column

    and private eliminateAtColumn (work: float[][]) (vector: float[]) (size: int) (column: int) : unit option =
        let pivot = findPivotRow work size column

        if isPivotSingular work pivot column then
            None
        else
            applyEliminationStep work vector size column pivot

    and private applyEliminationStep
        (work: float[][])
        (vector: float[])
        (size: int)
        (column: int)
        (pivot: int)
        : unit option =
        swapPivotRow work vector size column pivot
        eliminateColumnRows work vector size column
        runEliminate work vector size (column + 1)

    let private solveBackRow (work: float[][]) (vector: float[]) (solution: float[]) (size: int) (row: int) : unit =
        // DSL-MUTABLE: algorithm-scratch
        let mutable acc = vector.[row]

        for col in row + 1 .. size - 1 do
            acc <- acc - work.[row].[col] * solution.[col]

        solution.[row] <- acc / work.[row].[row]

    let private backSubstitute (work: float[][]) (vector: float[]) (solution: float[]) (size: int) : unit =
        // DSL-MUTABLE: algorithm-scratch
        for row in size - 1 .. -1 .. 0 do
            solveBackRow work vector solution size row

    let private backSubstituteSolution (work: float[][]) (vector: float[]) (size: int) : float[] =
        let solution = Array.zeroCreate size
        backSubstitute work vector solution size
        solution

    let private finishElimination (work: float[][]) (vector: float[]) (size: int) : float[] option =
        match runEliminate work vector size 0 with
        | None -> None
        | Some() -> Some(backSubstituteSolution work vector size)

    let private solveLinear (matrix: float[][]) (right: float[]) : float[] option =
        let size = right.Length
        let work = matrix |> Array.map Array.copy
        let vector = Array.copy right
        finishElimination work vector size

    let private decideNewtonAction (norm: float) (tolerance: float) (used: int) (iterations: int) : NewtonProgress =
        if norm < tolerance then NewtonConverged
        elif used >= iterations then NewtonMaxIterations
        else NewtonAdvance

    let private newtonConvergedResult
        (free: float array)
        (used: int)
        (norm: float)
        : Result<float array * int * float * bool, BtlError> =
        Ok(free, used, norm, true)

    let private newtonExhaustedResult
        (free: float array)
        (used: int)
        (norm: float)
        : Result<float array * int * float * bool, BtlError> =
        Ok(free, used, norm, false)

    let private solveNewtonStep (hessian: float[][]) (negGrad: float[]) : Result<float[], BtlError> =
        // L-2: a singular Newton system on a connected graph is rank
        // deficiency, not a non-finite number: label it as such.
        match solveLinear hessian negGrad with
        | None -> Error BtlError.SingularHessian
        | Some step -> Ok step

    let private checkNewtonStepFinite (step: float[]) : Result<float[], BtlError> =
        if step |> Array.exists (fun value -> not (finite value)) then
            Error BtlError.NonFiniteEstimate
        else
            Ok step

    let private dampenTrialVector (free: float array) (step: float[]) (freeCount: int) (factor: float) : float array =
        Array.init freeCount (fun k -> free.[k] + factor * step.[k])

    let private isTrialFinite (trial: float array) : bool =
        trial |> Array.exists (fun value -> not (finite value)) |> not

    let rec private dampenNewton
        (free: float array)
        (step: float[])
        (freeCount: int)
        (current: float)
        (penalized: float array -> float)
        (factor: float)
        (attempts: int)
        : float array option =
        if attempts <= 0 then
            None
        else
            dampenNewtonAttempt free step freeCount current penalized factor attempts

    and private dampenNewtonAttempt
        (free: float array)
        (step: float[])
        (freeCount: int)
        (current: float)
        (penalized: float array -> float)
        (factor: float)
        (attempts: int)
        : float array option =
        let trial = dampenTrialVector free step freeCount factor

        if isTrialFinite trial then
            evaluateDampenTrial free step freeCount current penalized factor attempts trial
        else
            dampenNewton free step freeCount current penalized (factor / 2.0) (attempts - 1)

    and private evaluateDampenTrial
        (free: float array)
        (step: float[])
        (freeCount: int)
        (current: float)
        (penalized: float array -> float)
        (factor: float)
        (attempts: int)
        (trial: float array)
        : float array option =
        let value = penalized trial

        if value >= current - 1e-12 then
            Some trial
        else
            dampenNewton free step freeCount current penalized (factor / 2.0) (attempts - 1)

    let private gradientNorm (grad: float array) : float =
        grad |> Array.fold (fun peak value -> max peak (abs value)) 0.0

    let rec private iterateNewton
        (free: float array)
        (used: int)
        (tolerance: float)
        (iterations: int)
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (count: int)
        (regularization: float)
        : Result<float array * int * float * bool, BtlError> =
        let grad = buildGradient free totals sorted count regularization
        let norm = gradientNorm grad
        dispatchNewtonAction free used norm tolerance iterations totals sorted count regularization

    and private dispatchNewtonAction
        (free: float array)
        (used: int)
        (norm: float)
        (tolerance: float)
        (iterations: int)
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (count: int)
        (regularization: float)
        : Result<float array * int * float * bool, BtlError> =
        match decideNewtonAction norm tolerance used iterations with
        | NewtonConverged -> newtonConvergedResult free used norm
        | NewtonMaxIterations -> newtonExhaustedResult free used norm
        | NewtonAdvance -> advanceNewton free used norm tolerance iterations totals sorted count regularization

    and private advanceNewton
        (free: float array)
        (used: int)
        (norm: float)
        (tolerance: float)
        (iterations: int)
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (count: int)
        (regularization: float)
        : Result<float array * int * float * bool, BtlError> =
        result {
            let hessian = buildHessian free totals sorted count regularization
            let grad = buildGradient free totals sorted count regularization
            let! step = solveNewtonStep hessian (grad |> Array.map (fun value -> -value))
            let! finiteStep = checkNewtonStepFinite step
            return! dampenAndContinue free used norm tolerance iterations totals sorted count regularization finiteStep
        }

    and private dampenAndContinue
        (free: float array)
        (used: int)
        (norm: float)
        (tolerance: float)
        (iterations: int)
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (count: int)
        (regularization: float)
        (step: float[])
        : Result<float array * int * float * bool, BtlError> =
        let freeCount = count - 1

        let current =
            penalizedLogLikelihoodFor (expandFree free count) totals sorted count regularization

        let penalized = dampenPenalized totals sorted count regularization

        match dampenNewton free step freeCount current penalized 1.0 40 with
        | None -> newtonExhaustedResult free used norm
        | Some next -> iterateNewton next (used + 1) tolerance iterations totals sorted count regularization

    and private dampenPenalized
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (count: int)
        (regularization: float)
        (trial: float array)
        : float =
        penalizedLogLikelihoodFor (expandFree trial count) totals sorted count regularization

    let private checkFreeCount (sorted: string list) (freeCount: int) : Result<unit, BtlError> =
        if freeCount = 0 then
            [ sorted ] |> BtlError.Unidentifiable |> Error
        else
            Ok()

    let private checkThetaFinite (theta: float array) : Result<unit, BtlError> =
        if theta |> Array.exists (fun value -> not (finite value)) then
            Error BtlError.NonFiniteEstimate
        else
            Ok()

    let private unitEntry (row: int) (col: int) : float = if row = col then 1.0 else 0.0

    let private unitVectorFor (size: int) (col: int) : float[] =
        Array.init size (fun row -> unitEntry row col)

    let rec private runInvertColumns
        (matrix: float[][])
        (size: int)
        (col: int)
        (acc: float[] list)
        : float[] list option =
        if col >= size then
            Some acc
        else
            invertSingleColumn matrix size col acc

    and private invertSingleColumn
        (matrix: float[][])
        (size: int)
        (col: int)
        (acc: float[] list)
        : float[] list option =
        let unit = unitVectorFor size col

        match solveLinear matrix unit with
        | None -> None
        | Some column -> runInvertColumns matrix size (col + 1) (column :: acc)

    let private invertHessian (matrix: float[][]) (size: int) : float[] list option = runInvertColumns matrix size 0 []

    let private missingStandardErrors (sorted: string list) : Map<string, float> =
        sorted |> List.map (fun candidate -> candidate, Double.NaN) |> Map.ofList

    let private freeVarianceFor (rows: float[][]) (freeCount: int) (k: int) : float =
        if k < freeCount then max 0.0 (-rows.[k].[k]) else 0.0

    let private variancePairIndices (freeCount: int) : (int * int) list =
        List.init freeCount id
        |> List.collect (fun k -> List.init freeCount (fun l -> k, l))

    let private lastVarianceFor (rows: float[][]) (freeCount: int) : float =
        let pairs = variancePairIndices freeCount
        let total = pairs |> List.fold (fun acc (k, l) -> acc + -rows.[k].[l]) 0.0
        max 0.0 total

    let private varianceForCandidate (rows: float[][]) (freeCount: int) (lastVariance: float) (i: int) : float =
        if i < freeCount then
            freeVarianceFor rows freeCount i
        else
            lastVariance

    let private buildStandardErrorsFromRows
        (rows: float[][])
        (sorted: string list)
        (freeCount: int)
        : Map<string, float> =
        let lastVariance = lastVarianceFor rows freeCount

        sorted
        |> List.mapi (fun i candidate -> candidate, sqrt (varianceForCandidate rows freeCount lastVariance i))
        |> Map.ofList

    let private buildStandardErrors
        (inverse: float[] list option)
        (sorted: string list)
        (freeCount: int)
        : Map<string, float> =
        match inverse with
        | None -> missingStandardErrors sorted
        | Some columns ->
            let rows = columns |> List.rev |> List.toArray
            buildStandardErrorsFromRows rows sorted freeCount

    let private checkStandardErrorsFinite (standardErrors: Map<string, float>) : Result<unit, BtlError> =
        if standardErrors |> Map.exists (fun _ value -> not (finite value)) then
            Error BtlError.NonFiniteEstimate
        else
            Ok()

    let private buildUnpenalizedLogLikelihood
        (free: float array)
        (totals: Map<string * string, int * int>)
        (sorted: string list)
        (count: int)
        : float =
        sumPairLogLikelihood (expandFree free count) totals sorted count

    let private maxAbsStrength (theta: float array) : float =
        theta |> Array.fold (fun peak value -> max peak (abs value)) 0.0

    let private regularizationAssumption (regularization: float) : string =
        if regularization > 0.0 then
            "l2-regularization"
        else
            "unregularized-maximum-likelihood"

    let private convergenceAssumption (regularization: float) : string list =
        if regularization = 0.0 then
            [ "unregularized-requires-converged-check" ]
        else
            []

    let private buildBtlAssumptions (regularization: float) : Set<string> =
        [ "connected-comparison-graph"
          "stable-sigmoid"
          "zero-sum-gauge"
          regularizationAssumption regularization ]
        @ convergenceAssumption regularization
        |> Set.ofList

    let private buildBtlStrengths (sorted: string list) (theta: float array) : Map<string, float> =
        sorted |> List.mapi (fun i candidate -> candidate, theta.[i]) |> Map.ofList

    let private buildFinalBtlOutcome
        (strengths: Map<string, float>)
        (appearances: Map<string, int>)
        (used: int)
        (converged: bool)
        (logLikelihood: float)
        (norm: float)
        (maxAbs: float)
        (input: BtlInput)
        (standardErrors: Map<string, float>)
        (assumptions: Set<string>)
        : BtlOutcome =
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

    let private buildBtlOutcomeParts
        (sorted: string list)
        (appearances: Map<string, int>)
        (theta: float array)
        (free: float array)
        (used: int)
        (converged: bool)
        (norm: float)
        (totals: Map<string * string, int * int>)
        (count: int)
        (freeCount: int)
        (input: BtlInput)
        : Result<BtlOutcome, BtlError> =
        result {
            let strengths = buildBtlStrengths sorted theta
            let matrix = buildHessian free totals sorted count input.Regularization
            let inverse = invertHessian matrix freeCount
            let standardErrors = buildStandardErrors inverse sorted freeCount
            do! checkStandardErrorsFinite standardErrors
            let logLikelihood = buildUnpenalizedLogLikelihood free totals sorted count
            let maxAbs = maxAbsStrength theta
            let assumptions = buildBtlAssumptions input.Regularization

            return
                buildFinalBtlOutcome
                    strengths
                    appearances
                    used
                    converged
                    logLikelihood
                    norm
                    maxAbs
                    input
                    standardErrors
                    assumptions
        }

    let bradleyTerry (input: BtlInput) : Result<BtlOutcome, BtlError> =
        result {
            do! validateBtlCandidates input.Candidates
            let known = Set.ofList input.Candidates
            do! validateBtlContests known input.Contests
            do! validateBtlScalars input
            let sorted = input.Candidates |> List.sort
            let count = sorted.Length
            let totals = buildContestTotals input.Contests
            let neighbors = buildNeighborMap totals
            let groups = buildComparisonGroups known neighbors
            do! checkConnectivity groups
            let appearances = buildAppearances sorted totals
            let iterations = min input.MaxIterations maxNewtonIterations
            let freeCount = count - 1
            do! checkFreeCount sorted freeCount

            let! free, used, norm, converged =
                iterateNewton
                    (Array.zeroCreate freeCount)
                    0
                    input.Tolerance
                    iterations
                    totals
                    sorted
                    count
                    input.Regularization

            let theta = expandFree free count
            do! checkThetaFinite theta
            return! buildBtlOutcomeParts sorted appearances theta free used converged norm totals count freeCount input
        }
