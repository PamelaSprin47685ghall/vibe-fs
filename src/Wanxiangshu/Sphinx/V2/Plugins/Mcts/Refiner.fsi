namespace Wanxiangshu.Sphinx.V2.Plugins

open System

type TransitionResult =
    { NextState: string
      Terminal: bool
      StepReturn: float
      Usage: (string * float) list option
      Provenance: string }

type ITransitionModel =
    abstract ModelRef: string
    abstract Actions: state: string -> string list
    abstract Horizon: int
    abstract RewardRange: float * float
    /// Samples one transition; must be pure in (state, action, rngState) and return
    /// the advanced rng state.
    abstract Sample: state: string * action: string * rngState: string -> TransitionResult * string
    abstract TerminalReward: state: string -> float option

type NodeStats =
    { Visits: int
      ValueSum: float
      ValueSumSquares: float
      /// Model and horizon this node's statistics belong to.
      ModelRef: string
      Horizon: int }

[<RequireQualifiedAccess>]
type NodeFault =
    | NoActions of state: string
    | UnknownAction of state: string * action: string
    | InvalidRngState
    | MissingValueBridge

/// The recommendation rule, stated explicitly so a reader can see what was maximized.
[<RequireQualifiedAccess>]
type Recommendation =
    | BestMean of action: string * mean: float
    | MostVisited of action: string * visits: int
    | InsufficientSamples

module Mcts =
    /// State key includes model, history and remaining horizon (WHAT[sphinx-v2-013]).
    val stateKey: string -> string list -> int -> string

    /// No visits means no mean, never a zero.
    val mean: NodeStats -> float option
    val variance: NodeStats -> float option

    /// UCT scaled by the declared reward range; unvisited nodes are handled first.
    val uct: int -> float -> float -> float -> NodeStats -> float

    val recommend: Map<string, NodeStats> -> Recommendation
    val allChildrenSeen: Map<string, NodeStats> -> string list -> bool
