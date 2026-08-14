namespace Wanxiangshu.Foundation
open Wanxiangshu.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Sphinx

open System
open System.Threading
open System.Threading.Tasks

/// ARCH-009: bounded fan-out only. No Flow monad.
module Defer =
    open Fable.Core

    [<Emit("Promise.resolve().then($0)")>]
    let defer<'T> (work: unit -> Task<'T>) : Task<'T> = jsNative

type private JsTcs<'T>() =
    // DSL-MUTABLE: resource — one-shot promise completion flag
    let mutable completed = false
    // DSL-MUTABLE: resource — pending promise resolver, written once at construct
    let mutable resolveFn: ('T -> unit) option = None

    let p =
        Fable.Core.JS.Constructors.Promise.Create(fun res _ -> resolveFn <- Some res)

    member _.Task: Task<'T> = unbox p
    member _.IsCompleted = completed

    member _.SetResult(res: 'T) =
        completed <- true

        match resolveFn with
        | Some f -> f res
        | None -> ()

    member _.TrySetResult(res: 'T) =
        if completed then
            false
        else
            completed <- true

            match resolveFn with
            | Some f ->
                f res
                true
            | None -> false

type private AsyncSemaphore(maxCount: int) =
    // DSL-MUTABLE: resource — remaining permit count of the semaphore
    let mutable count = maxCount
    let waiters = System.Collections.Generic.Queue<JsTcs<unit>>()
    let lockObj = obj ()

    member _.WaitAsync(ct: CancellationToken) =
        task {
            ct.ThrowIfCancellationRequested()

            let tcsOpt =
                lock lockObj (fun () ->
                    if count > 0 then
                        count <- count - 1
                        None
                    else
                        let tcs = JsTcs<unit>()
                        waiters.Enqueue(tcs)
                        Some tcs)

            match tcsOpt with
            | Some tcs -> do! tcs.Task
            | None -> ()
        }

    member _.Release() =
        lock lockObj (fun () ->
            if waiters.Count > 0 then
                let tcs = waiters.Dequeue()
                tcs.TrySetResult() |> ignore
            else
                count <- count + 1)

    interface IDisposable with
        member _.Dispose() = ()

module Parallel =
    open Fable.Core

    [<Emit("Promise.all($0)")>]
    let private promiseAll (promises: obj array) : Task<obj array> = jsNative

    let mapBounded
        (maxConcurrency: int)
        (cancellation: CancellationToken)
        (action: 't -> CancellationToken -> Task<'u>)
        (items: 't seq)
        : Task<'u list> =
        task {
            if maxConcurrency <= 0 then
                invalidArg (nameof maxConcurrency) "maxConcurrency must be greater than 0"

            let indexedItems = items |> Seq.toArray

            if indexedItems.Length = 0 then
                return []
            else
                use semaphore = new AsyncSemaphore(maxConcurrency)

                let workTasks =
                    indexedItems
                    |> Array.map (fun item ->
                        task {
                            do! semaphore.WaitAsync(cancellation)

                            try
                                return! action item cancellation
                            finally
                                semaphore.Release()
                        })

                let promises = workTasks |> Array.map box
                let! resultsObj = promiseAll promises
                let results = unbox<'u array> resultsObj
                return results |> Array.toList
        }
