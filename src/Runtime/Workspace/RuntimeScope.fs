module Wanxiangshu.Runtime.RuntimeScope

open Fable.Core
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.SessionProjectionStore
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.SubagentIteratorStore
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime

type RuntimeScope() =
    let projection = ProjectionStore()
    let mutable initPromise: JS.Promise<unit> option = None
    let mutable onInit: (string -> JS.Promise<unit>) option = None
    let mutable randomGen: unit -> float = fun () -> JS.Math.random ()
    let mutable capsFiles = Map.empty<string, CapsFile list>
    let mutable capsInflight = Map.empty<string, JS.Promise<CapsFile list>>
    let iteratorStore = createTypedIteratorStore 200
    let subagentIteratorStore = createSubagentIteratorStore 50
    let mutable sessionLocks = Map.empty<string, SessionReaderWriterLock>
    let mutable extState = Map.empty<string, obj>
    let mutable tempFilesByPrompt = Map.empty<string, string list>
    let mutable childSessionCounter = 0
    let mutable workspaceRoot = ""

    member _.RandomGen
        with get () = randomGen
        and set (v) = randomGen <- v

    member _.NextChildSessionId() : int =
        childSessionCounter <- childSessionCounter + 1
        childSessionCounter

    member _.Projection = projection

    member _.IteratorStore = iteratorStore

    member _.SubagentIteratorStore = subagentIteratorStore

    member _.RegisterTempFiles(prompt: string, files: string list) : unit =
        let key = if isNull prompt then "" else prompt.Trim()

        if key <> "" then
            tempFilesByPrompt <- Map.add key files tempFilesByPrompt

    member _.TryGetTempFiles(prompt: string) : string list option =
        let key = if isNull prompt then "" else prompt.Trim()
        if key = "" then None else Map.tryFind key tempFilesByPrompt

    member _.ClearTempFilesForPrompt(prompt: string) : unit =
        let key = if isNull prompt then "" else prompt.Trim()

        if key <> "" then
            tempFilesByPrompt <- Map.remove key tempFilesByPrompt

    member _.TryRemoveTempFilesForPrompt(prompt: string) : bool =
        let key = if isNull prompt then "" else prompt.Trim()

        if key = "" then
            false
        else
            let existed = Map.containsKey key tempFilesByPrompt
            tempFilesByPrompt <- Map.remove key tempFilesByPrompt
            existed

    member _.TryGetCapsFiles(key: string) : CapsFile list option = Map.tryFind key capsFiles

    member _.AddCapsFilesIfAbsent(key: string, files: CapsFile list) : unit =
        if not (Map.containsKey key capsFiles) then
            capsFiles <- Map.add key files capsFiles

    member _.ClearCapsFiles() : unit =
        capsFiles <- Map.empty
        capsInflight <- Map.empty

    member _.ClearCapsFilesForSession(prefix: string) : unit =
        capsFiles <- capsFiles |> Map.filter (fun k _ -> not (k.StartsWith prefix))

    member _.ClearCapsInflightForSession(prefix: string) : unit =
        capsInflight <- capsInflight |> Map.filter (fun k _ -> not (k.StartsWith prefix))

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
        clearSubagentIteratorStore subagentIteratorStore

    member _.ClearSessionQueues() : unit = sessionLocks <- Map.empty

    member _.RemoveSessionQueue(sessionId: string) : unit =
        sessionLocks <- Map.remove sessionId sessionLocks

    member _.RemoveTempFiles(sessionId: string) : unit =
        tempFilesByPrompt <-
            tempFilesByPrompt
            |> Map.filter (fun k _ -> not (k.StartsWith(sessionId + "\u0000")))

    member _.EnqueuePerSession(sessionId: string, work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        let lock =
            match Map.tryFind sessionId sessionLocks with
            | Some l -> l
            | None ->
                let l = SessionReaderWriterLock()
                sessionLocks <- Map.add sessionId l sessionLocks
                l

        lock.EnqueueWrite(work)

    member _.EnqueueExecutor(sessionId: string, mode: string, work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        let lock =
            match Map.tryFind sessionId sessionLocks with
            | Some l -> l
            | None ->
                let l = SessionReaderWriterLock()
                sessionLocks <- Map.add sessionId l sessionLocks
                l

        if mode = "ro" then
            lock.EnqueueRead(work)
        else
            lock.EnqueueWrite(work)

    member _.TryFindKey(key: string) : obj option = Map.tryFind key extState

    member _.Add(key: string, value: obj) : unit = extState <- Map.add key value extState

    member _.Remove(key: string) : unit = extState <- Map.remove key extState

    member _.InitPromise
        with get () = initPromise
        and set (v) = initPromise <- v

    member _.OnInit
        with get () = onInit
        and set (v) = onInit <- v

    member _.WorkspaceRoot
        with get () = workspaceRoot
        and set (v) = workspaceRoot <- v

    member _.TriggerInit(workspaceRootStr: string) : unit =
        if workspaceRootStr <> "" then
            workspaceRoot <- workspaceRootStr

        if workspaceRootStr <> "" && Option.isNone initPromise then
            match onInit with
            | Some f ->
                let initP = f workspaceRootStr
                initPromise <- Some initP
            | None -> ()

    member _.SessionLockCount = Map.count sessionLocks

    member _.TempFileMapCount = Map.count tempFilesByPrompt

    member _.CapsFileCount = Map.count capsFiles

    member _.CapsInflightCount = Map.count capsInflight

    member _.WaitInit() : JS.Promise<unit> =
        match initPromise with
        | Some p -> p
        | None -> Promise.lift ()

let create () : RuntimeScope = RuntimeScope()
