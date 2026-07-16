module Wanxiangshu.Runtime.EventLogFiles

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Kernel.SessionOverview
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventLogIo
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime.EventLogProjectionCache
open Wanxiangshu.Runtime.Wanxiangzhen.SquadEventWanCodec

let lockFileName = ".wanxiangshu.ndjson.lock"

type EventLogStore(workspaceRoot: string, ?appendLineOverride: string -> WanEvent -> JS.Promise<unit>) =
    let queue = SerialQueue()
    let eventFilePath = eventPath workspaceRoot
    let appendLineFn = defaultArg appendLineOverride appendLine
    let cache = ProjectionCache()
    let mutable initDone = false
    let mutable initPromise: JS.Promise<unit> option = None
    let mutable readCalled = false
    let mutable eventCountRead = 0
    let mutable lastKnownSize = 0L
    let mutable lastReadByteOffset = 0L
    let mutable partialLineBuffer = ""

    let processLines (lines: string[]) =
        let mutable stop = false

        for line in lines do
            if not stop && line.Trim() <> "" then
                match tryParseEventLine line with
                | Some e ->
                    cache.FoldWan e
                    eventCountRead <- eventCountRead + 1
                | None -> stop <- true

    let ensureInitializedInternal () : JS.Promise<unit> =
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
                                lastKnownSize <- unbox<int64> (stats?size)
                                lastReadByteOffset <- lastKnownSize
                                partialLineBuffer <- ""
                            })
        }

    let ensureInitialized () : JS.Promise<unit> =
        match initPromise with
        | Some p -> p
        | None ->
            let p = ensureInitializedInternal ()
            initPromise <- Some p
            p

    let syncNewEvents () : JS.Promise<unit> =
        promise {
            try
                let! stats = statAsync eventFilePath
                let size = unbox<int64> (stats?size)

                if size < lastReadByteOffset then
                    cache.Clear()
                    eventCountRead <- 0
                    let! events = readEventsFile eventFilePath

                    for e in events do
                        cache.FoldWan e

                    let! newStats = statAsync eventFilePath
                    lastReadByteOffset <- unbox<int64> (newStats?size)
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

                    processLines completeLines
                    partialLineBuffer <- newPartialLineBuffer
                    lastReadByteOffset <- size
                    lastKnownSize <- size
            with _ ->
                ()
        }

    let ensureSynced () : JS.Promise<unit> =
        promise {
            do! ensureInitialized ()

            try
                let! stats = statAsync eventFilePath
                let size = unbox<int64> (stats?size)

                if size <> lastKnownSize then
                    do! withWorkspaceLock eventFilePath (fun () -> syncNewEvents ())
            with _ ->
                ()
        }

    member _.ReadAllEvents() : JS.Promise<WanEvent list> =
        promise {
            do! ensureSynced ()
            let! events = withWorkspaceLock eventFilePath (fun () -> readEventsFile eventFilePath)
            return events
        }

    member _.GetRevision() : int = cache.Revision

    member _.GetSessionStateSync(sessionId: string) : SessionState = cache.GetSessionStateSync(sessionId)

    member this.GetSessionState(sessionId: string) : JS.Promise<SessionState> =
        promise {
            do! ensureSynced ()
            return this.GetSessionStateSync(sessionId)
        }

    member this.GetSessionOverview(sessionId: string) : SessionOverview =
        let st = this.GetSessionStateSync(sessionId)
        fromSessionState st

    member _.GetAllSessionStates() : JS.Promise<Map<string, SessionState>> =
        promise {
            do! ensureSynced ()
            return cache.GetAllSessionStates()
        }

    member _.EnsureInitialized() : JS.Promise<unit> = ensureInitialized ()

    member _.AppendEvent(e: WanEvent) : JS.Promise<Result<unit, string>> =
        queue.Enqueue(fun () ->
            promise {
                do! ensureInitialized ()

                try
                    do!
                        withWorkspaceLock eventFilePath (fun () ->
                            promise {
                                do! syncNewEvents ()
                                do! appendLineFn eventFilePath e
                                let! stats = statAsync eventFilePath
                                lastKnownSize <- unbox<int64> (stats?size)
                                lastReadByteOffset <- lastKnownSize
                            })

                    cache.FoldWan e
                    eventCountRead <- eventCountRead + 1
                    return Ok()
                with ex ->
                    return Error ex.Message
            })

    member _.AppendEventOrFail(e: WanEvent) : JS.Promise<unit> =
        queue.Enqueue(fun () ->
            promise {
                do! ensureInitialized ()

                do!
                    withWorkspaceLock eventFilePath (fun () ->
                        promise {
                            do! syncNewEvents ()
                            do! appendLineFn eventFilePath e
                            let! stats = statAsync eventFilePath
                            lastKnownSize <- unbox<int64> (stats?size)
                            lastReadByteOffset <- lastKnownSize
                        })

                cache.FoldWan e
                eventCountRead <- eventCountRead + 1
            })

    /// Atomic multi-event append: one lock, one contiguous write of all lines.
    member _.AppendEventsOrFail(events: WanEvent list) : JS.Promise<unit> =
        if List.isEmpty events then
            Promise.lift ()
        else
            queue.Enqueue(fun () ->
                promise {
                    do! ensureInitialized ()

                    do!
                        withWorkspaceLock eventFilePath (fun () ->
                            promise {
                                do! syncNewEvents ()

                                let block =
                                    events |> List.map (fun e -> wanEventToLine e + "\n") |> String.concat ""

                                do! appendFileAsync eventFilePath block
                                let! stats = statAsync eventFilePath
                                lastKnownSize <- unbox<int64> (stats?size)
                                lastReadByteOffset <- lastKnownSize
                            })

                    for e in events do
                        cache.FoldWan e
                        eventCountRead <- eventCountRead + 1
                })

    member _.GetSquadDag(sessionId: string) : JS.Promise<Dag> =
        promise {
            do! ensureSynced ()
            return cache.GetSquadDag(sessionId)
        }

    member _.GetLatestSquadSessionId() : JS.Promise<string option> =
        promise {
            do! ensureSynced ()
            return cache.GetLatestSquadSessionId()
        }

    member _.GetSquadSessions() : JS.Promise<Map<string, Dag>> =
        promise {
            do! ensureSynced ()
            return cache.GetSquadSessions()
        }

    member this.AppendSquadEvent (at: string) (e: SquadEvent) : JS.Promise<Result<unit, string>> =
        this.AppendEvent(squadEventToWanEvent at e)

    member _.TryClaimNudgeDispatch
        (sessionId: string)
        (action: NudgeAction)
        (anchor: string)
        (nudgeId: string)
        (nonce: string)
        (sessionGen: int)
        (cancelGen: int)
        (humanTurnId: string)
        (nudgeOrdinal: int)
        (isBlocked: NudgeDedupState -> string -> bool)
        : JS.Promise<bool> =
        queue.Enqueue(fun () ->
            promise {
                do! ensureInitialized ()
                let trimmedAnchor = anchor.Trim()
                let mutable claimed = false

                do!
                    withWorkspaceLock eventFilePath (fun () ->
                        promise {
                            do! syncNewEvents ()

                            let canClaim =
                                cache.CanClaimNudgeDispatch
                                    sessionId
                                    trimmedAnchor
                                    sessionGen
                                    cancelGen
                                    humanTurnId
                                    nudgeOrdinal
                                    isBlocked

                            if canClaim then
                                let payload =
                                    Map
                                        [ "action", Wanxiangshu.Kernel.Nudge.toString action
                                          "anchor", trimmedAnchor
                                          "nudgeId", nudgeId
                                          "nonce", nonce
                                          "generation", sessionGen.ToString()
                                          "cancelGeneration", cancelGen.ToString()
                                          "humanTurnId", humanTurnId
                                          "nudgeOrdinal", nudgeOrdinal.ToString() ]

                                let ev =
                                    buildEvent sessionId eventKindNudgeRequested payload (getTimestampMs().ToString())

                                do! appendLineFn eventFilePath ev
                                let! stats = statAsync eventFilePath
                                lastKnownSize <- unbox<int64> (stats?size)
                                lastReadByteOffset <- lastKnownSize
                                cache.FoldWan ev
                                eventCountRead <- eventCountRead + 1
                                claimed <- true
                        })

                return claimed
            })
