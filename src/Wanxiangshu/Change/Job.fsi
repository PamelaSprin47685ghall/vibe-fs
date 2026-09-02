namespace Wanxiangshu.Change

open System.Threading.Tasks
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Git

/// One Manager execution plus the worktree resource it owns through publish.
type ManagerJob =
    { JobId: ManagerJobId
      ManagerSessionId: SessionId
      ManagerAgent: string
      TargetRef: TargetRef
      Worktree: WorktreeResource }

    member Handle: OrchestratorHandle

/// Completion mailbox for published verdicts. It only stores the final
/// OrchestratorVerdict after FF; the publish program runs as an owned task.
/// EXEC-019: FIFO batch drain, MaxJoinBatch ceiling.
type VerdictMailbox =
    new: unit -> VerdictMailbox
    member StartJob: unit -> unit
    member Publish: verdict: OrchestratorVerdict -> unit
    member DrainAvailable: maxCount: int -> OrchestratorVerdict list
    member HasActive: bool
    member PendingCount: int
    member TryJoinBatch: maxCount: int -> Task<OrchestratorVerdict list>

    member JoinAvailable:
        maxCount: int * interrupt: Task<JoinInterruptReason> -> Task<JoinWaitOutcome<OrchestratorVerdict>>
