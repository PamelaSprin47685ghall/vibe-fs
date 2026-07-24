namespace Wanxiangshu.Next.Process

open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

module LargeGate =

    let mutable private gatePromise: JS.Promise<unit> =
        JS.Constructors.Promise.resolve ()

    let mutable private releaseCurrent: (unit -> unit) option = None

    let getCount () : int = if releaseCurrent.IsNone then 1 else 0

    let acquire (_ct: CancellationToken) : Task =
        let mutable resolvePrevious = ignore

        let next =
            JS.Constructors.Promise.Create(fun resolve _ -> resolvePrevious <- resolve)

        let previous = gatePromise
        gatePromise <- next
        let completion = TaskCompletionSource<unit>()

        emitJsExpr
            (previous,
             (fun () ->
                 releaseCurrent <- Some resolvePrevious
                 completion.SetResult(())))
            "$0.then($1)"
        |> ignore

        completion.Task

    let release () : unit =
        match releaseCurrent with
        | Some resolve ->
            releaseCurrent <- None
            resolve ()
        | None -> ()
