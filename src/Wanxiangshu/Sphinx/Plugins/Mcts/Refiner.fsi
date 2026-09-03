namespace Wanxiangshu.Sphinx.Plugins.Mcts

open Wanxiangshu.Sphinx.Core

/// WHAT[EPI-010]: seeded fixed-budget search over a finite generative model with sample statistics.
module Refiner =
    /// One stochastic branch of a state-action kernel entry.
    type Outcome =
        { Next: string
          Probability: float
          Reward: float }

    /// Finite distribution over next states and rewards for one state-action pair.
    type KernelEntry =
        { State: string
          Action: string
          Outcomes: Outcome list }

    /// Finite generative model with bounded rewards and a finite horizon.
    type GenerativeModel =
        { Root: string
          Actions: Map<string, string list>
          Transitions: KernelEntry list
          Horizon: int
          Discount: float
          RewardLo: float
          RewardHi: float }

    /// Fixed simulation budget with exploration, coverage level, seed, and sharing assumption.
    type SearchConfig =
        { Iterations: int
          Exploration: float
          Delta: float
          Seed: int
          DagSafe: bool }

    /// Sample mean, unbiased sample variance, and coverage radius for one root action.
    type ActionStats =
        { Action: string
          Visits: int
          Mean: float
          Variance: float
          Radius: float }

    /// Fixed-time coverage metadata: budget, return scale, and declared assumptions.
    /// Scope is always fixed-time-per-action (M-2); never consume as simultaneous/anytime-valid.
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

    /// Fixed-budget search result: recommendation with statistics, never a truth claim.
    type SearchResult =
        { BestAction: string option
          RootVisits: int
          ActionStats: ActionStats list
          Coverage: Coverage
          Seed: int }

    /// Typed reason the model or budget violates the search contract.
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

    val code: fault: MctsFault -> string
    val message: fault: MctsFault -> string
    val toCoreError: fault: MctsFault -> CoreError
    val returnWidth: rewardLo: float -> rewardHi: float -> horizon: int -> discount: float -> float
    val uctValue: exploration: float -> returnWidth: float -> parentVisits: int -> visits: int -> mean: float -> float
    val run: model: GenerativeModel -> config: SearchConfig -> Result<SearchResult, MctsFault>
