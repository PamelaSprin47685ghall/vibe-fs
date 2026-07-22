namespace Wanxiangshu.Next.Kernel

open System
open System.Runtime.ExceptionServices
open System.Threading
open System.Threading.Tasks

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
            let (Flow f) = create ()
            f ctx ct)

    member this.Combine(first: Flow<'ctx, 'error, unit>, second: Flow<'ctx, 'error, 'a>) : Flow<'ctx, 'error, 'a> =
        this.Bind(first, (fun () -> second))

    member _.TryFinally(Flow body: Flow<'ctx, 'error, 'a>, compensation: unit -> unit) : Flow<'ctx, 'error, 'a> =
        Flow(fun ctx ct ->
            task {
                try
                    return! body ctx ct
                finally
                    compensation ()
            })

    member _.TryWith
        (Flow body: Flow<'ctx, 'error, 'a>, handler: exn -> Flow<'ctx, 'error, 'a>)
        : Flow<'ctx, 'error, 'a> =
        Flow(fun ctx ct ->
            task {
                try
                    return! body ctx ct
                with
                | :? OperationCanceledException as ex -> return raise ex
                | ex ->
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
                    do! resource.DisposeAsync().AsTask()
                with ex ->
                    disposeEx <- Some ex

                match bodyEx, disposeEx with
                | Some(:? OperationCanceledException as oce), Some dEx ->
                    let agg = AggregateException([| (oce :> exn); dEx |])
                    ExceptionDispatchInfo.Capture(agg).Throw()
                    return Unchecked.defaultof<Result<'a, 'error>>
                | Some(:? OperationCanceledException as oce), None -> return raise oce
                | Some bEx, Some dEx ->
                    let agg = AggregateException([| bEx; dEx |])
                    ExceptionDispatchInfo.Capture(agg).Throw()
                    return Unchecked.defaultof<Result<'a, 'error>>
                | Some bEx, None ->
                    ExceptionDispatchInfo.Capture(bEx).Throw()
                    return Unchecked.defaultof<Result<'a, 'error>>
                | None, Some dEx ->
                    match bodyResult with
                    | Some(Error e) ->
                        let bEx = Exception(sprintf "Flow error: %A" e)
                        bEx.Data.Add("FlowError", box e)
                        let agg = AggregateException([| bEx; dEx |])
                        ExceptionDispatchInfo.Capture(agg).Throw()
                        return Unchecked.defaultof<Result<'a, 'error>>
                    | Some(Ok _) ->
                        let agg = AggregateException("Dispose failed after successful body", dEx)
                        ExceptionDispatchInfo.Capture(agg).Throw()
                        return Unchecked.defaultof<Result<'a, 'error>>
                    | None ->
                        ExceptionDispatchInfo.Capture(dEx).Throw()
                        return Unchecked.defaultof<Result<'a, 'error>>
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

module Parallel =

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
                use semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency)

                let workTasks =
                    indexedItems
                    |> Array.map (fun item ->
                        task {
                            do! semaphore.WaitAsync(cancellation)

                            try
                                return! action item cancellation
                            finally
                                semaphore.Release() |> ignore
                        })

                let! results = Task.WhenAll(workTasks)
                return results |> Array.toList
        }
