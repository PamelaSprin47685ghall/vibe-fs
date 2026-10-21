namespace Wanxiangshu.OpenCode.Host

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// crash-reconciliation-017/018/020: after a restart no interrupted work is
/// replayed, and active child runs maintain visible obligations rather than being
/// voided. Diagnostics emit their presence while Fission groups are closed.
module ChildWorkRecovery =

    /// A child work run the fresh process can no longer execute, and the parent
    /// handle that must carry its settlement.
    type OrphanedChildRun =
        private
        | Admitted of parentSessionId: SessionId * work: AdmittedWork
        | Historical of parentSessionId: SessionId * childSessionId: SessionId

    /// Child work runs still active at startup, resolved to their parent handle.
    val orphanedChildRuns: projections: AgentProjectionSet -> OrphanedChildRun list

    /// Orphaned child runs are not voided; settlementFact returns None to avoid
    /// appending ChildWorkVoided/ChildRunVoided facts.
    val settlementFact: orphaned: OrphanedChildRun -> ExecutionFactCases option

    /// Append one settlement per orphaned child run. Load Phase; no dispatch, no
    /// provider call, no command replay.
    val settleOrphanedChildRuns: journal: AgentJournal -> Task<unit>
