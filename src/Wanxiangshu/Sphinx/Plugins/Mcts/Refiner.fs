// WHAT[EPI-010]: MCTS refiner runs a seeded fixed-budget search over a finite generative model.

namespace Wanxiangshu.Sphinx.Plugins.Mcts

open System
open Wanxiangshu.Sphinx.Core

module Refiner =

    type Outcome =
        { Next: string
          Probability: float
          Reward: float }

    type KernelEntry =
        { State: string
          Action: string
          Outcomes: Outcome list }

    type GenerativeModel =
        { Root: string
          Actions: Map<string, string list>
          Transitions: KernelEntry list
          Horizon: int
          Discount: float
          RewardLo: float
          RewardHi: float }

    type SearchConfig =
        { Iterations: int
          Exploration: float
          Delta: float
          Seed: int
          DagSafe: bool }

    type ActionStats =
        { Action: string
          Visits: int
          Mean: float
          Variance: float
          Radius: float }

    // M-2: the per-action radius is a fixed-time Hoeffding bound with no union
    // correction, so Scope pins the only valid reading; DagSafe sharing is
    // unweighted, so Assumptions records it instead of claiming reweighting.
    type Coverage =
        { Iterations: int
          Horizon: int
          Discount: float
          RewardLo: float
          RewardHi: float
          ReturnWidth: float
          Delta: float
          DagSafe: bool
          Scope: string
          Assumptions: Set<string> }

    type SearchResult =
        { BestAction: string option
          RootVisits: int
          ActionStats: ActionStats list
          Coverage: Coverage
          Seed: int }

    [<RequireQualifiedAccess>]
    type MctsFault =
        | DuplicateAction of state: string * action: string
        | DuplicateKernelEntry of state: string * action: string
        | MissingKernelEntry of state: string * action: string
        | UnknownActionEntry of state: string * action: string
        | EmptyOutcomes of state: string * action: string
        | InvalidProbability of state: string * action: string * next: string
        | InvalidDistribution of state: string * action: string
        | RewardOutOfBounds of state: string * action: string * next: string
        | InvalidRewardRange
        | InvalidHorizon of value: int
        | InvalidDiscount of value: float
        | InvalidIterations of value: int
        | InvalidExploration of value: float
        | InvalidDelta of value: float

    let code (fault: MctsFault) : string =
        match fault with
        | MctsFault.DuplicateAction _ -> "duplicate-action"
        | MctsFault.DuplicateKernelEntry _ -> "duplicate-kernel-entry"
        | MctsFault.MissingKernelEntry _ -> "missing-kernel-entry"
        | MctsFault.UnknownActionEntry _ -> "unknown-action-entry"
        | MctsFault.EmptyOutcomes _ -> "empty-outcomes"
        | MctsFault.InvalidProbability _ -> "invalid-probability"
        | MctsFault.InvalidDistribution _ -> "invalid-distribution"
        | MctsFault.RewardOutOfBounds _ -> "reward-out-of-bounds"
        | MctsFault.InvalidRewardRange -> "invalid-reward-range"
        | MctsFault.InvalidHorizon _ -> "invalid-horizon"
        | MctsFault.InvalidDiscount _ -> "invalid-discount"
        | MctsFault.InvalidIterations _ -> "invalid-iterations"
        | MctsFault.InvalidExploration _ -> "invalid-exploration"
        | MctsFault.InvalidDelta _ -> "invalid-delta"

    let message (fault: MctsFault) : string =
        match fault with
        | MctsFault.DuplicateAction (state, action) ->
            sprintf "state %s lists action %s twice; action sets must be finite and distinct" state action
        | MctsFault.DuplicateKernelEntry (state, action) ->
            sprintf "kernel declares (%s, %s) twice; each state-action pair needs exactly one entry" state action
        | MctsFault.MissingKernelEntry (state, action) ->
            sprintf "kernel misses an entry for declared action %s in state %s" action state
        | MctsFault.UnknownActionEntry (state, action) ->
            sprintf "kernel entry (%s, %s) names an action the state does not declare" state action
        | MctsFault.EmptyOutcomes (state, action) ->
            sprintf "kernel entry (%s, %s) must list at least one outcome" state action
        | MctsFault.InvalidProbability (state, action, next) ->
            sprintf "outcome %s of (%s, %s) must carry a finite nonnegative probability" next state action
        | MctsFault.InvalidDistribution (state, action) ->
            sprintf "outcome probabilities of (%s, %s) must sum to one" state action
        | MctsFault.RewardOutOfBounds (state, action, next) ->
            sprintf "reward of outcome %s of (%s, %s) leaves the declared reward range" next state action
        | MctsFault.InvalidRewardRange -> "reward range needs finite bounds with lower <= upper"
        | MctsFault.InvalidHorizon value -> sprintf "horizon must be at least one step (got %d)" value
        | MctsFault.InvalidDiscount _ -> "discount must be a finite number in [0, 1]"
        | MctsFault.InvalidIterations value -> sprintf "fixed simulation budget must be at least one iteration (got %d)" value
        | MctsFault.InvalidExploration _ -> "exploration must be a finite nonnegative number"
        | MctsFault.InvalidDelta _ -> "coverage delta must lie strictly between zero and one"

    let toCoreError (fault: MctsFault) : CoreError =
        { Code = code fault
          Message = message fault }

    /// Width of the finite-horizon discounted return interval.
    let returnWidth (rewardLo: float) (rewardHi: float) (horizon: int) (discount: float) : float =
        let span = rewardHi - rewardLo

        if discount = 1.0 then
            span * float horizon
        else
            let _, geometric =
                [ 1..horizon ]
                |> List.fold (fun (level, sum) _ -> level * discount, sum + level) (1.0, 0.0)

            span * geometric

    /// UCT score with exploration scaled to the return width; unvisited actions score infinite.
    let uctValue (exploration: float) (returnWidth: float) (parentVisits: int) (visits: int) (mean: float) : float =
        if visits <= 0 then
            Double.PositiveInfinity
        elif parentVisits <= 1 then
            mean
        else
            mean + exploration * returnWidth * sqrt (log (float parentVisits) / float visits)

    let private isFiniteNumber (value: float) : bool =
        not (Double.IsNaN value || Double.IsInfinity value)

    /// Park-Miller generator over IEEE-754 doubles; every product stays below 2^53, so the
    /// stream is bit-identical on runtimes with correct double arithmetic.
    type private Prng = { State: int }

    let private seedFrom (seed: int) : Prng =
        let range = 2147483646
        { State = ((seed % range) + range) % range + 1 }

    let private nextUniform (prng: Prng) : float * Prng =
        let product = float prng.State * 48271.0
        let state = int (product - floor (product / 2147483647.0) * 2147483647.0)
        float state / 2147483647.0, { State = state }

    let private sample (outcomes: Outcome list) (u: float) : Outcome =
        let rec walk remaining cumulative =
            match remaining with
            | [] -> List.last outcomes
            | outcome :: rest ->
                let edge = cumulative + outcome.Probability

                if u < edge then
                    outcome
                else
                    walk rest edge

        walk outcomes 0.0

    type private Kernel = Map<string * string, Outcome list>

    let private checkEntry (model: GenerativeModel) (entry: KernelEntry) : Result<unit, MctsFault> =
        let declared =
            model.Actions |> Map.tryFind entry.State |> Option.defaultValue []

        if not (List.contains entry.Action declared) then
            Error(MctsFault.UnknownActionEntry(entry.State, entry.Action))
        elif List.isEmpty entry.Outcomes then
            Error(MctsFault.EmptyOutcomes(entry.State, entry.Action))
        else
            match
                entry.Outcomes
                |> List.tryFind (fun outcome -> not (isFiniteNumber outcome.Probability) || outcome.Probability < 0.0)
            with
            | Some bad -> Error(MctsFault.InvalidProbability(entry.State, entry.Action, bad.Next))
            | None ->
                match
                    entry.Outcomes
                    |> List.tryFind (fun outcome ->
                        not (isFiniteNumber outcome.Reward)
                        || outcome.Reward < model.RewardLo
                        || outcome.Reward > model.RewardHi)
                with
                | Some bad -> Error(MctsFault.RewardOutOfBounds(entry.State, entry.Action, bad.Next))
                | None ->
                    let total =
                        entry.Outcomes |> List.fold (fun sum outcome -> sum + outcome.Probability) 0.0

                    if abs (total - 1.0) > 1e-9 then
                        Error(MctsFault.InvalidDistribution(entry.State, entry.Action))
                    else
                        Ok()

    let private validateModel (model: GenerativeModel) : Result<Kernel, MctsFault> =
        if
            not (isFiniteNumber model.RewardLo)
            || not (isFiniteNumber model.RewardHi)
            || model.RewardLo > model.RewardHi
        then
            Error MctsFault.InvalidRewardRange
        elif model.Horizon < 1 then
            Error(MctsFault.InvalidHorizon model.Horizon)
        elif not (isFiniteNumber model.Discount) || model.Discount < 0.0 || model.Discount > 1.0 then
            Error(MctsFault.InvalidDiscount model.Discount)
        else
            match
                model.Actions
                |> Map.toList
                |> List.tryPick (fun (state, actions) ->
                    actions
                    |> List.countBy id
                    |> List.tryFind (fun (_, count) -> count > 1)
                    |> Option.map (fun (action, _) -> state, action))
            with
            | Some (state, action) -> Error(MctsFault.DuplicateAction(state, action))
            | None ->
                let rec insert (kernel: Kernel) (remaining: KernelEntry list) : Result<Kernel, MctsFault> =
                    match remaining with
                    | [] -> Ok kernel
                    | entry :: rest ->
                        let key = entry.State, entry.Action

                        if Map.containsKey key kernel then
                            Error(MctsFault.DuplicateKernelEntry(entry.State, entry.Action))
                        else
                            match checkEntry model entry with
                            | Error fault -> Error fault
                            | Ok () -> insert (Map.add key entry.Outcomes kernel) rest

                match insert Map.empty model.Transitions with
                | Error fault -> Error fault
                | Ok kernel ->
                    match
                        model.Actions
                        |> Map.toList
                        |> List.tryPick (fun (state, actions) ->
                            actions
                            |> List.tryFind (fun action -> not (Map.containsKey (state, action) kernel))
                            |> Option.map (fun action -> state, action))
                    with
                    | Some (state, action) -> Error(MctsFault.MissingKernelEntry(state, action))
                    | None -> Ok kernel

    let private validateConfig (config: SearchConfig) : Result<unit, MctsFault> =
        if config.Iterations < 1 then
            Error(MctsFault.InvalidIterations config.Iterations)
        elif not (isFiniteNumber config.Exploration) || config.Exploration < 0.0 then
            Error(MctsFault.InvalidExploration config.Exploration)
        elif not (isFiniteNumber config.Delta) || config.Delta <= 0.0 || config.Delta >= 1.0 then
            Error(MctsFault.InvalidDelta config.Delta)
        else
            Ok()

    type private EdgeStats =
        { Visits: int
          Sum: float
          SumSquares: float }

    type private TrailStep =
        { Key: string list
          Action: string
          Reward: float
          Depth: int }

    let private search (kernel: Kernel) (model: GenerativeModel) (config: SearchConfig) : SearchResult =
        let width = returnWidth model.RewardLo model.RewardHi model.Horizon model.Discount
        let empty = { Visits = 0; Sum = 0.0; SumSquares = 0.0 }
        let rootKey = [ model.Root ]

        let actionsOf state =
            model.Actions |> Map.tryFind state |> Option.defaultValue []

        let statsOf stats key action =
            stats |> Map.tryFind (key, action) |> Option.defaultValue empty

        let rec descend stats prng state key depth stepsRev =
            let actions = actionsOf state

            if depth >= model.Horizon || List.isEmpty actions then
                prng, List.rev stepsRev, state, depth
            else
                let chosen =
                    match actions |> List.filter (fun action -> (statsOf stats key action).Visits = 0) with
                    | first :: _ -> first
                    | [] ->
                        let parentVisits =
                            actions |> List.fold (fun sum action -> sum + (statsOf stats key action).Visits) 0

                        actions
                        |> List.sortBy (fun action ->
                            let current = statsOf stats key action
                            let mean = current.Sum / float current.Visits

                            let score =
                                uctValue config.Exploration width parentVisits current.Visits mean

                            -score, action)
                        |> List.head

                let u, prng2 = nextUniform prng
                let outcome = sample (Map.find (state, chosen) kernel) u

                let childKey =
                    if config.DagSafe then [ outcome.Next ] else key @ [ chosen; outcome.Next ]

                let step =
                    { Key = key
                      Action = chosen
                      Reward = outcome.Reward
                      Depth = depth }

                descend stats prng2 outcome.Next childKey (depth + 1) (step :: stepsRev)

        let rec rollout prng state depth rewardsRev =
            let actions = actionsOf state

            if depth >= model.Horizon || List.isEmpty actions then
                prng, List.rev rewardsRev
            else
                let uIndex, prng2 = nextUniform prng
                let action = actions |> List.item (int (uIndex * float actions.Length))
                let uNext, prng3 = nextUniform prng2
                let outcome = sample (Map.find (state, action) kernel) uNext
                rollout prng3 outcome.Next (depth + 1) (outcome.Reward :: rewardsRev)

        let backup stats steps tailReturn =
            steps
            |> List.rev
            |> List.fold
                (fun (carry, credited) step ->
                    let total = step.Reward + model.Discount * carry

                    let prior =
                        credited |> Map.tryFind (step.Key, step.Action) |> Option.defaultValue empty

                    let next =
                        { Visits = prior.Visits + 1
                          Sum = prior.Sum + total
                          SumSquares = prior.SumSquares + total * total }

                    total, Map.add (step.Key, step.Action) next credited)
                (tailReturn, stats)
            |> snd

        let rec iterate remaining stats prng =
            if remaining <= 0 then
                stats
            else
                let prng2, steps, leafState, leafDepth = descend stats prng model.Root rootKey 0 []
                let prng3, rolloutRewards = rollout prng2 leafState leafDepth []

                let tail =
                    rolloutRewards
                    |> List.foldBack (fun reward acc -> reward + model.Discount * acc) <| 0.0

                iterate (remaining - 1) (backup stats steps tail) prng3

        let finalStats = iterate config.Iterations Map.empty (seedFrom config.Seed)
        let rootActions = actionsOf model.Root |> List.sort

        let summarize action =
            let current = finalStats |> Map.tryFind (rootKey, action) |> Option.defaultValue empty
            let mean = if current.Visits = 0 then 0.0 else current.Sum / float current.Visits

            let variance =
                if current.Visits < 2 then
                    0.0
                else
                    max 0.0 ((current.SumSquares - current.Sum * current.Sum / float current.Visits)
                             / float (current.Visits - 1))

            let radius =
                if current.Visits = 0 then
                    Double.PositiveInfinity
                else
                    width * sqrt (log (2.0 / config.Delta) / (2.0 * float current.Visits))

            { Action = action
              Visits = current.Visits
              Mean = mean
              Variance = variance
              Radius = radius }

        let visitsOf action =
            finalStats |> Map.tryFind (rootKey, action) |> Option.map (fun s -> s.Visits) |> Option.defaultValue 0

        let best =
            rootActions
            |> List.filter (fun action -> visitsOf action > 0)
            |> List.sortBy (fun action ->
                let current = Map.find (rootKey, action) finalStats
                -visitsOf action, -(current.Sum / float current.Visits), action)
            |> List.tryHead

        { BestAction = best
          RootVisits = rootActions |> List.fold (fun sum action -> sum + visitsOf action) 0
          ActionStats = rootActions |> List.map summarize
          Coverage =
            { Iterations = config.Iterations
              Horizon = model.Horizon
              Discount = model.Discount
              RewardLo = model.RewardLo
              RewardHi = model.RewardHi
              ReturnWidth = width
              Delta = config.Delta
              DagSafe = config.DagSafe
              Scope = "fixed-time-per-action"
              Assumptions =
                [ "fixed-time-hoeffding-no-union-correction"
                  if config.DagSafe then
                      "dag-transposition-sharing-unweighted"
                  else
                      "dag-sharing-disabled" ]
                |> Set.ofList }
          Seed = config.Seed }

    let run (model: GenerativeModel) (config: SearchConfig) : Result<SearchResult, MctsFault> =
        match validateModel model with
        | Error fault -> Error fault
        | Ok kernel ->
            match validateConfig config with
            | Error fault -> Error fault
            | Ok () -> Ok(search kernel model config)
