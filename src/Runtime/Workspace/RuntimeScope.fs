module Wanxiangshu.Runtime.RuntimeScope

open Fable.Core
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.SessionProjectionStore
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
    let sessionExecutorRegistry = SessionExecutorRegistry()

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
        clearSubagentIteratorStore subagentIteratorStore

    member _.ClearSessionExecutors() : unit = sessionExecutorRegistry.Clear()

    member _.CloseSessionExecutor(sessionId: string) : unit =
        sessionExecutorRegistry.Close(sessionId)

    member _.RemoveTempFiles(sessionId: string) : unit =
        tempFileRegistry.RemoveSession(sessionId)

    member _.EnqueuePerSession(sessionId: string, work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        let executor = sessionExecutorRegistry.GetOrCreate(sessionId)
        executor.Enqueue(work)

    member _.EnqueueExecutor(sessionId: string, work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        let executor = sessionExecutorRegistry.GetOrCreate(sessionId)
        executor.Enqueue(work)

    member _.TryFindKey(key: string) : obj option = Map.tryFind key extState
    member _.Add(key: string, value: obj) : unit = extState <- Map.add key value extState
    member _.Remove(key: string) : unit = extState <- Map.remove key extState

    member _.InitState = initState

    member _.OnInit
        with get () = onInit
        and set (v) =
            match v with
            | None -> onInit <- None
            | Some _ ->
                match onInit with
                | Some _ -> failwith "InvalidOperationException: OnInit can only be assigned once"
                | None -> onInit <- v

    member _.WorkspaceRoot
        with get () = workspaceRoot
        and set (v) = workspaceRoot <- v

    member _.SessionExecutorCount = sessionExecutorRegistry.SessionExecutorCount
    member _.TempFileMapCount = tempFileRegistry.TempFileMapCount
    member _.CapsFileCount = capsCache.CapsFileCount
    member _.CapsInflightCount = capsCache.CapsInflightCount

    member this.TriggerInit(workspaceRootStr: string) : unit =
        if workspaceRootStr <> "" then
            this.WorkspaceRoot <- workspaceRootStr

        if workspaceRootStr <> "" then
            match initState with
            | Uninitialized
            | Degraded _ ->
                match onInit with
                | Some f ->
                    let opId = System.Guid.NewGuid().ToString()
                    let deadline = now () + 10000.0
                    let initP = f workspaceRootStr
                    initState <- Initializing(opId, deadline, initP)
                | None -> ()
            | _ -> ()

    member this.WaitInit() : JS.Promise<unit> =
        match initState with
        | Initializing(opId, dl, p) ->
            let remaining = max 0 (int (dl - now ()))

            if remaining = 0 then
                let msg = "InitializationTimeout: watchdog triggered"
                initState <- Degraded msg
                Promise.reject (exn msg)
            else
                promise {
                    try
                        let! res = PromiseQueue.withTimeout remaining p

                        match res with
                        | None ->
                            match initState with
                            | Initializing(currentOpId, _, _) when currentOpId = opId ->
                                initState <- Degraded "InitializationTimeout"
                            | _ -> ()
                        | Some _ ->
                            match initState with
                            | Initializing(currentOpId, _, _) when currentOpId = opId -> initState <- Ready
                            | _ -> ()
                    with ex ->
                        match initState with
                        | Initializing(currentOpId, _, _) when currentOpId = opId -> initState <- Degraded ex.Message
                        | _ -> ()

                        return raise ex
                }
        | Ready -> Promise.lift ()
        | Degraded _ -> Promise.lift ()
        | Uninitialized -> Promise.lift ()

let create () : RuntimeScope = RuntimeScope()
