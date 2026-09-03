namespace Wanxiangshu.Sphinx.Runtime

type RefinementTarget =
    { Id: string
      Dependencies: Set<string>
      ConflictKeys: Set<string>
      Cost: Map<string, float>
      LossCurrency: string option
      LossValue: float option
      CommonCurrency: string option
      EffectSlot: string option }

type ScheduleRequest =
    { Targets: RefinementTarget list
      Budget: Map<string, float>
      Completed: Set<string> }

type ScheduleResult =
    { Batch: string list
      Pareto: string list
      Order: string list }

type ScheduleError =
    { Code: string
      Message: string }

type ClosureDomain =
    | FiniteDag of nodes: int * edges: (int * int) list
    | FiniteChain of monotone: bool * continuous: bool
    | MetricSpace of modulus: float
    | NoDomain

type ClosureOperator =
    | DagRecurrence of order: int list * seeds: Map<int, float> * rule: string
    | FiniteMap of start: int * table: int list
    | AffineMap of factor: float * offset: float * start: float
    | NoOperator

type AsyncExpectation =
    { FiniteDecisionSet: bool
      StrictGap: bool
      VanishingUncertainty: bool
      FairScheduling: bool
      OrderAware: bool
      CorrectSpecification: bool option }

type ClosureRequest =
    { Domain: ClosureDomain option
      Operator: ClosureOperator option
      MaxIterations: int
      Async: AsyncExpectation option }

type FixedPoint =
    | DagPoint of Map<int, float>
    | ScalarPoint of float
    | NoPoint

type ClosureOutcome =
    { Converged: bool
      Point: FixedPoint
      Iterations: int
      ResidualBound: float
      Unique: bool }

module Agenda =
    val frontier: targets: RefinementTarget list -> string list
    val schedule: request: ScheduleRequest -> Result<ScheduleResult, ScheduleError>
    val evaluateClosure: request: ClosureRequest -> ClosureOutcome
