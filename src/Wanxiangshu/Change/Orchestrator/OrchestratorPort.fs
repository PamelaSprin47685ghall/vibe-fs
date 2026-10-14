namespace Wanxiangshu.Change

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation.Identity

type OrchestratorRecoveryView =
    { ActiveJobs: ManagerJobProjection list
      Handles: AgentLinkageProjection option }

/// Domain-owned sweep reads over persisted ManagerJobs.
///
/// Each member performs exactly one journal snapshot read per call;
/// `RecoveryView` takes one snapshot and cuts both recovery slices (active jobs
/// and the orchestrator's handles) from that same revision.
type OrchestratorSweepPort =
    { ActiveJobs: unit -> ManagerJobProjection list
      TryJob: ManagerJobId -> ManagerJobProjection option
      RecoveryView: SessionId -> OrchestratorRecoveryView }

type OrchestratorRelayPort =
    { AwaitChangeFrom: JournalRevision -> Task<unit> }
