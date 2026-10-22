namespace Wanxiangshu.OpenCode.Host

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// crash-reconciliation-017/018/020: after a restart no interrupted work is
/// replayed; the child work the previous process left behind is settled by a
/// void (Abandoned(Voided)) that join reports once. Fission groups are closed.
module ChildWorkRecovery =

    /// A child work run the fresh process can no longer execute, and the parent
    /// handle that must carry its settlement.
    type OrphanedChildRun = ChildWorkSettlement.OrphanedChildRun

    /// Child work runs still active at startup, resolved to their parent handle.
    val orphanedChildRuns: projections: AgentProjectionSet -> OrphanedChildRun list

    /// The exact settlement fact for one orphaned child run: ChildWorkVoided for
    /// a scoped (admitted) child work, ChildRunVoided for an unscoped historical
    /// child run.
    val settlementFact: orphaned: OrphanedChildRun -> ExecutionFactCases

    /// Drain exact admitted PTY resources before voiding the orphaned work.
    val settleOrphanedChildRuns: drainPtys: (AdmittedWork -> Task<unit>) -> journal: AgentJournal -> Task<unit>
