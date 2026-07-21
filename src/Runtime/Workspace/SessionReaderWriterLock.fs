namespace Wanxiangshu.Runtime

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.PromiseQueue

type SessionReaderWriterLock() =
    let queue = SerialQueue()
    let mutable activeReaders = 0
    let mutable noReadersResolver: ((unit -> unit) * (exn -> unit)) option = None
    let mutable disposed = false

    let awaitNoReaders () : JS.Promise<unit> =
        if activeReaders = 0 then
            Promise.lift ()
        else
            Promise.create (fun resolve reject -> noReadersResolver <- Some(resolve, reject))

    let releaseReader () =
        if not disposed then
            activeReaders <- activeReaders - 1

            if activeReaders < 0 then
                activeReaders <- 0
                failwith "AssertionError: activeReaders is negative"

            if activeReaders = 0 then
                match noReadersResolver with
                | Some(r, _) ->
                    noReadersResolver <- None
                    r ()
                | None -> ()

    member _.Dispose() =
        disposed <- true
        queue.Poisoned <- true

        match noReadersResolver with
        | Some(_, reject) ->
            noReadersResolver <- None
            reject (exn "DisposedException: Lock was disposed")
        | None -> ()

        activeReaders <- 0

    member _.ResetPoison() = queue.ResetPoison()

    member _.EnqueueRead(work: unit -> JS.Promise<'T>, ?timeoutMs: int, ?abortSignal: obj) : JS.Promise<'T> =
        if disposed then
            Promise.reject (exn "DisposedException: Lock was disposed")
        else
            let timeout = defaultArg timeoutMs 120000

            Promise.create (fun resolve reject ->
                queue.Enqueue(
                    (fun () ->
                        if disposed then
                            reject (exn "DisposedException: Lock was disposed")
                        else
                            activeReaders <- activeReaders + 1
                            let mutable released = false

                            let safeReleaseReader () =
                                if not released then
                                    released <- true
                                    releaseReader ()

                            let mutable cleanupAbort = fun () -> ()

                            match abortSignal with
                            | Some sigObj ->
                                let onAbort () =
                                    safeReleaseReader ()
                                    reject (exn "AbortError: The reader lock request was aborted")

                                sigObj?addEventListener ("abort", onAbort)
                                cleanupAbort <- fun () -> sigObj?removeEventListener ("abort", onAbort)
                            | None -> ()

                            try
                                let timeoutPromise = PromiseQueue.withTimeout timeout (work ())

                                timeoutPromise
                                |> Promise.map (fun resOpt ->
                                    cleanupAbort ()
                                    safeReleaseReader ()

                                    match resOpt with
                                    | Some res -> resolve res
                                    | None -> reject (exn "ReaderLockTimeout: Reader timed out"))
                                |> Promise.catch (fun ex ->
                                    cleanupAbort ()
                                    safeReleaseReader ()
                                    reject ex)
                                |> Promise.start
                            with ex ->
                                cleanupAbort ()
                                safeReleaseReader ()
                                reject ex

                        Promise.lift ()),
                    ?abortSignal = abortSignal
                )
                |> Promise.catch (fun ex -> reject ex)
                |> ignore)

    member _.EnqueueWrite(work: unit -> JS.Promise<'T>, ?timeoutMs: int) : JS.Promise<'T> =
        if disposed then
            Promise.reject (exn "DisposedException: Lock was disposed")
        else
            let timeout = defaultArg timeoutMs 120000

            queue.Enqueue(
                (fun () ->
                    promise {
                        if disposed then
                            return failwith "DisposedException: Lock was disposed"
                        else
                            let runWrite () =
                                promise {
                                    do! awaitNoReaders ()
                                    return! work ()
                                }

                            let! resOpt = PromiseQueue.withTimeout timeout (runWrite ())

                            match resOpt with
                            | Some res -> return res
                            | None -> return failwith "WriterLockTimeout: Writer timed out"
                    })
            )
