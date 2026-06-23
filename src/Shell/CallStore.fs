module VibeFs.Shell.CallStore

open Fable.Core
open VibeFs.Shell.PromiseQueue

type PendingCall =
    { resolve: obj -> unit
      reject: exn -> unit
      createdAt: int64 }

type CallStore internal () =
    let queue = SerialQueue()
    let mutable pending = Map.empty<string, PendingCall>
    let ttlMs = 600000L

    let nowMs () : int64 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

    let cleanupOld () =
        let cutoff = nowMs () - ttlMs
        let expired =
            pending
            |> Map.filter (fun _ call -> call.createdAt < cutoff)
            |> Map.toList
        for (_callId, call) in expired do
            try call.reject (System.TimeoutException("Call expired"))
            with _ -> ()
        pending <- expired |> List.fold (fun acc (callId, _) -> Map.remove callId acc) pending

    member _.RegisterCallWithTimeout(callId: string, timeoutMs: int64) : JS.Promise<obj> =
        let mutable resolveRef = None
        let mutable rejectRef = None
        let result =
            Promise.create (fun resolve reject ->
                resolveRef <- Some resolve
                rejectRef <- Some reject)
        queue.Enqueue(fun () ->
            cleanupOld ()
            match resolveRef, rejectRef with
            | Some resolve, Some reject ->
                pending <- Map.add callId { resolve = resolve; reject = reject; createdAt = nowMs () } pending
                JS.setTimeout (fun () ->
                    queue.Enqueue(fun () ->
                        match Map.tryFind callId pending with
                        | Some call ->
                            call.reject (System.TimeoutException($"Call {callId} timed out"))
                            pending <- Map.remove callId pending
                            Promise.lift ()
                        | None -> Promise.lift ())
                    |> ignore) (int timeoutMs) |> ignore
            | _ -> ()
            Promise.lift ())
        |> ignore
        result

    member this.RegisterCall(callId: string) : JS.Promise<obj> =
        this.RegisterCallWithTimeout(callId, ttlMs)

    member _.ResolveCall(callId: string, arguments: obj) : JS.Promise<bool> =
        queue.Enqueue(fun () ->
            match Map.tryFind callId pending with
            | Some call ->
                call.resolve arguments
                pending <- Map.remove callId pending
                Promise.lift true
            | None -> Promise.lift false)

    member _.HasCall(callId: string) : JS.Promise<bool> =
        queue.Enqueue(fun () -> Promise.lift (Map.containsKey callId pending))

    member _.PendingCallIds() : JS.Promise<string array> =
        queue.Enqueue(fun () -> Promise.lift (pending |> Map.toArray |> Array.map fst))

    member _.ResolveFirstMatching(prefix: string, arguments: obj) : JS.Promise<bool> =
        queue.Enqueue(fun () ->
            match pending |> Map.toSeq |> Seq.tryFind (fun (k, _) -> k.StartsWith(prefix)) with
            | Some(callId, _) ->
                (Map.find callId pending).resolve arguments
                pending <- Map.remove callId pending
                Promise.lift true
            | None -> Promise.lift false)

let createCallStore () = CallStore()

let registerCallWithTimeout (store: CallStore) (callId: string) (timeoutMs: int64) : JS.Promise<obj> =
    store.RegisterCallWithTimeout(callId, timeoutMs)

let registerCall (store: CallStore) (callId: string) : JS.Promise<obj> =
    store.RegisterCall(callId)

let hasCallAsync (store: CallStore) (callId: string) : JS.Promise<bool> =
    store.HasCall(callId)

let resolveCallAsync (store: CallStore) (callId: string) (arguments: obj) : JS.Promise<bool> =
    store.ResolveCall(callId, arguments)

let pendingCallIdsAsync (store: CallStore) : JS.Promise<string array> =
    store.PendingCallIds()

let resolveFirstMatchingAsync (store: CallStore) (prefix: string) (arguments: obj) : JS.Promise<bool> =
    store.ResolveFirstMatching(prefix, arguments)
