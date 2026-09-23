namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

type AgendaError = { Code: string; Message: string }

type AgendaExclusion =
    | MissingDependency of dependency: WorkId
    | ConflictKeyClash of key: string
    | ResourceExhausted of resource: string
    | CapacityUnavailable
    | AlreadyTerminal

type DispatchDecision =
    { Dispatchable: WorkSpec list
      Excluded: (WorkSpec * AgendaExclusion) list }

module Agenda =
    /// Feasibility only: dependencies actually satisfied, conflict keys disjoint,
    /// resources within the authorized limit, capacity respected. No value ranking.
    val planDispatch: InquiryState -> WorkSpec list -> int -> DispatchDecision
