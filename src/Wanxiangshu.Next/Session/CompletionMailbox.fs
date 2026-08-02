namespace Wanxiangshu.Next.Session

open System.Collections.Generic
open System.Threading.Tasks

/// Single physical completion mailbox shared by agent and PTY runs. It owns
/// queueing, pending join waiters, and terminal cancellation exactly once.
type CompletionMailbox(gate: obj, hasActive: unit -> bool) =
    let completions = Queue<RunCompletion>()
    let waiters = Queue<TaskCompletionSource<Result<RunCompletion, ForkError>>>()
    let mutable cancelled = false

    member _.Publish(completion: RunCompletion) =
        lock gate (fun () ->
            if not cancelled then
                if waiters.Count > 0 then
                    waiters.Dequeue().SetResult(Ok completion)
                else
                    completions.Enqueue completion)

    member _.Join() =
        lock gate (fun () ->
            if completions.Count > 0 then
                Task.FromResult(Ok(completions.Dequeue()))
            elif cancelled then
                Task.FromResult(Error ForkError.Cancelled)
            elif not (hasActive ()) then
                Task.FromResult(Error ForkError.NothingToJoin)
            else
                let waiter = TaskCompletionSource<Result<RunCompletion, ForkError>>()
                waiters.Enqueue waiter
                waiter.Task)

    /// Returns true only to the caller that performed the first cancellation.
    member _.Cancel() =
        let drained =
            lock gate (fun () ->
                if cancelled then
                    None
                else
                    cancelled <- true

                    Some
                        [ while waiters.Count > 0 do
                              yield waiters.Dequeue() ])

        match drained with
        | None -> false
        | Some pending ->
            for waiter in pending do
                waiter.SetResult(Error ForkError.Cancelled)

            true

    member _.PendingCount = lock gate (fun () -> completions.Count)
    member _.IsCancelled = lock gate (fun () -> cancelled)
