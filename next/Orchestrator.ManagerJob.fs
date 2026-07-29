namespace Wanxiangshu.Next.Orchestrator

open System.Collections.Generic
open System.Threading.Tasks

/// One Manager execution plus the worktree resource it owns through publish.
type ManagerJob private
    (managerId: string, prompt: string, worktree: WorktreeResource, completion: Task<Result<unit, string>>) =

    member _.ManagerId = managerId
    member _.Prompt = prompt
    member _.Worktree = worktree
    member _.Completion = completion

    member _.Handle =
        { ManagerId = managerId
          WorktreePath = worktree.Path }

    static member Start(manager: ManagerPort, managerId: string, prompt: string, worktree: WorktreeResource) =
        ManagerJob(managerId, prompt, worktree, manager.RunManager managerId worktree.Path prompt)

    static member Recover
        (manager: ManagerPort, managerId: string, prompt: string, worktree: WorktreeResource, completed: bool)
        =
        let completion =
            if completed then Task.FromResult(Ok())
            else manager.RunManager managerId worktree.Path prompt

        ManagerJob(managerId, prompt, worktree, completion)

/// Completion mailbox for published verdicts. It only stores the final
/// OrchestratorVerdict after FF; the publish program runs as an owned task.
type VerdictMailbox() =
    let gate = obj ()
    let verdicts = Queue<OrchestratorVerdict>()
    let waiters = Queue<TaskCompletionSource<OrchestratorVerdict>>()
    let mutable active = 0

    member _.StartJob() = lock gate (fun () -> active <- active + 1)

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
