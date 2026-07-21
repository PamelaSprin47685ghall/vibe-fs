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
open Wanxiangshu.Runtime.EventStoreStateInit
open Wanxiangshu.Runtime.EventStoreStateSync

[<Global("Buffer")>]
let private nodeBuffer: obj = jsNative

let private byteLength (s: string) : int =
    unbox<int> (nodeBuffer?byteLength (s, "utf-8"))

type EventLogStore(workspaceRoot: string, ?appendLineOverride: string -> WanEvent -> JS.Promise<unit>, ?timeoutMs: int)
    =
    let queue = SerialQueue()
    let eventFilePath = eventPath workspaceRoot
    let writesDisabled = workspaceRoot = ""
    let appendLineFn = defaultArg appendLineOverride appendLine
    let state = EventStoreState(workspaceRoot, eventFilePath)
    let writerId = System.Guid.NewGuid().ToString()

    let enqueueWrite (noopValue: 'T) (work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        if writesDisabled then
            Promise.lift noopValue
        else
            queue.Enqueue(work, timeoutMs = defaultArg timeoutMs 10000)

    member _.Generation = queue.Generation
    member _.Poisoned = queue.Poisoned

    member this.EnsureSynced() : JS.Promise<unit> = state.EnsureSynced()
    member _.ProjectionCache = state.Cache

    member _.ReadAllEvents() : JS.Promise<WanEvent list> =
        promise {
            do! state.EnsureSynced()
            let! events = withWorkspaceLock eventFilePath (fun () -> readEventsFile eventFilePath)
            return events
        }

    member _.EnsureInitialized() : JS.Promise<unit> = state.EnsureInitialized()

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

    member _.AppendEvent(e: WanEvent) : JS.Promise<Result<unit, string>> =
        promise {
            try
                let! res =
                    enqueueWrite (Ok()) (fun () ->
                        promise {
                            do! state.EnsureInitialized()
                            let mutable decoratedOpt = None

                            try
                                do!
                                    withWorkspaceLock eventFilePath (fun () ->
                                        promise {
                                            do! state.SyncNewEvents()
                                            let decorated = decorateEvent writerId state.EventCountRead e
                                            decoratedOpt <- Some decorated
                                            let line = wanEventToLine decorated + "\n"
                                            do! appendLineFn eventFilePath decorated
                                            let! stats = statAsync eventFilePath
                                            state.LastKnownSize <- state.SizeOf stats
                                            state.LastReadByteOffset <- state.LastReadByteOffset + int64 (byteLength line)
                                        })

                                match decoratedOpt with
                                | Some decorated ->
                                    state.Cache.FoldWan decorated
                                    state.EventCountRead <- state.EventCountRead + 1
                                | None -> ()

                                return Ok()
                            with ex ->
                                return Error ex.Message
                        })

                return res
            with ex ->
                return Error ex.Message
        }

    member _.AppendEventOrFail(e: WanEvent) : JS.Promise<unit> =
        enqueueWrite () (fun () ->
            promise {
                do! state.EnsureInitialized()
                let mutable decoratedOpt = None

                do!
                    withWorkspaceLock eventFilePath (fun () ->
                        promise {
                            do! state.SyncNewEvents()
                            let decorated = decorateEvent writerId state.EventCountRead e
                            decoratedOpt <- Some decorated
                            let line = wanEventToLine decorated + "\n"
                            do! appendLineFn eventFilePath decorated
                            let! stats = statAsync eventFilePath
                            state.LastKnownSize <- state.SizeOf stats
                            state.LastReadByteOffset <- state.LastReadByteOffset + int64 (byteLength line)
                        })

                match decoratedOpt with
                | Some decorated ->
                    state.Cache.FoldWan decorated
                    state.EventCountRead <- state.EventCountRead + 1
                | None -> ()
            })

    /// Atomic multi-event append: one lock, one contiguous write of all lines.
    member _.AppendEventsOrFail(events: WanEvent list) : JS.Promise<unit> =
        if List.isEmpty events then
            Promise.lift ()
        else
            enqueueWrite () (fun () ->
                promise {
                    do! state.EnsureInitialized()
                    let mutable decoratedEvents = []

                    do!
                        withWorkspaceLock eventFilePath (fun () ->
                            promise {
                                do! state.SyncNewEvents()
                                let decorateds = decorateEvents writerId state.EventCountRead events
                                decoratedEvents <- decorateds

                                let block =
                                    decoratedEvents
                                    |> List.map (fun e -> wanEventToLine e + "\n")
                                    |> String.concat ""

                                do! appendFileAsync eventFilePath block
                                let! stats = statAsync eventFilePath
                                state.LastKnownSize <- state.SizeOf stats
                                state.LastReadByteOffset <- state.LastReadByteOffset + int64 (byteLength block)
                            })

                    for e in decoratedEvents do
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
                                let decorated = decorateEvent writerId state.EventCountRead ev
                                let line = wanEventToLine decorated + "\n"
                                do! appendLineFn eventFilePath decorated
                                let! stats = statAsync eventFilePath
                                state.LastKnownSize <- state.SizeOf stats
                                state.LastReadByteOffset <- state.LastReadByteOffset + int64 (byteLength line)
                                state.Cache.FoldWan decorated
                                state.EventCountRead <- state.EventCountRead + 1
                                claimed <- true
                        })

                return claimed
            })

    member _.Dispose() : unit =
        state.Dispose()
        queue.Poisoned <- true
