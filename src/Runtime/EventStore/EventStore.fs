module Wanxiangshu.Runtime.EventStore

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.SessionOverview
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventLogFile
open Wanxiangshu.Runtime.EventLogIo
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime.NudgeDispatchClaim
open Wanxiangshu.Runtime.EventStoreIo

type EventLogStore(workspaceRoot: string, ?appendLineOverride: string -> WanEvent -> JS.Promise<unit>) =
    let queue = SerialQueue()
    let eventFilePath = eventPath workspaceRoot
    let writesDisabled = workspaceRoot = ""
    let appendLineFn = defaultArg appendLineOverride appendLine
    let state = EventStoreState(eventFilePath)

    let enqueueWrite (noopValue: 'T) (work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        if writesDisabled then
            Promise.lift noopValue
        else
            queue.Enqueue work

    member this.EnsureSynced() : JS.Promise<unit> = state.EnsureSynced()
    member _.ProjectionCache = state.Cache

    member _.ReadAllEvents() : JS.Promise<WanEvent list> =
        promise {
            do! state.EnsureSynced()
            let! events = withWorkspaceLock eventFilePath (fun () -> readEventsFile eventFilePath)
            return events
        }

    member _.GetRevision() : int = state.Cache.Revision

    member _.GetSessionStateSync(sessionId: string) : SessionState =
        state.Cache.GetSessionStateSync(sessionId)

    member this.GetSessionState(sessionId: string) : JS.Promise<SessionState> =
        promise {
            do! state.EnsureSynced()
            return this.GetSessionStateSync(sessionId)
        }

    member this.GetSessionOverview(sessionId: string) : SessionOverview =
        let st = this.GetSessionStateSync(sessionId)
        fromSessionState st

    member _.GetAllSessionStates() : JS.Promise<Map<string, SessionState>> =
        promise {
            do! state.EnsureSynced()
            return state.Cache.GetAllSessionStates()
        }

    member _.EnsureInitialized() : JS.Promise<unit> = state.EnsureInitialized()

    member _.AppendEvent(e: WanEvent) : JS.Promise<Result<unit, string>> =
        enqueueWrite (Ok()) (fun () ->
            promise {
                do! state.EnsureInitialized()

                try
                    do!
                        withWorkspaceLock eventFilePath (fun () ->
                            promise {
                                do! state.SyncNewEvents()
                                do! appendLineFn eventFilePath e
                                let! stats = statAsync eventFilePath
                                state.LastKnownSize <- unbox<int64> (stats?size)
                                state.LastReadByteOffset <- state.LastKnownSize
                            })

                    state.Cache.FoldWan e
                    state.EventCountRead <- state.EventCountRead + 1
                    return Ok()
                with ex ->
                    return Error ex.Message
            })

    member _.AppendEventOrFail(e: WanEvent) : JS.Promise<unit> =
        enqueueWrite () (fun () ->
            promise {
                do! state.EnsureInitialized()

                do!
                    withWorkspaceLock eventFilePath (fun () ->
                        promise {
                            do! state.SyncNewEvents()
                            do! appendLineFn eventFilePath e
                            let! stats = statAsync eventFilePath
                            state.LastKnownSize <- unbox<int64> (stats?size)
                            state.LastReadByteOffset <- state.LastKnownSize
                        })

                state.Cache.FoldWan e
                state.EventCountRead <- state.EventCountRead + 1
            })

    /// Atomic multi-event append: one lock, one contiguous write of all lines.
    member _.AppendEventsOrFail(events: WanEvent list) : JS.Promise<unit> =
        if List.isEmpty events then
            Promise.lift ()
        else
            enqueueWrite () (fun () ->
                promise {
                    do! state.EnsureInitialized()

                    do!
                        withWorkspaceLock eventFilePath (fun () ->
                            promise {
                                do! state.SyncNewEvents()

                                let block =
                                    events |> List.map (fun e -> wanEventToLine e + "\n") |> String.concat ""

                                do! appendFileAsync eventFilePath block
                                let! stats = statAsync eventFilePath
                                state.LastKnownSize <- unbox<int64> (stats?size)
                                state.LastReadByteOffset <- state.LastKnownSize
                            })

                    for e in events do
                        state.Cache.FoldWan e
                        state.EventCountRead <- state.EventCountRead + 1
                })

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
        enqueueWrite false (fun () ->
            promise {
                do! state.EnsureInitialized()
                let trimmedAnchor = anchor.Trim()
                let mutable claimed = false

                do!
                    withWorkspaceLock eventFilePath (fun () ->
                        promise {
                            do! state.SyncNewEvents()

                            match
                                NudgeDispatchClaim.tryClaim
                                    state.Cache
                                    sessionId
                                    action
                                    trimmedAnchor
                                    nudgeId
                                    nonce
                                    sessionGen
                                    cancelGen
                                    humanTurnId
                                    nudgeOrdinal
                                    isBlocked
                                    (getTimestampMs().ToString())
                            with
                            | None -> ()
                            | Some ev ->
                                do! appendLineFn eventFilePath ev
                                let! stats = statAsync eventFilePath
                                state.LastKnownSize <- unbox<int64> (stats?size)
                                state.LastReadByteOffset <- state.LastKnownSize
                                state.Cache.FoldWan ev
                                state.EventCountRead <- state.EventCountRead + 1
                                claimed <- true
                        })

                return claimed
            })
