module Wanxiangshu.Runtime.RuntimeScope

open Fable.Core
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.SessionProjectionStore
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.SubagentIteratorStore
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Workspace

[<Emit("performance.now()")>]
let private now () : float = jsNative

type InitState =
    | Uninitialized
    | Initializing of operationId: string * deadline: float * promise: JS.Promise<unit>
    | Ready
    | Degraded of error: string

type RuntimeScope() =
    let projection = ProjectionStore()
    let mutable initState = Uninitialized
    let mutable onInit: (string -> JS.Promise<unit>) option = None
    let mutable randomGen: unit -> float = fun () -> JS.Math.random ()

    let capsCache = CapsCache()
    let tempFileRegistry = TempFileRegistry()
    let sessionLockRegistry = SessionLockRegistry()

    let iteratorStore = createTypedIteratorStore 200
    let subagentIteratorStore = createSubagentIteratorStore 50
    let mutable extState = Map.empty<string, obj>
    let mutable childSessionCounter = 0
    let mutable workspaceRoot = ""

    member _.RandomGen
        with get () = randomGen
        and set (v) = randomGen <- v

    member _.DegradedReason =
        match initState with
        | Degraded err -> Some err
        | _ -> None

    member _.IsDegraded =
        match initState with
        | Degraded _ -> true
        | _ -> false

    member _.NextChildSessionId() : int =
        childSessionCounter <- childSessionCounter + 1
        childSessionCounter

    member _.Projection = projection
    member _.IteratorStore = iteratorStore
    member _.SubagentIteratorStore = subagentIteratorStore

    member _.RegisterTempFiles(prompt: string, files: string list) : unit =
        tempFileRegistry.Register(prompt, files)

    member _.TryGetTempFiles(prompt: string) : string list option = tempFileRegistry.TryGet(prompt)

    member _.ClearTempFilesForPrompt(prompt: string) : unit = tempFileRegistry.ClearForPrompt(prompt)

    member _.TryRemoveTempFilesForPrompt(prompt: string) : bool =
        tempFileRegistry.TryRemoveForPrompt(prompt)

    member _.TryGetCapsFiles(key: string) : CapsFile list option = capsCache.TryGetCapsFiles(key)

    member _.AddCapsFilesIfAbsent(key: string, files: CapsFile list) : unit =
        capsCache.AddCapsFilesIfAbsent(key, files)

    member _.ClearCapsFiles() : unit = capsCache.Clear()

    member _.ClearCapsFilesForSession(prefix: string) : unit = capsCache.ClearForSession(prefix)

    member _.ClearCapsInflightForSession(prefix: string) : unit =
        capsCache.ClearInflightForSession(prefix)

    member _.GetOrLoadCapsInflight(key: string, load: unit -> JS.Promise<CapsFile list>) : JS.Promise<CapsFile list> =
        capsCache.GetOrLoadInflight(key, load)

    member _.ClearIterators() : unit =
        clearTypedIteratorStore iteratorStore
        clearSubagentIteratorStore subagentIteratorStore

    member _.ClearSessionQueues() : unit = sessionLockRegistry.Clear()

    member _.RemoveSessionQueue(sessionId: string) : unit = sessionLockRegistry.Remove(sessionId)

    member _.RemoveTempFiles(sessionId: string) : unit =
        tempFileRegistry.RemoveSession(sessionId)

    member _.EnqueuePerSession(sessionId: string, work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        let lock = sessionLockRegistry.GetOrCreate(sessionId)
        lock.EnqueueWrite(work)

    member _.EnqueueExecutor(sessionId: string, mode: string, work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        let lock = sessionLockRegistry.GetOrCreate(sessionId)

        if mode = "ro" then
            lock.EnqueueRead(work)
        else
            lock.EnqueueWrite(work)

    member _.TryFindKey(key: string) : obj option = Map.tryFind key extState
    member _.Add(key: string, value: obj) : unit = extState <- Map.add key value extState
    member _.Remove(key: string) : unit = extState <- Map.remove key extState

    member _.InitState
        with get () = initState
        and set (v) = initState <- v

    member _.OnInit
        with get () = onInit
        and set (v) = onInit <- v

    member _.WorkspaceRoot
        with get () = workspaceRoot
        and set (v) = workspaceRoot <- v

    member _.SessionLockCount = sessionLockRegistry.SessionLockCount
    member _.TempFileMapCount = tempFileRegistry.TempFileMapCount
    member _.CapsFileCount = capsCache.CapsFileCount
    member _.CapsInflightCount = capsCache.CapsInflightCount

    member this.TriggerInit(workspaceRootStr: string) : unit =
        if workspaceRootStr <> "" then
            this.WorkspaceRoot <- workspaceRootStr

        if workspaceRootStr <> "" then
            match this.InitState with
            | Uninitialized
            | Degraded _ ->
                match this.OnInit with
                | Some f ->
                    let opId = System.Guid.NewGuid().ToString()
                    let deadline = now () + 10000.0
                    let initP = f workspaceRootStr
                    this.InitState <- Initializing(opId, deadline, initP)
                | None -> ()
            | _ -> ()

    member this.WaitInit() : JS.Promise<unit> =
        match this.InitState with
        | Initializing(opId, dl, p) ->
            if now () > dl then
                this.InitState <- Degraded "InitializationTimeout"
                Promise.reject (exn "InitializationTimeout: watchdog triggered")
            else
                promise {
                    try
                        let! res = PromiseQueue.withTimeout 10000 p

                        match res with
                        | None ->
                            match this.InitState with
                            | Initializing(currentOpId, _, _) when currentOpId = opId ->
                                this.InitState <- Degraded "InitializationTimeout"
                            | _ -> ()
                        | Some _ ->
                            match this.InitState with
                            | Initializing(currentOpId, _, _) when currentOpId = opId -> this.InitState <- Ready
                            | _ -> ()
                    with ex ->
                        match this.InitState with
                        | Initializing(currentOpId, _, _) when currentOpId = opId ->
                            this.InitState <- Degraded ex.Message
                        | _ -> ()
                }
        | Ready -> Promise.lift ()
        | Degraded _ -> Promise.lift ()
        | Uninitialized -> Promise.lift ()

let create () : RuntimeScope = RuntimeScope()
