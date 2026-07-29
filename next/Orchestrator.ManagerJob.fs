namespace Wanxiangshu.Next.Orchestrator

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity

/// One Manager execution plus the worktree resource it owns through publish.
///
/// Exactly the fields ORCH-006 persists, so a recovered job and a fresh one are the
/// same value. There is deliberately no `Prompt`: `ManagerJobCreated` does not record
/// one, so nothing after a restart may depend on it. The initial prompt goes straight
/// to `StartManager`, and conflict resumption sends only the conflict instruction to a
/// session that already holds the task (PROMPT-003).
///
/// No completion Task either. ORCH-006 requires `ManagerJobCreated` to carry the
/// Manager's `SessionId`, which exists only after the fork, so the previous shape —
/// start the Manager in the constructor, then persist — could only write the job fact
/// after the Manager had already begun. A crash in that window left a live Manager with
/// no durable job. Starting and awaiting are now two steps the program sequences, with
/// the fact written between them.
type ManagerJob =
    { JobId: ManagerJobId
      ManagerSessionId: SessionId
      ManagerAgent: string
      TargetRef: TargetRef
      Worktree: WorktreeResource }

    member this.Handle =
        { JobId = this.JobId
          WorktreePath = this.Worktree.Path }

/// Completion mailbox for published verdicts. It only stores the final
/// OrchestratorVerdict after FF; the publish program runs as an owned task.
type VerdictMailbox() =
    let gate = obj ()
    let verdicts = Queue<OrchestratorVerdict>()
    let waiters = Queue<TaskCompletionSource<OrchestratorVerdict>>()
    let mutable active = 0

    member _.StartJob() =
        lock gate (fun () -> active <- active + 1)

    member _.Publish(verdict: OrchestratorVerdict) =
        lock gate (fun () ->
            active <- max 0 (active - 1)

            if waiters.Count > 0 then
                waiters.Dequeue().SetResult verdict
            else
                verdicts.Enqueue verdict)

    member _.TryJoin() =
        lock gate (fun () ->
            if verdicts.Count > 0 then
                Task.FromResult(Some(verdicts.Dequeue()))
            elif active = 0 then
                Task.FromResult None
            else
                let waiter = TaskCompletionSource<OrchestratorVerdict>()
                waiters.Enqueue waiter

                task {
                    let! verdict = waiter.Task
                    return Some verdict
                })
