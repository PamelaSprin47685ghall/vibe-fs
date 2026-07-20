module Wanxiangshu.Runtime.Fallback.RetryDispatchGovernor

open Fable.Core
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime.ToolSequenceThrottle

[<Emit("typeof performance !== 'undefined' ? performance.now() : Date.now()")>]
let private getMonotonicTimeMs () : float = jsNative

/// Result of a transport schedule attempt.
type RetryDispatchResult =
    | Dispatched
    | CancelledBeforeDispatch

/// Clock abstraction for testability (virtual clock in tests; no production delay zeroing).
type IClock =
    abstract GetMonotonicTimeMs: unit -> float

type SystemClock() =
    interface IClock with
        member _.GetMonotonicTimeMs() = getMonotonicTimeMs ()

/// Provider-credential + model transport key.
/// Rate limit and serial queue are shared by every session in the same workspace
/// that targets the same provider/model/variant. Workspace is part of the key so
/// distinct workspaces never share stamps or queues.
/// Session single in-flight is NOT this type's job — that belongs to the session actor.
type ProviderModelTransportKey =
    { Workspace: string
      ProviderID: string
      ModelID: string
      Variant: string option }

    static member Create
        (workspace: string, providerId: string, modelId: string, ?variant: string)
        : ProviderModelTransportKey =
        { Workspace = workspace
          ProviderID = providerId
          ModelID = modelId
          Variant = variant }

/// Backward-compatible alias used by older call sites / tests during migration.
type RetryModelKey = ProviderModelTransportKey

/// Process-local provider transport scheduler:
/// real per-key SerialQueue + spacing window. Not a session prompt mutex.
type RetryDispatchGovernor(?rateLimitMs: int64, ?clock: IClock, ?sleeper: ISleeper) =
    let rateLimitMs = defaultArg rateLimitMs 10000L
    let clock = defaultArg clock (SystemClock() :> IClock)
    let sleeper = defaultArg sleeper (PromiseSleeper() :> ISleeper)

    let mutable lastActualDispatchAt: Map<ProviderModelTransportKey, float> = Map.empty
    let mutable transportQueues: Map<ProviderModelTransportKey, SerialQueue> = Map.empty
    let gate = obj ()

    let getOrCreateTransportQueue (key: ProviderModelTransportKey) : SerialQueue =
        match Map.tryFind key transportQueues with
        | Some q -> q
        | None ->
            let q = SerialQueue()
            transportQueues <- Map.add key q transportQueues
            q

    /// Enqueue transport work for one provider/model key.
    /// Same key: fully serial (second cannot compute wait or send until first finishes).
    /// Different keys: independent queues and stamps.
    member _.RunWhenAllowed
        (key: ProviderModelTransportKey, stillValid: unit -> bool, dispatch: unit -> JS.Promise<unit>)
        : JS.Promise<RetryDispatchResult> =
        let queue = lock gate (fun () -> getOrCreateTransportQueue key)

        queue.Enqueue(fun () ->
            promise {
                if not (stillValid ()) then
                    return CancelledBeforeDispatch
                else
                    let now = clock.GetMonotonicTimeMs()

                    // Missing key ⇒ never dispatched: allow immediately.
                    // Defaulting to 0.0 would sleep ~rateLimitMs on cold start
                    // because monotonic clocks start near zero.
                    let lastDispatch =
                        lock gate (fun () -> Map.tryFind key lastActualDispatchAt)

                    let delay =
                        match lastDispatch with
                        | None -> 0.0
                        | Some last -> max 0.0 (float rateLimitMs - (now - last))

                    if delay > 0.0 then
                        do! sleeper.Sleep(int delay)

                    if not (stillValid ()) then
                        return CancelledBeforeDispatch
                    else
                        let actualNow = clock.GetMonotonicTimeMs()

                        lock gate (fun () ->
                            lastActualDispatchAt <- Map.add key actualNow lastActualDispatchAt)

                        do! dispatch ()
                        return Dispatched
            })

    member _.Cleanup(staleThresholdMs: int64) : unit =
        let now = clock.GetMonotonicTimeMs()
        let cutoff = now - float staleThresholdMs

        lock gate (fun () ->
            lastActualDispatchAt <- lastActualDispatchAt |> Map.filter (fun _ last -> last >= cutoff)
            transportQueues <- transportQueues |> Map.filter (fun k _ -> Map.containsKey k lastActualDispatchAt))

    member _.Reset() : unit =
        lock gate (fun () ->
            lastActualDispatchAt <- Map.empty
            transportQueues <- Map.empty)
