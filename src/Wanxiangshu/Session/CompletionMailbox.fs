namespace Wanxiangshu.Session

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Process

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
                    // trySet: a TimedOut waiter may already be completed.
                    let waiter = waiters.Dequeue()
                    AsyncSupport.trySetResult waiter (Ok completion) |> ignore
                else
                    completions.Enqueue completion)

    /// Await the next completion. Optional timeout unblocks Join forever-waiters.
    member _.Join(?timeoutMs: int) =
        let pending =
            lock gate (fun () ->
                if completions.Count > 0 then
                    Choice1Of2(Ok(completions.Dequeue()))
                elif cancelled then
                    Choice1Of2(Error ForkError.Cancelled)
                elif not (hasActive ()) then
                    Choice1Of2(Error ForkError.NothingToJoin)
                else
                    let waiter = TaskCompletionSource<Result<RunCompletion, ForkError>>()
                    waiters.Enqueue waiter
                    Choice2Of2 waiter)

        match pending with
        | Choice1Of2 result -> Task.FromResult result
        | Choice2Of2 waiter ->
            match timeoutMs with
            | None -> waiter.Task
            | Some ms when ms <= 0 ->
                AsyncSupport.trySetResult waiter (Error ForkError.TimedOut) |> ignore
                waiter.Task
            | Some ms ->
                task {
                    // raceExit: true = waiter completed first; false = timer elapsed.
                    let! completedFirst = PtyTiming.raceExit (waiter.Task :> Task) ms

                    if completedFirst then
                        return! waiter.Task
                    else
                        // Drop this waiter so a later Publish does not deliver into a dead TCS.
                        lock gate (fun () ->
                            let kept = Queue<TaskCompletionSource<Result<RunCompletion, ForkError>>>()

                            while waiters.Count > 0 do
                                let w = waiters.Dequeue()

                                if not (obj.ReferenceEquals(w, waiter)) then
                                    kept.Enqueue w

                            while kept.Count > 0 do
                                waiters.Enqueue(kept.Dequeue()))

                        AsyncSupport.trySetResult waiter (Error ForkError.TimedOut) |> ignore
                        return! waiter.Task
                }

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
                AsyncSupport.trySetResult waiter (Error ForkError.Cancelled) |> ignore

            true

    member _.PendingCount = lock gate (fun () -> completions.Count)
    member _.IsCancelled = lock gate (fun () -> cancelled)
