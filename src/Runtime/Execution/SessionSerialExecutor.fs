namespace Wanxiangshu.Runtime

open Fable.Core

type SessionSerialExecutor() =
    let mutable tail: JS.Promise<unit> = Promise.lift ()
    let mutable accepting = true
    let mutable pendingCount = 0

    let settle (work: JS.Promise<'T>) : JS.Promise<unit> =
        work |> Promise.map ignore |> Promise.catch (fun _ -> ())

    member _.IsClosed = not accepting
    member _.PendingCount = pendingCount
    member _.Drained = tail

    member _.Close() : unit = accepting <- false

    member _.Enqueue(work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        if not accepting then
            Promise.reject (exn "SessionExecutorClosed: session executor is closed")
        else
            pendingCount <- pendingCount + 1
            let predecessor = tail

            let result =
                promise {
                    do! predecessor

                    if not accepting then
                        return raise (exn "SessionExecutorClosed: session executor is closed")

                    return! work ()
                }

            tail <- settle result |> Promise.map (fun () -> pendingCount <- pendingCount - 1)

            result
