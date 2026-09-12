namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation

/// DSL-003 / DELEG-029 / DURABLE-EVENTS-023: Durable composition bridge for
/// delegation-owned folds.
///
/// Pure fold decisions and state transitions are owned by the delegation domain
/// (`ExecutionFactFold` and `DelegationFactFold`). Durable composition is the sole
/// assembly point that routes `AgentProjectionSet` into bounded slices and applies
/// fold changes back to aggregate projection state.
module DelegationProjectionBridge =

    val foldExecution:
        projection: AgentProjectionSet -> fact: ExecutionFactCases -> Result<AgentProjectionSet, FoldRejection>

    val foldDelegation:
        projection: AgentProjectionSet -> fact: DelegationFactCases -> Result<AgentProjectionSet, FoldRejection>
