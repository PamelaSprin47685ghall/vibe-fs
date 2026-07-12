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
    let mutable revision = 0

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
            let! events = readEventsFile eventFilePath
            return events
        }

    member _.GetRevision() : int = revision

    member _.GetSessionStateSync(sessionId: string) : SessionState =
        match Map.tryFind sessionId sessionStates with
        | Some st -> st
        | None -> emptySessionState ()

    member this.GetSessionState(sessionId: string) : JS.Promise<SessionState> =
        promise {
            do! ensureInitialized ()
            return this.GetSessionStateSync(sessionId)
        }

    member _.GetAllSessionStates() : JS.Promise<Map<string, SessionState>> =
        promise {
            do! ensureInitialized ()
            return sessionStates
        }

    member _.EnsureInitialized() : JS.Promise<unit> = ensureInitialized ()

    member _.AppendEvent(e: WanEvent) : JS.Promise<Result<unit, string>> =
        queue.Enqueue(fun () ->
            promise {
                do! ensureInitialized ()

                try
                    do! withWorkspaceLock eventFilePath (fun () -> appendLineFn eventFilePath e)
                    foldWan e
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
        (nudgeId: string)
        (nonce: string)
        (sessionGen: int)
        (cancelGen: int)
        (humanTurnId: string)
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
                elif oldState.SessionGeneration <> sessionGen then
                    return false
                elif oldState.CancelGeneration <> cancelGen then
                    return false
                elif
                    (oldState.LatestHumanTurn
                     |> Option.map (fun t -> t.TurnId)
                     |> Option.defaultValue "")
                    <> humanTurnId
                then
                    return false
                elif
                    oldState.SessionOwner = Some "Fallback"
                    || oldState.SessionOwner = Some "Compaction"
                then
                    return false
                elif oldState.PendingLease.IsSome then
                    return false
                else
                    let payload =
                        Map
                            [ "action", Wanxiangshu.Kernel.Nudge.toString action
                              "anchor", trimmedAnchor
                              "nudgeId", nudgeId
                              "nonce", nonce
                              "generation", sessionGen.ToString()
                              "cancelGeneration", cancelGen.ToString()
                              "humanTurnId", humanTurnId ]

                    let ev =
                        buildEvent sessionId eventKindNudgeRequested payload (getTimestampMs().ToString())

                    do! withWorkspaceLock eventFilePath (fun () -> appendLineFn eventFilePath ev)
                    foldWan ev
                    return true
            })
