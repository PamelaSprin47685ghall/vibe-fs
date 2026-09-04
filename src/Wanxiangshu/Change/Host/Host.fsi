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

    /// Same-road charge update: retain physical Session/worktree while advancing
    /// durable Relay AuthorityRevision for one exact caller tool invocation.
    member ContinueManagerJob:
        jobId: ManagerJobId *
        prompt: string *
        callerProviderRun: ProviderRunIdentity *
        callerToolCallId: ToolCallId *
        ?expectedToolCalls: int ->
            Task<Result<string, string>>

    /// EXEC-019: FIFO batch + local interrupt (JoinTool renders wire).
    member JoinPublishedAvailable:
        maxCount: int * interrupt: Task<JoinInterruptReason> ->
            Task<Result<JoinWaitOutcome<OrchestratorVerdict>, string>>

    member CancelAndDrain: unit -> Task
    member DetachAndDrain: unit -> Task
    member Cancel: unit -> unit
