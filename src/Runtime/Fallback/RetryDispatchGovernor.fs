module Wanxiangshu.Runtime.Fallback.RetryDispatchGovernor

open Fable.Core
open Fable.Core.JsInterop

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

/// Key for rate-limiting: providerID/modelID[:variant].
type RetryModelKey =
    | RetryModelKey of string

    static member Create(providerId: string, modelId: string, ?variant: string) : RetryModelKey =
        match variant with
        | Some v when v <> "" -> RetryModelKey $"{providerId}/{modelId}:{v}"
        | _ -> RetryModelKey $"{providerId}/{modelId}"

    member this.Value: string =
        let (RetryModelKey v) = this
        v

/// Result of a retry dispatch attempt.
type RetryDispatchResult =
    | Dispatched
    | CancelledBeforeDispatch

/// Per-key rate-limit gate: at most one dispatch per (provider, model,
/// variant) tuple in the configured window.  A separate process-wide
/// SerialQueue per key guarantees that two concurrent calls for the
/// same key cannot both pass the stillValid check at the same instant
/// and then both call the dispatch lambda.
///
/// The per-key queue is the rate limiter.  Without it, two concurrent
/// calls could each see `elapsed > rateLimitMs`, both call the dispatch
/// lambda, and the host would receive two prompts in the same window.
/// With the per-key queue, the second call observes the first call's
/// `lastActualDispatchAt` update and waits its turn.
type RetryDispatchGovernor(?rateLimitMs: int64) =
    let rateLimitMs = defaultArg rateLimitMs 10000L

    let mutable lastActualDispatchAt: Map<string, int64> = Map.empty

    let mutable keyQueues: Map<string, Wanxiangshu.Runtime.PromiseQueue.SerialQueue> =
        Map.empty

    let lockObj = obj ()

    let getOrCreateQueue (key: string) : Wanxiangshu.Runtime.PromiseQueue.SerialQueue =
        match Map.tryFind key keyQueues with
        | Some q -> q
        | None ->
            let q = Wanxiangshu.Runtime.PromiseQueue.SerialQueue()
            keyQueues <- Map.add key q keyQueues
            q

    /// Enqueue a dispatch for the given key. The dispatch function will run
    /// only when the rate-limit window allows. Returns CancelledBeforeDispatch
    /// if stillValid returns false before the actual dispatch.
    member _.RunWhenAllowed
        (key: RetryModelKey, stillValid: unit -> bool, dispatch: unit -> JS.Promise<unit>)
        : JS.Promise<RetryDispatchResult> =
        promise {
            let queue = lock lockObj (fun () -> getOrCreateQueue key.Value)

            let body () : JS.Promise<RetryDispatchResult> =
                promise {
                    do! Promise.sleep 0 // yield to avoid blocking the caller

                    let effectiveRateLimitMs =
                        if nodeProcess?env?("WANXIANGSHU_TEST") = "true" then
                            0L
                        else
                            rateLimitMs

                    let delay, now =
                        lock lockObj (fun () ->
                            let now = System.DateTime.UtcNow.Ticks / 10000L // ms

                            let lastDispatch =
                                Map.tryFind key.Value lastActualDispatchAt |> Option.defaultValue 0L

                            let elapsed = now - lastDispatch
                            let delay = max 0L (effectiveRateLimitMs - elapsed)
                            delay, now)

                    if delay > 0L then
                        do! Promise.sleep (int delay)

                    // Re-check after waiting
                    if not (stillValid ()) then
                        return CancelledBeforeDispatch
                    else
                        let actualNow = System.DateTime.UtcNow.Ticks / 10000L

                        lock lockObj (fun () ->
                            lastActualDispatchAt <- Map.add key.Value actualNow lastActualDispatchAt)

                        do! dispatch ()
                        return Dispatched
                }

            return! queue.Enqueue body
        }

    /// Remove stale entries that haven't been used for a long time.
    member _.Cleanup(staleThresholdMs: int64) : unit =
        let cutoff = System.DateTime.UtcNow.Ticks / 10000L - staleThresholdMs

        lock lockObj (fun () ->
            lastActualDispatchAt <- lastActualDispatchAt |> Map.filter (fun _ last -> last >= cutoff)

            keyQueues <- keyQueues |> Map.filter (fun k _ -> Map.containsKey k lastActualDispatchAt))

    /// Reset all state (for testing).
    member _.Reset() : unit =
        lock lockObj (fun () ->
            lastActualDispatchAt <- Map.empty
            keyQueues <- Map.empty)
