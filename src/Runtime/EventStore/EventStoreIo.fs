module Wanxiangshu.Runtime.EventStoreIo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventLogIo
open Wanxiangshu.Runtime.ProjectionCache

type internal EventStoreState(eventFilePath: string) =
    let cache = ProjectionCache()
    let mutable initDone = false
    let mutable initPromise: JS.Promise<unit> option = None
    let mutable readCalled = false
    let mutable eventCountRead = 0
    let mutable lastKnownSize = 0L
    let mutable lastReadByteOffset = 0L
    let mutable partialLineBuffer = ""

    member _.Cache = cache

    member this.LastKnownSize
        with get () = lastKnownSize
        and set (v) = lastKnownSize <- v

    member this.LastReadByteOffset
        with get () = lastReadByteOffset
        and set (v) = lastReadByteOffset <- v

    member this.EventCountRead
        with get () = eventCountRead
        and set (v) = eventCountRead <- v

    member private this.ProcessLines(lines: string[]) =
        let mutable stop = false

        for line in lines do
            if not stop && line.Trim() <> "" then
                match tryParseEventLine line with
                | Some e ->
                    cache.FoldWan e
                    this.EventCountRead <- this.EventCountRead + 1
                | None -> stop <- true

    member this.EnsureInitializedInternal() : JS.Promise<unit> =
        promise {
            if not initDone && not readCalled then
                let! exists = fileExists eventFilePath

                if not exists then
                    initDone <- true
                else
                    readCalled <- true

                    do!
                        withWorkspaceLock eventFilePath (fun () ->
                            promise {
                                let! events = readEventsFile eventFilePath

                                for e in events do
                                    cache.FoldWan e

                                initDone <- true
                                eventCountRead <- events.Length
                                let! stats = statAsync eventFilePath
                                lastKnownSize <- this.SizeOf stats
                                lastReadByteOffset <- lastKnownSize
                                partialLineBuffer <- ""
                            })
        }

    member this.EnsureInitialized() : JS.Promise<unit> =
        match initPromise with
        | Some p -> p
        | None ->
            let p = this.EnsureInitializedInternal()
            initPromise <- Some p
            p

    member private _.ClearForMissingFile() : unit =
        cache.Clear()
        eventCountRead <- 0
        lastKnownSize <- 0L
        lastReadByteOffset <- 0L
        partialLineBuffer <- ""

    member _.SizeOf(stats: obj) : int64 =
        int64 (unbox<float> (stats?size))

    member this.SyncNewEvents() : JS.Promise<unit> =
        promise {
            let! stats = statAsync eventFilePath
            let size = this.SizeOf stats

            if size < lastReadByteOffset then
                cache.Clear()
                eventCountRead <- 0
                let! events = readEventsFile eventFilePath

                for e in events do
                    cache.FoldWan e

                let! newStats = statAsync eventFilePath
                lastReadByteOffset <- this.SizeOf newStats
                lastKnownSize <- lastReadByteOffset
                partialLineBuffer <- ""
            elif size > lastReadByteOffset then
                let offset = lastReadByteOffset
                let length = int (size - offset)
                let! newText = readChunkAsync eventFilePath (float offset) length
                let combinedText = partialLineBuffer + newText
                let lines = combinedText.Split('\n')

                let completeLines =
                    if lines.Length > 1 then
                        lines.[0 .. lines.Length - 2]
                    else
                        [||]

                let newPartialLineBuffer =
                    if combinedText.EndsWith("\n") then
                        ""
                    else
                        lines.[lines.Length - 1]

                this.ProcessLines completeLines
                partialLineBuffer <- newPartialLineBuffer
                lastReadByteOffset <- size
                lastKnownSize <- size
        }

    member this.EnsureSynced() : JS.Promise<unit> =
        promise {
            do! this.EnsureInitialized()

            try
                let! stats = statAsync eventFilePath
                let size = this.SizeOf stats

                if size <> lastKnownSize then
                    do! withWorkspaceLock eventFilePath (fun () -> this.SyncNewEvents())
            with ex when isMissingPathError (box ex) ->
                this.ClearForMissingFile()
        }
