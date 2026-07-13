module Wanxiangshu.Shell.EventLogFiles

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.EventLog.Fold
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Shell.EventLogCodec
open Wanxiangshu.Shell.EventLogIo
open Wanxiangshu.Shell.EventLogSquadProjection
open Wanxiangshu.Shell.Wanxiangzhen.SquadEventWanCodec
open Wanxiangshu.Shell.Clock
open Wanxiangshu.Shell.PromiseQueue

let lockFileName = ".wanxiangshu.ndjson.lock"

type EventLogStore(workspaceRoot: string, ?appendLineOverride: string -> WanEvent -> JS.Promise<unit>) =
    let queue = SerialQueue()
    let eventFilePath = eventPath workspaceRoot
    let appendLineFn = defaultArg appendLineOverride appendLine
    let mutable sessionStates: Map<string, SessionState> = Map.empty
    let mutable squadProj = emptyProjection ()
    let mutable latestSessionId: string option = None
    let mutable initDone = false
    let mutable initPromise: JS.Promise<unit> option = None
    let mutable readCalled = false
    let mutable revision = 0
    let mutable eventCountRead = 0
    let mutable lastKnownSize = 0L
    let mutable lastReadByteOffset = 0L
    let mutable partialLineBuffer = ""

    let foldWan (e: WanEvent) =
        let sId = e.Session

        let oldState =
            match Map.tryFind sId sessionStates with
            | Some st -> st
            | None -> emptySessionState ()

        sessionStates <- Map.add sId (applyEvent oldState e) sessionStates
        squadProj <- applyWanEvent squadProj e
        revision <- revision + 1

        if isSquadEventKind e.Kind then
            latestSessionId <- Some e.Session

    let ensureInitializedInternal () : JS.Promise<unit> =
        promise {
            if initDone || readCalled then
                return ()
            else
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
                                    foldWan e

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
                    sessionStates <- Map.empty
                    squadProj <- emptyProjection ()
                    latestSessionId <- None
                    revision <- 0
                    eventCountRead <- 0

                    let! events = readEventsFile eventFilePath

                    for e in events do
                        foldWan e

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

                    let mutable stop = false

                    for line in completeLines do
                        if not stop then
                            if line.Trim() <> "" then
                                match tryParseEventLine line with
                                | Some e ->
                                    foldWan e
                                    eventCountRead <- eventCountRead + 1
                                | None -> stop <- true

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

    member _.GetRevision() : int = revision

    member _.GetSessionStateSync(sessionId: string) : SessionState =
        match Map.tryFind sessionId sessionStates with
        | Some st -> st
        | None -> emptySessionState ()

    member this.GetSessionState(sessionId: string) : JS.Promise<SessionState> =
        promise {
            do! ensureSynced ()
            return this.GetSessionStateSync(sessionId)
        }

    member _.GetAllSessionStates() : JS.Promise<Map<string, SessionState>> =
        promise {
            do! ensureSynced ()
            return sessionStates
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

                    foldWan e
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

                foldWan e
                eventCountRead <- eventCountRead + 1
            })

    member _.GetSquadDag(sessionId: string) : JS.Promise<Dag> =
        promise {
            do! ensureSynced ()
            return getDag squadProj sessionId
        }

    member _.GetLatestSquadSessionId() : JS.Promise<string option> =
        promise {
            do! ensureSynced ()
            return latestSessionId
        }

    member _.GetSquadSessions() : JS.Promise<Map<string, Dag>> =
        promise {
            do! ensureSynced ()
            return squadProj.Dags
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

                            let oldState =
                                match Map.tryFind sessionId sessionStates with
                                | Some st -> st
                                | None -> emptySessionState ()

                            let snap = oldState.NudgeSnapshot
                            let currentAnchor = nudgeAnchorKey snap.turnId snap.lastAssistantText

                            let currentHumanTurnId =
                                oldState.LatestHumanTurn
                                |> Option.map (fun t -> t.TurnId)
                                |> Option.defaultValue ""

                            let canClaim =
                                currentAnchor.Trim() = trimmedAnchor
                                && not (isBlocked oldState.NudgeDedup trimmedAnchor)
                                && oldState.SessionGeneration = sessionGen
                                && oldState.CancelGeneration = cancelGen
                                && currentHumanTurnId = humanTurnId
                                && oldState.SessionOwner <> Some "Fallback"
                                && oldState.SessionOwner <> Some "Compaction"
                                && oldState.SessionOwner <> Some "Nudge"
                                && oldState.PendingLease.IsNone
                                && oldState.PendingNudgeLease.IsNone
                                && oldState.NudgeStage <> Requested
                                && oldState.NudgeStage <> Dispatched
                                && oldState.NudgeOrdinal < nudgeOrdinal

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
                                foldWan ev
                                eventCountRead <- eventCountRead + 1
                                claimed <- true
                        })

                return claimed
            })
