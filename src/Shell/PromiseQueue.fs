module Wanxiangshu.Shell.PromiseQueue

open Fable.Core
open Fable.Core.JsInterop

[<Global("process")>]
let private nodeProcess: obj = jsNative

let private killHostProcessIfFatal (msg: string) : unit =
    try
        let isTest =
            not (isNull nodeProcess?env?WANXIANGSHU_TEST)
            && string nodeProcess?env?WANXIANGSHU_TEST = "1"

        if not isTest then
            JS.console.error ($"[FATAL HOST TERMINATION] {msg}")
            nodeProcess?exit (1) |> ignore
    with _ ->
        ()

/// Single-threaded, lock-free async serial queue. Replaces the legacy F# Agent
/// actor pattern with a plain Promise chain: tasks run in the
/// order they are enqueued, the tail swallows predecessor exceptions so the queue
/// never jams, and each Enqueue resolves/rejects with its own task's outcome.
type IExceptionObserver =
    abstract OnException: exn -> unit

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

type SerialQueue(?observer: IExceptionObserver, ?defaultSafetyTimeoutMs: int) =
    let tail = ref (Promise.lift ())
    let safetyMarginMs = defaultArg defaultSafetyTimeoutMs 120000

    member _.Enqueue(work: unit -> JS.Promise<'T>, ?timeoutMs: int) : JS.Promise<'T> =
        Promise.create (fun resolve reject ->
            let runNext () =
                promise {
                    try
                        let safetyMs = defaultArg timeoutMs safetyMarginMs
                        let! resOpt = withTimeout safetyMs (work ())

                        match resOpt with
                        | Some result -> resolve result
                        | None ->
                            let msg =
                                $"[FATAL EXECUTOR QUEUE BUG] SerialQueue task failed to settle within {safetyMs}ms timeout window (expected <= 100s). Fail-fast triggered."

                            JS.console.error msg
                            killHostProcessIfFatal msg
                            let ex = exn msg
                            observer |> Option.iter (fun o -> o.OnException ex)
                            reject ex
                    with ex ->
                        observer |> Option.iter (fun o -> o.OnException ex)
                        reject ex
                }

            let oldTail = tail.Value

            tail.Value <-
                oldTail
                |> Promise.catch (fun ex -> observer |> Option.iter (fun o -> o.OnException ex))
                |> Promise.bind (fun _ -> runNext () |> Promise.map ignore))
