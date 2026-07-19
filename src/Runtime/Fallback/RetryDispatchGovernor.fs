module Wanxiangshu.Runtime.Fallback.RetryDispatchGovernor

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime.ToolSequenceThrottle

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

/// Clock abstraction for testability.
type IClock =
    abstract GetMonotonicTimeMs: unit -> float

/// Default system clock.
type SystemClock() =
    interface IClock with
        member _.GetMonotonicTimeMs() = getMonotonicTimeMs ()

/// Key representing the session serialization scope.
type SessionKey =
    { Workspace: string; SessionID: string }

/// Key representing the provider rate-limiting scope.
type ProviderKey =
    { Workspace: string
      ProviderID: string
      ModelID: string
      Variant: string option }

/// Per-key rate-limit gate: at most one dispatch per (provider, model,
/// variant) tuple in the configured window.
/// Per-session serialization queue: at most one active dispatch per physical session.
type RetryDispatchGovernor(?rateLimitMs: int64, ?clock: IClock, ?sleeper: ISleeper) =
    let rateLimitMs = defaultArg rateLimitMs 10000L
    let clock = defaultArg clock (SystemClock() :> IClock)
    let sleeper = defaultArg sleeper (PromiseSleeper() :> ISleeper)

    let mutable lastActualDispatchAt: Map<ProviderKey, float> = Map.empty
    let mutable lastSessionUseAt: Map<SessionKey, float> = Map.empty

    let mutable sessionQueues: Map<SessionKey, SerialQueue> = Map.empty
    let mutable providerQueues: Map<ProviderKey, SerialQueue> = Map.empty

    let lockObj = obj ()

    let getOrCreateSessionQueue (key: SessionKey) : SerialQueue =
        match Map.tryFind key sessionQueues with
        | Some q -> q
        | None ->
            let q = SerialQueue()
            sessionQueues <- Map.add key q sessionQueues
            q

    let getOrCreateProviderQueue (key: ProviderKey) : SerialQueue =
        match Map.tryFind key providerQueues with
        | Some q -> q
        | None ->
            let q = SerialQueue()
            providerQueues <- Map.add key q providerQueues
            q

    /// Enqueue a dispatch for the given key. The dispatch function will run
    /// only when the rate-limit window allows. Returns CancelledBeforeDispatch
    /// if stillValid returns false before the actual dispatch.
    member _.RunWhenAllowed
        (key: RetryModelKey, stillValid: unit -> bool, dispatch: unit -> JS.Promise<unit>)
        : JS.Promise<RetryDispatchResult> =
        promise {
            let sessionKey =
                { Workspace = key.Workspace
                  SessionID = key.SessionID }

            let providerKey =
                { Workspace = key.Workspace
                  ProviderID = key.ProviderID
                  ModelID = key.ModelID
                  Variant = key.Variant }

            let sessionQueue =
                lock lockObj (fun () ->
                    lastSessionUseAt <- Map.add sessionKey (clock.GetMonotonicTimeMs()) lastSessionUseAt
                    getOrCreateSessionQueue sessionKey)

            // Outer layer: Session serialization queue (scoped to workspace + session ID)
            return!
                sessionQueue.Enqueue(fun () ->
                    promise {
                        // Check validity immediately after entering the session queue
                        if not (stillValid ()) then
                            return CancelledBeforeDispatch
                        else
                            let providerQueue = lock lockObj (fun () -> getOrCreateProviderQueue providerKey)

                            // Inner layer: Provider rate limiting queue (scoped to workspace + provider + model)
                            return!
                                providerQueue.Enqueue(fun () ->
                                    promise {
                                        // Check validity immediately after entering the provider queue
                                        if not (stillValid ()) then
                                            return CancelledBeforeDispatch
                                        else
                                            let now = clock.GetMonotonicTimeMs()

                                            let lastDispatch =
                                                lock lockObj (fun () ->
                                                    Map.tryFind providerKey lastActualDispatchAt
                                                    |> Option.defaultValue 0.0)

                                            let elapsed = now - lastDispatch
                                            let delay = max 0.0 (float rateLimitMs - elapsed)

                                            if delay > 0.0 then
                                                do! sleeper.Sleep(int delay)

                                            // Check validity again after sleeping/waiting
                                            if not (stillValid ()) then
                                                return CancelledBeforeDispatch
                                            else
                                                let actualNow = clock.GetMonotonicTimeMs()

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
        let now = clock.GetMonotonicTimeMs()
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
