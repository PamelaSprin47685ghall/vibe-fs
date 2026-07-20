module Wanxiangshu.Runtime.SubagentAbort

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn

let signalAborted (signal: obj) : bool =
    not (Dyn.isNullish signal) && Dyn.truthy (Dyn.get signal "aborted")

let raceWithAbortSignal (signal: obj) (onAbort: unit -> unit) (work: JS.Promise<'T>) : JS.Promise<'T> =
    if Dyn.isNullish signal then
        work
    else
        let rejecter =
            emitJsExpr () "Object.assign(new Error('Aborted'), { name: 'AbortError' })"

        Promise.create (fun resolve reject ->
            let mutable isDone = false
            let mutable handler: obj = null

            let removeHandler () =
                if not (isNull handler) then
                    try
                        signal?removeEventListener ("abort", handler)
                    with _ ->
                        ()

                    handler <- null

            let settleAbort () =
                if not isDone then
                    isDone <- true
                    removeHandler ()

                    try
                        onAbort ()
                    with _ ->
                        ()

                    reject rejecter

            let settleWork (continuation: unit -> unit) () =
                if not isDone then
                    isDone <- true
                    removeHandler ()
                    continuation ()

            if signalAborted signal then
                settleAbort ()
            else
                handler <- box (fun () -> settleAbort ())

                try
                    signal?addEventListener ("abort", handler)
                with _ ->
                    ()

                work?``then`` (
                    (fun res -> settleWork (fun () -> resolve res) ()),
                    (fun err -> settleWork (fun () -> reject err) ())
                )
                |> ignore)
