namespace Wanxiangshu.OpenCode.Host

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation.Identity

/// crash-reconciliation-018/020: re-enlist the surviving durable children a
/// restarted process must be able to address again. Bookkeeping only — no prompt
/// is sent, nothing is replayed, and reuse stays the manager's explicit action.
module DurableChildAdoption =

    /// Each addressable child with the parent session that owns its handle.
    val adoptableChildren: projections: AgentProjectionSet -> (SessionId * HandleRecord) list
