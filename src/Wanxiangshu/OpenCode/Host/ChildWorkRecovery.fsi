namespace Wanxiangshu.OpenCode.Host

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// crash-reconciliation-017/018/020: after a restart no interrupted work is
/// replayed, so a child work run left active by the previous runtime can never
/// complete. This module settles those runs once, at load, so the road can hand
/// work to that child again while its transcript stays intact.
module ChildWorkRecovery =

    /// A child work run the fresh process can no longer execute, and the parent
    /// handle that must carry its settlement.
    type OrphanedChildRun =
        private
        | Admitted of parentSessionId: SessionId * work: AdmittedWork
        | Historical of parentSessionId: SessionId * childSessionId: SessionId

    /// Child work runs still active at startup, resolved to their parent handle.
    val orphanedChildRuns: projections: AgentProjectionSet -> OrphanedChildRun list

    /// Exact ChildWorkVoided closes admitted work; ChildRunVoided only normalizes
    /// history without scoped work. Neither creates a completion or join delivery.
    val settlementFact: orphaned: OrphanedChildRun -> ExecutionFactCases

    /// Append one settlement per orphaned child run. Load Phase; no dispatch, no
    /// provider call, no command replay.
    val settleOrphanedChildRuns: journal: AgentJournal -> Task<unit>
