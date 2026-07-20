module Wanxiangshu.Runtime.EventStoreIo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.ProjectionCache

type EventStoreStateKind =
    | Uninitialized
    | Initializing of operationId: string * deadlineAt: float
    | Ready of revision: int
    | Repairing of operationId: string * fault: string
    | Degraded of error: string
    | Disposed

type internal EventStoreState(workspaceRoot: string, eventFilePath: string) =
    let cache = ProjectionCache()
    let mutable stateKind = Uninitialized
    let mutable initPromise: JS.Promise<unit> option = None
    let mutable eventCountRead = 0
    let mutable lastKnownSize = 0L
    let mutable lastReadByteOffset = 0L
    let mutable partialLineBuffer = ""

    member _.Cache = cache
    member _.WorkspaceRoot = workspaceRoot
    member _.EventFilePath = eventFilePath

    member this.LastKnownSize
        with get () = lastKnownSize
        and set (v) = lastKnownSize <- v

    member this.LastReadByteOffset
        with get () = lastReadByteOffset
        and set (v) = lastReadByteOffset <- v

    member this.EventCountRead
        with get () = eventCountRead
        and set (v) = eventCountRead <- v

    member this.PartialLineBuffer
        with get () = partialLineBuffer
        and set (v) = partialLineBuffer <- v

    member this.StateKind
        with get () = stateKind
        and set (v) = stateKind <- v

    member this.InitPromise
        with get () = initPromise
        and set (v) = initPromise <- v

    member this.ClearForMissingFile() : unit =
        cache.ClearSessionStatesOnly()
        this.EventCountRead <- 0
        this.LastKnownSize <- 0L
        this.LastReadByteOffset <- 0L
        this.PartialLineBuffer <- ""

    member _.SizeOf(stats: obj) : int64 = int64 (unbox<float> (stats?size))

    member this.Dispose() : unit =
        this.StateKind <- Disposed
        this.InitPromise <- None
