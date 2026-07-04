module Wanxiangshu.Shell.RuntimeScope

open Fable.Core
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Shell.SessionProjectionStore
open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Shell.PromiseQueue

type RuntimeScope() =
    let projection = ProjectionStore()
    let mutable capsFiles = Map.empty<string, CapsFile list>
    let mutable capsInflight = Map.empty<string, JS.Promise<CapsFile list>>
    let iteratorStore = createTypedIteratorStore 200
    let mutable sessionQueues = Map.empty<string, SerialQueue>
    let mutable extState = Map.empty<string, obj>

    member _.Projection = projection

    member _.IteratorStore = iteratorStore

    member _.TryGetCapsFiles(key: string) : CapsFile list option =
        Map.tryFind key capsFiles

    member _.AddCapsFilesIfAbsent(key: string, files: CapsFile list) : unit =
        if not (Map.containsKey key capsFiles) then
            capsFiles <- Map.add key files capsFiles

    member _.ClearCapsFiles() : unit =
        capsFiles <- Map.empty
        capsInflight <- Map.empty

    member _.GetOrLoadCapsInflight(key: string, load: unit -> JS.Promise<CapsFile list>) : JS.Promise<CapsFile list> =
        match Map.tryFind key capsInflight with
        | Some p -> p
        | None ->
            let p =
                load ()
                |> Promise.map (fun files ->
                    capsInflight <- Map.remove key capsInflight
                    files)
                |> Promise.catch (fun ex ->
                    capsInflight <- Map.remove key capsInflight
                    raise ex)
            capsInflight <- Map.add key p capsInflight
            p

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

    member _.TryFindKey(key: string) : obj option =
        Map.tryFind key extState

    member _.Add(key: string, value: obj) : unit =
        extState <- Map.add key value extState

    member _.Remove(key: string) : unit =
        extState <- Map.remove key extState

let create () : RuntimeScope = RuntimeScope()