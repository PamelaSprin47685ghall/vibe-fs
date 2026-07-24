namespace Wanxiangshu.Next.Kernel

open System
open System.Threading
open System.Threading.Tasks

module FlowHelpers =
    open Fable.Core

    [<Emit("($0 && typeof $0.then === 'function') ? $0 : Promise.resolve()")>]
    let awaitValueTask (vt: obj) : Task = jsNative

    [<Emit("Promise.resolve().then($0)")>]
    let defer<'T> (work: unit -> Task<'T>) : Task<'T> = jsNative

    [<Emit("Promise.reject($0)")>]
    let reject<'T> (error: exn) : Task<'T> = jsNative

type Flow<'ctx, 'error, 'a> = private Flow of ('ctx -> CancellationToken -> Task<Result<'a, 'error>>)

type ProgressGuard<'ctx, 'error> =
    { Stamp: 'ctx -> int64
      NoProgress: string -> 'error }

type FlowBuilder<'ctx, 'error>(progress: ProgressGuard<'ctx, 'error> option) =

    member _.Return(value: 'a) : Flow<'ctx, 'error, 'a> =
        Flow(fun _ _ -> Task.FromResult(Ok value))

    member _.ReturnFrom(flow: Flow<'ctx, 'error, 'a>) : Flow<'ctx, 'error, 'a> = flow

    member _.Bind(Flow action: Flow<'ctx, 'error, 'a>, next: 'a -> Flow<'ctx, 'error, 'b>) : Flow<'ctx, 'error, 'b> =
        Flow(fun ctx ct ->
            task {
                let! result = action ctx ct

                match result with
                | Error e -> return Error e
                | Ok value ->
                    let (Flow cont) = next value
                    return! cont ctx ct
            })

    member _.Zero() : Flow<'ctx, 'error, unit> = Flow(fun _ _ -> Task.FromResult(Ok()))

    member _.Delay(create: unit -> Flow<'ctx, 'error, 'a>) : Flow<'ctx, 'error, 'a> =
        Flow(fun ctx ct ->
            FlowHelpers.defer (fun () ->
                let (Flow f) = create ()
                f ctx ct))

    member this.Combine(first: Flow<'ctx, 'error, unit>, second: Flow<'ctx, 'error, 'a>) : Flow<'ctx, 'error, 'a> =
        this.Bind(first, (fun () -> second))

    member _.TryFinally(Flow body: Flow<'ctx, 'error, 'a>, compensation: unit -> unit) : Flow<'ctx, 'error, 'a> =
        Flow(fun ctx ct ->
            task {
                let mutable bodyResult: Result<'a, 'error> option = None
                let mutable bodyEx: exn option = None

                try
                    let! r = body ctx ct
                    bodyResult <- Some r
                with ex ->
                    bodyEx <- Some ex

                let mutable compEx: exn option = None

                try
                    compensation ()
                with ex ->
                    compEx <- Some ex

                match bodyEx, compEx with
                | Some bEx, _ -> return raise bEx
                | None, Some cEx -> return raise cEx
                | None, None -> return bodyResult.Value
            })

    member _.TryWith
        (Flow body: Flow<'ctx, 'error, 'a>, handler: exn -> Flow<'ctx, 'error, 'a>)
        : Flow<'ctx, 'error, 'a> =
        Flow(fun ctx ct ->
            task {
                try
                    return! body ctx ct
                with ex ->
                    if
                        ex :? OperationCanceledException
                        || (not (isNull ex) && ex.ToString().Contains("OperationCanceledException"))
                    then
                        return! FlowHelpers.defer (fun () -> FlowHelpers.reject ex)
                    else
                        let (Flow h) = handler ex
                        return! h ctx ct
            })

    member _.Using
        (resource: 'resource, body: 'resource -> Flow<'ctx, 'error, 'a>)
        : Flow<'ctx, 'error, 'a> when 'resource :> IAsyncDisposable =
        Flow(fun ctx ct ->
            task {
                let mutable bodyResult: Result<'a, 'error> option = None
                let mutable bodyEx: exn option = None

                try
                    let (Flow b) = body resource
                    let! r = b ctx ct
                    bodyResult <- Some r
                with ex ->
                    bodyEx <- Some ex

                let mutable disposeEx: exn option = None

                try
                    do! FlowHelpers.awaitValueTask (resource.DisposeAsync())
                with ex ->
                    disposeEx <- Some ex

                match bodyEx, disposeEx with
                | Some bEx, _ -> return raise bEx
                | None, Some dEx -> return raise dEx
                | None, None -> return bodyResult.Value
            })

    member _.While(condition: unit -> bool, body: Flow<'ctx, 'error, unit>) : Flow<'ctx, 'error, unit> =
        Flow(fun ctx ct ->
            task {
                let mutable result = Ok()

                while condition () && Result.isOk result do
                    ct.ThrowIfCancellationRequested()

                    match progress with
                    | None ->
                        let (Flow runBody) = body
                        let! current = runBody ctx ct
                        result <- current
                    | Some guard ->
                        let before = guard.Stamp ctx
                        let (Flow runBody) = body
                        let! current = runBody ctx ct

                        match current with
                        | Error e -> result <- Error e
                        | Ok() when guard.Stamp ctx = before ->
                            result <- Error(guard.NoProgress "Loop body completed without progress")
                        | Ok() -> ()

                return result
            })

    member _.For(items: seq<'t>, body: 't -> Flow<'ctx, 'error, unit>) : Flow<'ctx, 'error, unit> =
        Flow(fun ctx ct ->
            task {
                let mutable result = Ok()
                use enum = items.GetEnumerator()

                while enum.MoveNext() && Result.isOk result do
                    ct.ThrowIfCancellationRequested()
                    let (Flow runBody) = body enum.Current
                    let! current = runBody ctx ct
                    result <- current

                return result
            })

module Flow =

    let create (f: 'ctx -> CancellationToken -> Task<Result<'a, 'error>>) : Flow<'ctx, 'error, 'a> = Flow f
    let run (ctx: 'ctx) (ct: CancellationToken) (Flow f: Flow<'ctx, 'error, 'a>) : Task<Result<'a, 'error>> = f ctx ct

    let fail (error: 'error) : Flow<'ctx, 'error, 'a> =
        Flow(fun _ _ -> Task.FromResult(Error error))

    let attempt (Flow f: Flow<'ctx, 'error, 'a>) : Flow<'ctx, 'error, Result<'a, 'error>> =
        Flow(fun ctx ct ->
            task {
                let! res = f ctx ct
                return Ok res
            })

type JsTcs<'T>() =
    let mutable completed = false
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

type AsyncSemaphore(maxCount: int) =
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
