module Wanxiangshu.Shell.PromiseQueue

open Fable.Core

/// Single-threaded, lock-free async serial queue. Replaces the legacy F# Agent
/// actor pattern with a plain Promise chain: tasks run in the
/// order they are enqueued, the tail swallows predecessor exceptions so the queue
/// never jams, and each Enqueue resolves/rejects with its own task's outcome.
type IExceptionObserver =
    abstract OnException: exn -> unit

type SerialQueue(?observer: IExceptionObserver) =
    let tail = ref (Promise.lift ())

    member _.Enqueue(work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        Promise.create (fun resolve reject ->
            let runNext () =
                promise {
                    try
                        let! result = work ()
                        resolve result
                    with ex ->
                        observer |> Option.iter (fun o -> o.OnException ex)
                        reject ex
                }
            let oldTail = tail.Value
            tail.Value <-
                oldTail
                |> Promise.catch (fun ex ->
                    observer |> Option.iter (fun o -> o.OnException ex))
                |> Promise.bind (fun _ -> runNext () |> Promise.map ignore))

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
