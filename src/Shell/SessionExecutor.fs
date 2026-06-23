module VibeFs.Shell.SessionExecutor

open Fable.Core
open VibeFs.Shell.PromiseQueue

let mutable queues : Map<string, SerialQueue> = Map.empty

let enqueuePerSession (sessionId: string) (work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
    let queue =
        match Map.tryFind sessionId queues with
        | Some q -> q
        | None ->
            let q = SerialQueue()
            queues <- Map.add sessionId q queues
            q
    queue.Enqueue(work)
