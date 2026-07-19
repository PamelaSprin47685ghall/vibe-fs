module Wanxiangshu.Runtime.Fallback.RetryDispatchGovernor

open Fable.Core
open Fable.Core.JsInterop

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

[<Emit("typeof performance !== 'undefined' ? performance.now() : Date.now()")>]
let private getMonotonicTimeMs () : float = jsNative

/// Key for rate-limiting and serialization: contains workspace, session, provider/model.
type RetryModelKey =
    { Workspace: string
      SessionID: string
      ProviderID: string
      ModelID: string
      Variant: string option }

    static member Create
        (workspace: string, sessionId: string, providerId: string, modelId: string, ?variant: string)
        : RetryModelKey =
        { Workspace = workspace
          SessionID = sessionId
          ProviderID = providerId
          ModelID = modelId
          Variant = variant }

/// Result of a retry dispatch attempt.
type RetryDispatchResult =
    | Dispatched
    | CancelledBeforeDispatch

/// Per-key rate-limit gate: at most one dispatch per (provider, model,
/// variant) tuple in the configured window.
/// Per-session serialization queue: at most one active dispatch per physical session.
type RetryDispatchGovernor(?rateLimitMs: int64) =
    let rateLimitMs = defaultArg rateLimitMs 10000L

    let mutable lastActualDispatchAt: Map<string, float> = Map.empty
    let mutable lastSessionUseAt: Map<string, float> = Map.empty

    let mutable sessionQueues: Map<string, Wanxiangshu.Runtime.PromiseQueue.SerialQueue> =
        Map.empty

    let mutable providerQueues: Map<string, Wanxiangshu.Runtime.PromiseQueue.SerialQueue> =
        Map.empty

    let lockObj = obj ()

    let getOrCreateSessionQueue (key: string) : Wanxiangshu.Runtime.PromiseQueue.SerialQueue =
        match Map.tryFind key sessionQueues with
        | Some q -> q
        | None ->
            let q = Wanxiangshu.Runtime.PromiseQueue.SerialQueue()
            sessionQueues <- Map.add key q sessionQueues
            q

    let getOrCreateProviderQueue (key: string) : Wanxiangshu.Runtime.PromiseQueue.SerialQueue =
        match Map.tryFind key providerQueues with
        | Some q -> q
        | None ->
            let q = Wanxiangshu.Runtime.PromiseQueue.SerialQueue()
            providerQueues <- Map.add key q providerQueues
            q

    /// Enqueue a dispatch for the given key. The dispatch function will run
    /// only when the rate-limit window allows. Returns CancelledBeforeDispatch
    /// if stillValid returns false before the actual dispatch.
    member _.RunWhenAllowed
        (key: RetryModelKey, stillValid: unit -> bool, dispatch: unit -> JS.Promise<unit>)
        : JS.Promise<RetryDispatchResult> =
        promise {
            let sessionKey = $"{key.Workspace}/{key.SessionID}"

            let providerKey =
                match key.Variant with
                | Some v when v <> "" -> $"{key.ProviderID}/{key.ModelID}:{v}"
                | _ -> $"{key.ProviderID}/{key.ModelID}"

            let sessionQueue =
                lock lockObj (fun () ->
                    lastSessionUseAt <- Map.add sessionKey (getMonotonicTimeMs ()) lastSessionUseAt
                    getOrCreateSessionQueue sessionKey)

            // Outer enqueue: serialize per physical session (workspace + sessionId)
            return!
                sessionQueue.Enqueue(fun () ->
                    promise {
                        // Check if still valid after obtaining the session lock/queue slot
                        if not (stillValid ()) then
                            return CancelledBeforeDispatch
                        else
                            let providerQueue = lock lockObj (fun () -> getOrCreateProviderQueue providerKey)

                            // Inner enqueue: rate limit per provider/model
                            return!
                                providerQueue.Enqueue(fun () ->
                                    promise {
                                        // Check stillValid again after obtaining the provider queue slot
                                        if not (stillValid ()) then
                                            return CancelledBeforeDispatch
                                        else
                                            let effectiveRateLimitMs =
                                                if nodeProcess?env?("WANXIANGSHU_TEST") = "true" then
                                                    0.0
                                                else
                                                    float rateLimitMs

                                            let now = getMonotonicTimeMs ()

                                            let lastDispatch =
                                                lock lockObj (fun () ->
                                                    Map.tryFind providerKey lastActualDispatchAt
                                                    |> Option.defaultValue 0.0)

                                            let elapsed = now - lastDispatch
                                            let delay = max 0.0 (effectiveRateLimitMs - elapsed)

                                            if delay > 0.0 then
                                                do! Promise.sleep (int delay)

                                            // Final validity check after waiting/delay
                                            if not (stillValid ()) then
                                                return CancelledBeforeDispatch
                                            else
                                                let actualNow = getMonotonicTimeMs ()

                                                lock lockObj (fun () ->
                                                    lastActualDispatchAt <-
                                                        Map.add providerKey actualNow lastActualDispatchAt)

                                                do! dispatch ()
                                                return Dispatched
                                    })
                    })
        }

    /// Remove stale entries that haven't been used for a long time.
    member _.Cleanup(staleThresholdMs: int64) : unit =
        let now = getMonotonicTimeMs ()
        let cutoff = now - float staleThresholdMs

        lock lockObj (fun () ->
            lastActualDispatchAt <- lastActualDispatchAt |> Map.filter (fun _ last -> last >= cutoff)
            lastSessionUseAt <- lastSessionUseAt |> Map.filter (fun _ last -> last >= cutoff)

            providerQueues <- providerQueues |> Map.filter (fun k _ -> Map.containsKey k lastActualDispatchAt)
            sessionQueues <- sessionQueues |> Map.filter (fun k _ -> Map.containsKey k lastSessionUseAt))

    /// Reset all state (for testing).
    member _.Reset() : unit =
        lock lockObj (fun () ->
            lastActualDispatchAt <- Map.empty
            lastSessionUseAt <- Map.empty
            sessionQueues <- Map.empty
            providerQueues <- Map.empty)
