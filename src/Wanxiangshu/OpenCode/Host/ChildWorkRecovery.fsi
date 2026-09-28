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
        { ParentSessionId: SessionId
          Handle: HandleId
          ChildSessionId: SessionId }

    /// Child work runs still active at startup, resolved to their parent handle.
    val orphanedChildRuns: projections: AgentProjectionSet -> OrphanedChildRun list

    /// ChildRunVoided closes the child authority without creating a completion;
    /// the active handle remains reusable and owes no join delivery.
    val settlementFact: orphaned: OrphanedChildRun -> ExecutionFactCases

    /// Append one settlement per orphaned child run. Load Phase; no dispatch, no
    /// provider call, no command replay. A failed append aborts with JournalAppendException.
    val settleOrphanedChildRuns: journal: AgentJournal -> Task<unit>
