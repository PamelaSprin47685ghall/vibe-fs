module Wanxiangshu.Runtime.PromiseQueue

open Fable.Core
open Fable.Core.JsInterop

[<Emit("performance.now()")>]
let private now () : float = jsNative

type IExceptionObserver =
    abstract OnException: exn -> unit

[<RequireQualifiedAccess>]
type QueueItemState =
    | Pending
    | Completed
    | Failed of exn
    | TimedOutAndCancelled
    | TimedOutAndQueuePoisoned

let private setupAbortListener (abortSignal: obj option) (stateRef: QueueItemState ref) (reject: exn -> unit) =
    match abortSignal with
    | Some sigObj ->
        let onAbort () =
            if stateRef.Value = QueueItemState.Pending then
                stateRef.Value <- QueueItemState.TimedOutAndCancelled
                reject (exn "AbortError: The operation was aborted")

        sigObj?addEventListener ("abort", onAbort)
        fun () -> sigObj?removeEventListener ("abort", onAbort)
    | None -> fun () -> ()

let private executeTask
    (work: unit -> JS.Promise<'T>)
    (timeout: int)
    (deadline: float)
    (itemGen: int)
    (stateRef: QueueItemState ref)
    (queuePoisonedRef: bool ref)
    (queueGenRef: int ref)
    (resolve: 'T -> unit)
    (reject: exn -> unit)
    (observer: IExceptionObserver option)
    =
    promise {
        if queuePoisonedRef.Value then
            stateRef.Value <- QueueItemState.TimedOutAndQueuePoisoned
            reject (exn "QueuePoisoned: Queue is poisoned")
        elif stateRef.Value = QueueItemState.Pending then
            if now () > deadline then
                stateRef.Value <- QueueItemState.TimedOutAndQueuePoisoned
                queuePoisonedRef.Value <- true
                queueGenRef.Value <- queueGenRef.Value + 1
                reject (exn "TimeoutError: Task deadline exceeded before start")
            else
                try
                    let timeoutPromise =
                        promise {
                            do! Promise.sleep timeout
                            return None
                        }

                    let workPromise =
                        promise {
                            let! res = work ()
                            return Some res
                        }

                    let! resOpt = Promise.race [ timeoutPromise; workPromise ]

                    match resOpt with
                    | Some res ->
                        if itemGen = queueGenRef.Value then
                            stateRef.Value <- QueueItemState.Completed
                            resolve res
                        else
                            stateRef.Value <- QueueItemState.TimedOutAndQueuePoisoned
                            reject (exn "QueuePoisoned: Task completed but generation changed")
                    | None ->
                        stateRef.Value <- QueueItemState.TimedOutAndQueuePoisoned
                        queuePoisonedRef.Value <- true
                        queueGenRef.Value <- queueGenRef.Value + 1
                        reject (exn "TimeoutError: Task timed out")
                with ex ->
                    stateRef.Value <- QueueItemState.Failed ex
                    observer |> Option.iter (fun o -> o.OnException ex)
                    reject ex
    }

type SerialQueue(?observer: IExceptionObserver) =
    let queue = ResizeArray<unit -> JS.Promise<unit>>()
    let mutable running = false
    let mutable poisoned = false
    let mutable generation = 0

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

    member _.Poisoned
        with get () = poisoned
        and set (v) = poisoned <- v

    member _.Generation
        with get () = generation
        and set (v) = generation <- v

    member _.Enqueue(work: unit -> JS.Promise<'T>, ?name: string, ?timeoutMs: int, ?abortSignal: obj) : JS.Promise<'T> =
        if poisoned then
            Promise.reject (exn "QueuePoisoned: The serial queue is poisoned due to a previous task timeout")
        else
            Promise.create (fun resolve reject ->
                let timeout = defaultArg timeoutMs 10000
                let deadline = now () + float timeout
                let itemGen = generation
                let stateRef = ref QueueItemState.Pending
                let cleanup = setupAbortListener abortSignal stateRef reject

                let task () =
                    promise {
                        cleanup ()
                        let poisonedRef = ref poisoned
                        let genRef = ref generation

                        do!
                            executeTask
                                work
                                timeout
                                deadline
                                itemGen
                                stateRef
                                poisonedRef
                                genRef
                                resolve
                                reject
                                observer

                        poisoned <- poisonedRef.Value
                        generation <- genRef.Value
                    }

                queue.Add(task)

                if not running then
                    running <- true
                    processQueue () |> Promise.start)

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
