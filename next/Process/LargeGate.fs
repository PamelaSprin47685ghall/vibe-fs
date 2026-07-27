namespace Wanxiangshu.Next.Process

open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

/// Single-holder large-process gate with cancelable FIFO waiters (Fable-safe).
module LargeGate =

    type private Waiter(ct: CancellationToken, onCancel: unit -> unit) =
        let completion = TaskCompletionSource<unit>()
        let mutable cancelled = false
        let mutable granted = false

        do
            // Poll cancellation without CancellationTokenRegistration.Dispose
            // (unsupported under Fable). Cheap while parked on the large gate.
            let rec poll () =
                emitJsExpr
                    (fun () ->
                        if granted || cancelled then
                            ()
                        elif ct.IsCancellationRequested then
                            cancelled <- true
                            completion.TrySetCanceled() |> ignore
                            onCancel ()
                        else
                            emitJsExpr (poll, 15) "setTimeout($0, $1)" |> ignore)
                    "$0()"
                |> ignore

            if ct.IsCancellationRequested then
                cancelled <- true
                completion.TrySetCanceled() |> ignore
            else
                poll ()

        member _.Completion = completion.Task
        member _.IsCancelled = cancelled

        member _.TryGrant() =
            if cancelled || granted then
                false
            else
                granted <- true
                completion.TrySetResult(()) |> ignore
                true

    let private waiters = Queue<Waiter>()
    let mutable private held = false
    let private gate = obj ()

    let getCount () : int =
        lock gate (fun () -> if held then 0 else 1)

    let private pumpUnlocked () =
        while waiters.Count > 0 && not held do
            let waiter = waiters.Dequeue()

            if waiter.TryGrant() then
                held <- true

    let acquire (ct: CancellationToken) : Task =
        if ct.IsCancellationRequested then
            let done' = TaskCompletionSource<unit>()
            done'.SetCanceled()
            done'.Task
        else
            lock gate (fun () ->
                if not held && waiters.Count = 0 then
                    held <- true
                    Task.FromResult(()) :> Task
                else
                    let waiter =
                        Waiter(
                            ct,
                            fun () ->
                                lock gate (fun () ->
                                    if not held then
                                        pumpUnlocked ())
                        )

                    waiters.Enqueue waiter
                    waiter.Completion)

    let release () : unit =
        lock gate (fun () ->
            held <- false
            pumpUnlocked ())
