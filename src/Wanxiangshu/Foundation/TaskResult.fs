namespace Wanxiangshu.Foundation

open System
open System.Threading.Tasks

[<Sealed>]
type TaskResultBuilder() =
    member _.Return(value: 'value) : Task<Result<'value, 'error>> = Task.FromResult(Ok value)

    member _.ReturnFrom(value: Result<'value, 'error>) : Task<Result<'value, 'error>> = Task.FromResult value

    member _.ReturnFrom(value: Task<Result<'value, 'error>>) : Task<Result<'value, 'error>> = value

    member _.Bind
        (value: Result<'value, 'error>, next: 'value -> Task<Result<'next, 'error>>)
        : Task<Result<'next, 'error>> =
        match value with
        | Ok resolved -> next resolved
        | Error error -> Task.FromResult(Error error)

    member _.Bind
        (value: Task<Result<'value, 'error>>, next: 'value -> Task<Result<'next, 'error>>)
        : Task<Result<'next, 'error>> =
        task {
            match! value with
            | Ok resolved -> return! next resolved
            | Error error -> return Error error
        }

    member _.Zero() : Task<Result<unit, 'error>> = Task.FromResult(Ok())

    member this.Combine
        (first: Task<Result<unit, 'error>>, next: unit -> Task<Result<'value, 'error>>)
        : Task<Result<'value, 'error>> =
        this.Bind(first, next)

    member _.Delay(next: unit -> Task<Result<'value, 'error>>) = next

    member _.Run(next: unit -> Task<Result<'value, 'error>>) = next ()

    member _.TryWith
        (body: unit -> Task<Result<'value, 'error>>, handler: exn -> Task<Result<'value, 'error>>)
        : Task<Result<'value, 'error>> =
        task {
            try
                return! body ()
            with ex ->
                return! handler ex
        }

    member _.TryFinally
        (body: unit -> Task<Result<'value, 'error>>, compensation: unit -> unit)
        : Task<Result<'value, 'error>> =
        task {
            try
                return! body ()
            finally
                compensation ()
        }

    member this.Using
        (resource: 'resource, binder: 'resource -> Task<Result<'value, 'error>>)
        : Task<Result<'value, 'error>> when 'resource :> IDisposable =
        this.TryFinally(
            (fun () -> binder resource),
            fun () ->
                if not (isNull (box resource)) then
                    resource.Dispose()
        )

    member this.While(guard: unit -> bool, body: unit -> Task<Result<unit, 'error>>) : Task<Result<unit, 'error>> =
        let rec loop () =
            if guard () then this.Bind(body (), loop) else this.Zero()

        loop ()

    member this.For(items: seq<'item>, body: 'item -> Task<Result<unit, 'error>>) : Task<Result<unit, 'error>> =
        let list = Seq.toList items

        let rec loop remaining =
            match remaining with
            | [] -> this.Zero()
            | head :: tail -> this.Bind(body head, (fun () -> loop tail))

        loop list

[<AutoOpen>]
module TaskResultCE =
    let taskResult = TaskResultBuilder()

    let ofTask (value: Task<'value>) : Task<Result<'value, 'error>> =
        task {
            let! resolved = value
            return Ok resolved
        }
