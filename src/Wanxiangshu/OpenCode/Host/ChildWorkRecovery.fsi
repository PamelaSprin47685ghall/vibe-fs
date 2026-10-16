namespace Wanxiangshu.OpenCode.Host

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// crash-reconciliation-017/018/020: after a restart no interrupted work is
/// replayed, so a child work run left active by the previous runtime can never
/// complete. This module settles those runs once, at load, so the road can hand
/// work to that child again while its transcript stays intact.
module ChildWorkRecovery =

    /// Reason recorded when a fresh process settles a Fission group the previous
    /// runtime left Open.
    [<Literal>]
    val OrphanedFissionReason: string = "process_interrupted"

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

    /// Open Fission groups left behind by a previous runtime.
    val orphanedFissionGroups: projections: AgentProjectionSet -> FissionGroupProjection list

    /// Exact FissionFailed fact closing an orphaned open Fission group at load.
    val fissionSettlementFact: group: FissionGroupProjection -> FissionFactCases

    /// Stop exact admitted PTY work before voiding it. Failed drain preserves
    /// the active work. No provider call or command replay.
    val settleOrphanedChildRuns: drainPtys: (AdmittedWork -> Task<unit>) -> journal: AgentJournal -> Task<unit>
