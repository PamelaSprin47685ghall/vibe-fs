namespace Wanxiangshu.Runtime

open Fable.Core
open PromiseQueue

type SessionReaderWriterLock() =
    let queue = SerialQueue()
    let mutable activeReaders = 0
    let mutable readerZeroPromises: (unit -> unit) list = []

    let awaitNoReaders () : JS.Promise<unit> =
        if activeReaders = 0 then
            Promise.lift ()
        else
            Promise.create (fun resolve _ -> readerZeroPromises <- resolve :: readerZeroPromises)

    let releaseReader () =
        activeReaders <- activeReaders - 1

        if activeReaders = 0 then
            let temp = readerZeroPromises
            readerZeroPromises <- []

            for resolve in temp do
                resolve ()

    member _.EnqueueRead(work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        Promise.create (fun resolve reject ->
            queue.Enqueue(fun () ->
                activeReaders <- activeReaders + 1
                let p = work ()

                p
                |> Promise.map (fun res ->
                    releaseReader ()
                    resolve res)
                |> Promise.catch (fun ex ->
                    releaseReader ()
                    reject ex)
                |> Promise.start

                Promise.lift ())
            |> Promise.catch (fun ex -> reject ex)
            |> ignore)

    member _.EnqueueWrite(work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        queue.Enqueue(fun () ->
            promise {
                do! awaitNoReaders ()
                return! work ()
            })
