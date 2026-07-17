module Wanxiangshu.Runtime.PromiseQueue

open Fable.Core

/// Single-threaded serial queue backed by a ResizeArray. Unlike the prior
/// Promise-chain design, completed tasks are removed immediately so their
/// closures — including any captured rawEvent objects — become unreachable.
/// Without this, high-frequency streaming events build an unbounded chain of
/// PromiseReaction nodes that V8 cannot GC.
type IExceptionObserver =
    abstract OnException: exn -> unit

type SerialQueue(?observer: IExceptionObserver) =
    let queue = ResizeArray<unit -> JS.Promise<unit>>()
    let mutable running = false

    let rec processQueue () =
        promise {
            if queue.Count = 0 then
                running <- false
            else
                running <- true
                let task = queue.[0]
                queue.RemoveAt(0)

                try
                    do! task ()
                with _ ->
                    ()

                do! processQueue ()
        }

    member _.Enqueue(work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        Promise.create (fun resolve reject ->
            let task () =
                promise {
                    try
                        let! result = work ()
                        resolve result
                    with ex ->
                        observer |> Option.iter (fun o -> o.OnException ex)
                        reject ex
                }

            queue.Add(task)

            if not running then
                running <- true
                processQueue () |> Promise.start)

/// Race a promise against a timeout. Returns None when the timeout wins, Some
/// value when the work resolves first.
let withTimeout (timeoutMs: int) (work: JS.Promise<'T>) : JS.Promise<'T option> =
    let timeoutPromise =
        promise {
            do! Promise.sleep timeoutMs
            return None
        }

    let workPromise =
        promise {
            let! res = work
            return Some res
        }

    Promise.race [ timeoutPromise; workPromise ]
