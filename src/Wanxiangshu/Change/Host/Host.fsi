namespace Wanxiangshu.Change.Host

open System.Threading.Tasks
open Wanxiangshu.Change
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation.Identity

/// Host wiring for one Change Road. One physical Manager session can host many
/// logical Relay incumbencies; no Reviewer session participates in publication.
type OrchestratorHost =
    new: deps: OrchestratorHostDeps * orchestratorId: SessionId -> OrchestratorHost

    member ForkManagerJob:
        jobId: ManagerJobId * managerAgent: string * prompt: string * ?byname: string * ?expectedToolCalls: int ->
            Task<Result<string, string>>

    /// GLORY-068: `commission(existing_job_id, charge)` — continue the SAME
    /// Manager job (same worktree, same session) with an appended requirement.
    member ContinueManagerJob:
        jobId: ManagerJobId * prompt: string * ?expectedToolCalls: int -> Task<Result<string, string>>

    /// EXEC-019: FIFO batch + local interrupt (JoinTool renders wire).
    member JoinPublishedAvailable:
        maxCount: int * interrupt: Task<JoinInterruptReason> ->
            Task<Result<JoinWaitOutcome<OrchestratorVerdict>, string>>

    member CancelAndDrain: unit -> Task
    member DetachAndDrain: unit -> Task
    member Cancel: unit -> unit
