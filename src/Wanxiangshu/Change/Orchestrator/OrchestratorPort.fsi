namespace Wanxiangshu.Change

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Relay

/// Domain-owned sweep reads over persisted ManagerJobs.
///
/// Each member performs exactly one journal snapshot read per call.
type OrchestratorSweepPort =
    { ActiveJobs: unit -> ManagerJobProjection list
      TryJob: ManagerJobId -> ManagerJobProjection option
      Snapshot: unit -> ProjectionSet }

/// Domain-owned Relay reads, appends, and revision wait for one ManagerJob.
///
/// RoadSnapshot performs exactly one snapshot-with-revision read per call, so
/// the returned road view and revision always belong to the same revision.
type OrchestratorRelayPort =
    { RoadSnapshot: ManagerJobProjection -> RoadView option * JournalRevision
      AwaitChangeFrom: JournalRevision -> Task<unit>
      AppendRelay: ManagerJobProjection -> RelayTransaction -> Task<Result<unit, string>> }
