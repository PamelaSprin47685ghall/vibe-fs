namespace Wanxiangshu.Sphinx.V2.Plugins

open System

/// Graph-MCTS behind a generator port.
///
/// WHAT[sphinx-v2-026]: the statistics this plugin produces are sample statistics in a
/// declared model with a declared horizon. They are not coverage guarantees. Under an
/// adaptive tree policy the ordinary i.i.d. radius has no finite-sample coverage, so it
/// is never reported as a fixed-time bound, and the search never claims a deterministic
/// singleton.
///
/// WHAT[sphinx-v2-021]: a rollout that needs a language model is a WorkItem. It never
/// hides inside a `for` loop making network calls, and its real cost is charged.
/// The transition oracle. A synchronous in-memory simulator is a legal implementation;
/// a rollout that calls a model returns WorkProposals instead of results.
type TransitionResult =
    { NextState: string
      Terminal: bool
      StepReturn: float
      Usage: (string * float) list option
      Provenance: string }

type ITransitionModel =
    abstract ModelRef: string
    /// The actions available at a state.
    abstract Actions: state: string -> string list
    /// The declared horizon in steps.
    abstract Horizon: int
    /// The reward range, used to scale the exploration term.
    abstract RewardRange: float * float
    /// Samples one transition. Must be a pure function of (state, action, rngState)
    /// and must return the advanced rng state.
    abstract Sample: state: string * action: string * rngState: string -> TransitionResult * string
    /// The terminal reward, if any.
    abstract TerminalReward: state: string -> float option

type NodeStats =
    {
        Visits: int
        ValueSum: float
        ValueSumSquares: float
        /// Model and horizon this node's statistics belong to.
        ModelRef: string
        Horizon: int
    }

/// The recommendation rule, stated explicitly so a reader can see what was maximized.
[<RequireQualifiedAccess>]
type Recommendation =
    | BestMean of action: string * mean: float
    | MostVisited of action: string * visits: int
    | InsufficientSamples

/// The scaling factor for the exploration term. A declared reward range of zero width
/// means the model has no usable spread, so the term falls back to unit scale rather
/// than dividing by zero.
module private RewardScale =

    let ofRange (rewardLow: float) (rewardHigh: float) : float =
        let span = rewardHigh - rewardLow

        match span > 0.0 with
        | true -> span
        | false -> 1.0

[<RequireQualifiedAccess>]
type NodeFault =
    | NoActions of state: string
    | UnknownAction of state: string * action: string
    | InvalidRngState
    | MissingValueBridge

module Mcts =

    /// The state key includes history, remaining horizon and the model revision.
    ///
    /// WHAT[sphinx-v2-013]: two semantically similar nodes are not the same decision
    /// state unless their remaining budget and horizon also match. Sharing statistics
    /// across them is how a search "learns" from a problem it was never in.
    let stateKey (modelRef: string) (history: string list) (horizon: int) : string =
        String.concat "|" [ modelRef; String.concat ">" history; string horizon ]

    /// Mean of a node's samples. No visits means no mean, never a zero.
    let mean (stats: NodeStats) : float option =
        match stats.Visits with
        | 0 -> None
        | visits -> Some(stats.ValueSum / float visits)

    /// Sample variance. One visit has no variance, which is reported as None rather
    /// than as zero uncertainty.
    let variance (stats: NodeStats) : float option =
        match stats.Visits with
        | 0
        | 1 -> None
        | visits ->
            let average = stats.ValueSum / float visits
            let spread = stats.ValueSumSquares / float visits - average * average
            Some(max 0.0 spread)

    /// The UCT value, scaled by the declared reward range. An unvisited node returns
    /// positive infinity so it is handled before any mean-vs-mean comparison.
    let uct (parentVisits: int) (exploration: float) (rewardLow: float) (rewardHigh: float) (node: NodeStats) : float =
        match node.Visits with
        | 0 -> Double.PositiveInfinity
        | visits ->
            let average = node.ValueSum / float visits
            let explore = exploration * sqrt (log (float parentVisits) / float visits)
            average + explore * RewardScale.ofRange rewardLow rewardHigh

    let recommend (stats: Map<string, NodeStats>) : Recommendation =
        let visited = stats |> Map.toList |> List.filter (fun (_, node) -> node.Visits > 0)

        match visited |> List.isEmpty with
        | true -> Recommendation.InsufficientSamples
        | false ->
            let best =
                visited |> List.maxBy (fun (_, node) -> node.ValueSum / float node.Visits)

            Recommendation.BestMean(fst best, snd best |> mean |> Option.defaultValue 0.0)

    /// Whether every child has been visited at least once.
    let allChildrenSeen (stats: Map<string, NodeStats>) (actions: string list) : bool =
        actions
        |> List.forall (fun action ->
            match stats |> Map.tryFind action with
            | Some node -> node.Visits > 0
            | None -> false)
