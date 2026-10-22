namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// crash-reconciliation-017/018/020: a fresh process cannot execute the child
/// work the previous process left behind, so that obligation is settled by a
/// void (Abandoned(Voided)) that join reports once.
module ChildWorkSettlement =

    /// A child work run the fresh process can no longer execute, and the parent
    /// handle that must carry its settlement.
    type OrphanedChildRun =
        | Admitted of parentSessionId: SessionId * work: AdmittedWork
        | Historical of parentSessionId: SessionId * childSessionId: SessionId

    /// Child work runs still active at startup, resolved to their parent handle.
    val orphanedChildRuns: projections: AgentProjectionSet -> OrphanedChildRun list

    /// The exact settlement fact for one orphaned child run: ChildWorkVoided for
    /// a scoped (admitted) child work, ChildRunVoided for an unscoped historical
    /// child run.
    val settlementFact: orphaned: OrphanedChildRun -> ExecutionFactCases

    /// Append one settlement per orphaned child run. Load Phase; no dispatch, no
    /// provider call, no command replay.
    val settleOrphanedChildRuns: journal: AgentJournal -> Task<unit>
