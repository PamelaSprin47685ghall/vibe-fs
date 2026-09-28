namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation

/// DSL-003 / delegation-029 / durable-events-023: Durable composition bridge for
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

    /// crash-reconciliation-017/020 + managed-session-lifecycle-018: a child
    /// work run that ended without completing is reset here — the child's
    /// logical run is closed (fresh identity on the next handoff, transcript
    /// preserved) and the parent's handle is settled as a Cancelled completion
    /// so a pending join gets an explicit outcome. Restarting the work itself
    /// stays the manager's explicit decision.
    val settleUncompletedChildRun: projection: AgentProjectionSet -> fact: ChatExecutionFactCases -> AgentProjectionSet
