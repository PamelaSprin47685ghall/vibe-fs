module Wanxiangshu.Shell.EventLogFiles

open Fable.Core
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
    let readAllResult = ResizeArray<WanEvent>()

    let foldWan (e: WanEvent) =
        let sId = e.Session

        let oldState =
            match Map.tryFind sId sessionStates with
            | Some st -> st
            | None -> emptySessionState ()

        sessionStates <- Map.add sId (applyEvent oldState e) sessionStates
        squadProj <- applyWanEvent squadProj e

        if isSquadEventKind e.Kind then
            latestSessionId <- Some e.Session

    let ensureInitializedInternal () : JS.Promise<unit> =
        promise {
            if initDone then
                return ()
            else
                let! exists = fileExists eventFilePath

                if not exists then
                    initDone <- true
                else
                    if readCalled then
                        failwith
                            "ReadAllEvents / ensureInitialized is restricted to be called at most once during the store lifecycle!"

                    readCalled <- true

                    do!
                        withWorkspaceLock eventFilePath (fun () ->
                            promise {
                                let! events = readEventsFile eventFilePath
                                readAllResult.AddRange(events)

                                for e in events do
                                    foldWan e

                                initDone <- true
                            })
        }

    let ensureInitialized () : JS.Promise<unit> =
        match initPromise with
        | Some p -> p
        | None ->
            let p = ensureInitializedInternal ()
            initPromise <- Some p
            p

    member _.ReadAllEvents() : JS.Promise<WanEvent list> =
        promise {
            do! ensureInitialized ()
            return Seq.toList readAllResult
        }

    member _.GetSessionState(sessionId: string) : JS.Promise<SessionState> =
        promise {
            do! ensureInitialized ()

            return
                match Map.tryFind sessionId sessionStates with
                | Some st -> st
                | None -> emptySessionState ()
        }

    member _.GetAllSessionStates() : JS.Promise<Map<string, SessionState>> =
        promise {
            do! ensureInitialized ()
            return sessionStates
        }

    member _.EnsureInitialized() : JS.Promise<unit> = ensureInitializedInternal ()

    member _.AppendEvent(e: WanEvent) : JS.Promise<Result<unit, string>> =
        queue.Enqueue(fun () ->
            promise {
                do! ensureInitialized ()

                try
                    do! withWorkspaceLock eventFilePath (fun () -> appendLineFn eventFilePath e)
                    foldWan e
                    readAllResult.Add(e) |> ignore
                    return Ok()
                with ex ->
                    return Error ex.Message
            })

    member _.AppendEventOrFail(e: WanEvent) : JS.Promise<unit> =
        queue.Enqueue(fun () ->
            promise {
                do! ensureInitialized ()
                do! withWorkspaceLock eventFilePath (fun () -> appendLineFn eventFilePath e)
                foldWan e
                readAllResult.Add(e) |> ignore
            })

    member _.GetSquadDag(sessionId: string) : JS.Promise<Dag> =
        promise {
            do! ensureInitialized ()
            return getDag squadProj sessionId
        }

    member _.GetLatestSquadSessionId() : JS.Promise<string option> =
        promise {
            do! ensureInitialized ()
            return latestSessionId
        }

    member _.GetSquadSessions() : JS.Promise<Map<string, Dag>> =
        promise {
            do! ensureInitialized ()
            return squadProj.Dags
        }

    member this.AppendSquadEvent (at: string) (e: SquadEvent) : JS.Promise<Result<unit, string>> =
        this.AppendEvent(squadEventToWanEvent at e)

    member _.TryClaimNudgeDispatch
        (sessionId: string)
        (action: NudgeAction)
        (anchor: string)
        (isBlocked: NudgeDedupState -> string -> bool)
        : JS.Promise<bool> =
        queue.Enqueue(fun () ->
            promise {
                do! ensureInitialized ()
                let trimmedAnchor = anchor.Trim()

                let oldState =
                    match Map.tryFind sessionId sessionStates with
                    | Some st -> st
                    | None -> emptySessionState ()

                let snap = oldState.NudgeSnapshot
                let currentAnchor = nudgeAnchorKey snap.turnId snap.lastAssistantText

                if currentAnchor.Trim() <> trimmedAnchor then
                    return false
                elif isBlocked oldState.NudgeDedup trimmedAnchor then
                    return false
                else
                    let payload =
                        Map [ "action", Wanxiangshu.Kernel.Nudge.toString action; "anchor", trimmedAnchor ]

                    let ev =
                        buildEvent sessionId eventKindNudgeDispatched payload (getTimestampMs().ToString())

                    do! withWorkspaceLock eventFilePath (fun () -> appendLineFn eventFilePath ev)
                    foldWan ev
                    readAllResult.Add(ev) |> ignore
                    return true
            })
