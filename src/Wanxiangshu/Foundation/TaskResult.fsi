namespace Wanxiangshu.Foundation

open System
open System.Threading.Tasks

[<Sealed>]
type TaskResultBuilder =
    new: unit -> TaskResultBuilder
    member Return: value: 'value -> Task<Result<'value, 'error>>
    member ReturnFrom: value: Result<'value, 'error> -> Task<Result<'value, 'error>>
    member ReturnFrom: value: Task<Result<'value, 'error>> -> Task<Result<'value, 'error>>

    member Bind:
        value: Result<'value, 'error> * next: ('value -> Task<Result<'next, 'error>>) -> Task<Result<'next, 'error>>

    member Bind:
        value: Task<Result<'value, 'error>> * next: ('value -> Task<Result<'next, 'error>>) ->
            Task<Result<'next, 'error>>

    member Zero: unit -> Task<Result<unit, 'error>>

    member Combine:
        first: Task<Result<unit, 'error>> * next: (unit -> Task<Result<'value, 'error>>) -> Task<Result<'value, 'error>>

    member Delay: next: (unit -> Task<Result<'value, 'error>>) -> (unit -> Task<Result<'value, 'error>>)
    member Run: next: (unit -> Task<Result<'value, 'error>>) -> Task<Result<'value, 'error>>

    member TryWith:
        body: (unit -> Task<Result<'value, 'error>>) * handler: (exn -> Task<Result<'value, 'error>>) ->
            Task<Result<'value, 'error>>

    member TryFinally:
        body: (unit -> Task<Result<'value, 'error>>) * compensation: (unit -> unit) -> Task<Result<'value, 'error>>

    member Using:
        resource: 'resource * binder: ('resource -> Task<Result<'value, 'error>>) -> Task<Result<'value, 'error>>
            when 'resource :> IDisposable

    member While: guard: (unit -> bool) * body: (unit -> Task<Result<unit, 'error>>) -> Task<Result<unit, 'error>>

    member For: items: seq<'item> * body: ('item -> Task<Result<unit, 'error>>) -> Task<Result<unit, 'error>>

[<AutoOpen>]
module TaskResultCE =
    val taskResult: TaskResultBuilder
    val ofTask: value: Task<'value> -> Task<Result<'value, 'error>>
