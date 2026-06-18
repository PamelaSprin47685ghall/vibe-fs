module VibeFs.Mux.CallStore

open Fable.Core
open Fable.Core.JsInterop
open System.Collections.Generic
open VibeFs.Kernel

type PendingCall =
    { resolve: obj -> unit
      reject: exn -> unit
      createdAt: int64 }

type CallStore private (pendingCalls: Dictionary<string, PendingCall>) =
    member internal _.PendingCalls = pendingCalls

    static member Create() =
        CallStore(Dictionary<string, PendingCall>())

let createCallStore () = CallStore.Create()

let private ttlMs = 600000L

let private nowMs () = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

let private cleanupOld (store: CallStore) =
    let cutoff = nowMs () - ttlMs
    let keys = store.PendingCalls.Keys |> Seq.filter (fun k -> store.PendingCalls.[k].createdAt < cutoff) |> Seq.toArray
    for k in keys do
        try store.PendingCalls.[k].reject (System.TimeoutException("Call expired"))
        with _ -> ()
        store.PendingCalls.Remove(k) |> ignore

let registerCallWithTimeout (store: CallStore) (callId: string) (timeoutMs: int64) : JS.Promise<obj> =
    async {
        cleanupOld store
        let! result =
            Async.FromContinuations (fun (cont, econt, _) ->
                let entry =
                    { resolve = cont
                      reject = econt
                      createdAt = nowMs () }
                store.PendingCalls.[callId] <- entry
                JS.setTimeout (fun () ->
                    match store.PendingCalls.TryGetValue(callId) with
                    | true, pending ->
                        pending.reject (System.TimeoutException($"Call {callId} timed out"))
                        store.PendingCalls.Remove(callId) |> ignore
                    | _ -> ()) (int timeoutMs) |> ignore)
        store.PendingCalls.Remove(callId) |> ignore
        return result
    }
    |> Async.StartAsPromise

let registerCall (store: CallStore) (callId: string) : JS.Promise<obj> = registerCallWithTimeout store callId ttlMs

let hasCall (store: CallStore) (callId: string) : bool =
    store.PendingCalls.ContainsKey(callId)

let resolveCall (store: CallStore) (callId: string) (arguments: obj) : bool =
    match store.PendingCalls.TryGetValue(callId) with
    | true, pending ->
        pending.resolve arguments
        true
    | false, _ -> false
