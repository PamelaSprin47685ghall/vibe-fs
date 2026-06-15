module VibeFs.MuxPlugin.PlanToolStore

open Fable.Core
open Fable.Core.JsInterop
open System.Collections.Generic

/// A pending plan tool call: a resolver plus a timestamp for best-effort cleanup.
type private PendingCall =
    { resolve: obj -> unit
      createdAt: int }

let private pendingCalls = Dictionary<string, PendingCall>()
let private ttlMs = 600000

let private nowMs () = int (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())

let private cleanupOld () =
    let cutoff = nowMs () - ttlMs
    let keys = pendingCalls.Keys |> Seq.filter (fun k -> pendingCalls.[k].createdAt < cutoff) |> Seq.toArray
    for k in keys do pendingCalls.Remove(k) |> ignore

/// Register a call key and return a promise that resolves when the matching
/// plan tool is invoked.
let registerCall (callId: string) : JS.Promise<obj> =
    async {
        cleanupOld ()
        let! result =
            Async.FromContinuations (fun (cont, _, _) ->
                pendingCalls.[callId] <- { resolve = cont; createdAt = nowMs () })
        pendingCalls.Remove(callId) |> ignore
        return result
    }
    |> Async.StartAsPromise

let hasCall (callId: string) : bool =
    pendingCalls.ContainsKey(callId)

/// Resolve a previously registered call with the supplied tool arguments.
let resolveCall (callId: string) (arguments: obj) : bool =
    match pendingCalls.TryGetValue(callId) with
    | true, pending ->
        pending.resolve arguments
        true
    | false, _ -> false
