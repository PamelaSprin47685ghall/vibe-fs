namespace Wanxiangshu.Change.Host

open System.Threading.Tasks
open Wanxiangshu.Change

/// Remove worktrees and branches no active ManagerJob owns.
///
/// Cleanup only. It never derives a recovery action from what it finds on disk —
/// ORCH-007 forbids substituting filesystem state for a durable fact, and the
/// active-job set here comes from the projection.
module OrchestratorSweep =
    val sweepStaleArtifacts: git: GitPort -> activeJobs: ManagerJobProjection list -> Task<Result<unit, string>>

    val sweepLocked:
        lockPath: string ->
        git: GitPort ->
        activeJobs: (unit -> ManagerJobProjection list) ->
            Task<Result<unit, string>>
