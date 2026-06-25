module VibeFs.Shell.SessionExecutor

open Fable.Core
open VibeFs.Shell.PromiseQueue

let mutable queues : Map<string, SerialQueue> = Map.empty

let mutable private activeRuns : Map<string, unit -> unit> = Map.empty

let registerActiveRun (sessionId: string) (kill: unit -> unit) : unit =
    if sessionId <> "" then activeRuns <- Map.add sessionId kill activeRuns

let unregisterActiveRun (sessionId: string) : unit =
    if sessionId <> "" then activeRuns <- Map.remove sessionId activeRuns

let hasActiveExecutorRun (sessionId: string) : bool =
    sessionId <> "" && Map.containsKey sessionId activeRuns

let abortExecutorRun (sessionId: string) : unit =
    if sessionId = "" then ()
    else
        match Map.tryFind sessionId activeRuns with
        | None -> ()
        | Some kill ->
            try kill () with _ -> ()
            unregisterActiveRun sessionId

let resetSessionExecutorForTesting () : unit =
    activeRuns <- Map.empty

let enqueuePerSession (sessionId: string) (work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
    let queue =
        match Map.tryFind sessionId queues with
        | Some q -> q
        | None ->
            let q = SerialQueue()
            queues <- Map.add sessionId q queues
            q
    queue.Enqueue(fun () ->
        promise {
            try
                let! result = work ()
                return result
            finally
                unregisterActiveRun sessionId
        })