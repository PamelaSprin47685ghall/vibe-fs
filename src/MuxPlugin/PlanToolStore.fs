module VibeFs.MuxPlugin.PlanToolStore

open Fable.Core
open Fable.Core.JsInterop
open System.Collections.Generic

/// A pending plan tool call: a resolver, rejecter plus a timestamp for best-effort cleanup.
type private PendingCall =
    { resolve: obj -> unit
      reject: exn -> unit
      createdAt: int64 }

let private pendingCalls = Dictionary<string, PendingCall>()
let private ttlMs = 600000L

let private nowMs () = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

let private cleanupOld () =
    let cutoff = nowMs () - ttlMs
    let keys = pendingCalls.Keys |> Seq.filter (fun k -> pendingCalls.[k].createdAt < cutoff) |> Seq.toArray
    for k in keys do
        try pendingCalls.[k].reject (System.TimeoutException("Plan tool call expired"))
        with _ -> ()
        pendingCalls.Remove(k) |> ignore

/// Register a call key and return a promise that resolves when the matching
/// plan tool is invoked, or rejects after timeoutMs.
let registerCallWithTimeout (callId: string) (timeoutMs: int64) : JS.Promise<obj> =
    async {
        cleanupOld ()
        let! result =
            Async.FromContinuations (fun (cont, econt, _) ->
                let entry =
                    { resolve = cont
                      reject = econt
                      createdAt = nowMs () }
                pendingCalls.[callId] <- entry
                JS.setTimeout (fun () ->
                    match pendingCalls.TryGetValue(callId) with
                    | true, pending ->
                        pending.reject (System.TimeoutException($"Plan tool call {callId} timed out"))
                        pendingCalls.Remove(callId) |> ignore
                    | _ -> ()) (int timeoutMs) |> ignore)
        pendingCalls.Remove(callId) |> ignore
        return result
    }
    |> Async.StartAsPromise

/// Register a call key and return a promise that resolves when the matching
/// plan tool is invoked, using the default TTL as timeout.
let registerCall (callId: string) : JS.Promise<obj> = registerCallWithTimeout callId ttlMs

let hasCall (callId: string) : bool =
    pendingCalls.ContainsKey(callId)

/// Resolve a previously registered call with the supplied tool arguments.
let resolveCall (callId: string) (arguments: obj) : bool =
    match pendingCalls.TryGetValue(callId) with
    | true, pending ->
        pending.resolve arguments
        true
    | false, _ -> false
