namespace Wanxiangshu.Change

open System.Threading.Tasks
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation.Identity

type Orchestrator =
    new:
        git: GitPort *
        manager: ManagerPort *
        repoPath: string *
        targetRef: TargetRef *
        ?journal: OrchestratorJournalPort *
        ?lockRepoPath: string ->
            Orchestrator

    member ForkManager:
        jobId: ManagerJobId *
        managerAgent: string *
        prompt: string *
        ?worktreePath: WorktreePath *
        ?byname: string *
        ?expectedToolCalls: int ->
            Task<Result<OrchestratorHandle, OrchestratorVerdict>>

    member RecoverManagerJob: record: ManagerJobProjection -> unit

    member ContinueManager: jobId: ManagerJobId * prompt: string -> Task<Result<WorktreePath, string>>

    member JoinPublishedBatch:
        maxCount: int * interrupt: Task<JoinInterruptReason> -> Task<JoinWaitOutcome<OrchestratorVerdict>>
