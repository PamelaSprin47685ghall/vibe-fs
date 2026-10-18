namespace Wanxiangshu.Change.Host

open System.Threading.Tasks
open Wanxiangshu.Change
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Work

/// The durable loop signal one Manager work holds for Change (see Host.fs). Module
/// level so the read-only harness probe observes exactly the program's decision.
[<RequireQualifiedAccess>]
module DurableLoopSignal =
    val ofWork: work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork -> ManagerLoopSignal option

/// The same Work's next tenure after a delivered manager life
/// (change-integration-005/013). Module level so the read-only harness probe
/// exercises exactly the hand-off the Host applies when it continues a delivered
/// loop, with the cursor the caller observed.
[<RequireQualifiedAccess>]
module DeliveredTenureHandOff =
    val continuation:
        gitCommonDir: string ->
        workId: string ->
        work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork ->
        retirement: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.HostRetirement ->
        observedCursor: int64 option ->
        requestId: string ->
            Result<IncumbencyId, string>

/// The durable review evidence the publish gate reads for one Manager road
/// (change-integration-005/013). Module level so the read-only harness probes the
/// exact rule the gate applies after a delivered hand-off.
[<RequireQualifiedAccess>]
module PublishReviewEvidence =
    val ofWork: work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork -> (string * string) option

/// Host wiring for one Change Work. One physical Manager session can host many
/// logical Relay incumbencies; no second audit session participates in publication.
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

    /// A child exists when this process drives it or when its durable handle says
    /// so; the process tables alone would answer "no" after every restart.
    member HasChild: agentId: string -> bool
