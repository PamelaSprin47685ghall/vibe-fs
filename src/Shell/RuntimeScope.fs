module VibeFs.Shell.RuntimeScope

open Fable.Core
open VibeFs.Kernel.CapsFormat
open VibeFs.Shell.SessionProjectionStore
open VibeFs.Shell.FuzzyIteratorStore
open VibeFs.Shell.PromiseQueue

type RuntimeScope() =
    let projection = ProjectionStore()
    let mutable capsFiles = Map.empty<string, CapsFile list>
    let iteratorStore = createTypedIteratorStore 200
    let mutable sessionQueues = Map.empty<string, SerialQueue>

    member _.Projection = projection

    member _.IteratorStore = iteratorStore

    member _.TryGetCapsFiles(key: string) : CapsFile list option =
        Map.tryFind key capsFiles

    member _.AddCapsFilesIfAbsent(key: string, files: CapsFile list) : unit =
        if not (Map.containsKey key capsFiles) then
            capsFiles <- Map.add key files capsFiles

    member _.ClearCapsFiles() : unit =
        capsFiles <- Map.empty

    member _.ClearIterators() : unit =
        clearTypedIteratorStore iteratorStore

    member _.ClearSessionQueues() : unit =
        sessionQueues <- Map.empty

    member _.EnqueuePerSession(sessionId: string, work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        let queue =
            match Map.tryFind sessionId sessionQueues with
            | Some q -> q
            | None ->
                let q = SerialQueue()
                sessionQueues <- Map.add sessionId q sessionQueues
                q
        queue.Enqueue(work)

let create () : RuntimeScope = RuntimeScope()

let mutable private defaultScope = create ()

let getDefault () : RuntimeScope = defaultScope

let resetDefaultForTesting () : unit =
    defaultScope <- create ()
    defaultScope.ClearIterators()
    defaultScope.ClearSessionQueues()