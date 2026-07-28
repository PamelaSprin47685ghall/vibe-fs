namespace Wanxiangshu.Next.Process

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.AsyncSupport

/// Single-holder large-process gate with cancelable FIFO waiters.
module LargeGate =

    type private Waiter(ct: CancellationToken) =
        let tcs = TaskCompletionSource<unit>()
        let mutable canceled = ct.IsCancellationRequested

        do
            if canceled then
                trySetCanceled tcs |> ignore
            else
                ct.Register(fun () ->
                    canceled <- true
                    trySetCanceled tcs |> ignore)
                |> ignore

        member _.Task = tcs.Task
        member _.IsCanceled = canceled

        member _.TryGrant() =
            if canceled then
                false
            else
                trySetResult tcs () |> ignore
                true

    let private waiters = Queue<Waiter>()
    let mutable private held = false
    let private gate = obj ()

    let getCount () =
        lock gate (fun () -> if held then 0 else 1)

    let private pumpUnlocked () =
        while waiters.Count > 0 && not held do
            let waiter = waiters.Dequeue()

            if waiter.TryGrant() then
                held <- true

    let acquire (ct: CancellationToken) : Task =
        if ct.IsCancellationRequested then
            let tcs = TaskCompletionSource<unit>()
            trySetCanceled tcs |> ignore
            tcs.Task
        else
            lock gate (fun () ->
                if not held && waiters.Count = 0 then
                    held <- true
                    Task.FromResult() :> Task
                else
                    let waiter = Waiter ct
                    waiters.Enqueue waiter
                    pumpUnlocked ()
                    waiter.Task)

    let release () : unit =
        lock gate (fun () ->
            held <- false
            pumpUnlocked ())
