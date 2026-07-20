module Wanxiangshu.Runtime.EventStoreStateInit

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.EventStoreIo
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventLogRecovery
open Wanxiangshu.Runtime.EventLogIo
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope

[<Emit("performance.now()")>]
let private now () : float = jsNative

type EventStoreState with
    member internal this.RepairAndReplay() : JS.Promise<unit> =
        promise {
            let opId = System.Guid.NewGuid().ToString()
            this.StateKind <- Repairing(opId, "Incremental sync corruption")
            do! repairAndTruncateFile this.WorkspaceRoot this.EventFilePath

            let! buffer = readFileBufferAsync this.EventFilePath
            let scanRes = scanEventLog buffer

            let (validOffset, events) =
                match scanRes with
                | Clean(off, evs) -> (off, evs)
                | ValidFinalLineMissingNewline(off, evs) -> (off, evs)
                | CorruptTail(off, _, _, _, _, evs) -> (off, evs)
                | CorruptMiddle(off, _, _, _, _, evs) -> (off, evs)

            let! stats = statAsync this.EventFilePath

            match this.StateKind with
            | Repairing(currentOpId, _) when currentOpId = opId ->
                this.Cache.Clear()

                for e in events do
                    this.Cache.FoldWan e

                this.EventCountRead <- events.Length
                this.LastKnownSize <- this.SizeOf stats
                this.LastReadByteOffset <- int64 validOffset
                this.PartialLineBuffer <- ""
                this.StateKind <- Ready this.Cache.Revision
            | _ -> ()
        }

    member private this.onInitSuccess(events: WanEvent list, validOffset: int, stats: obj, opId: string) : unit =
        match this.StateKind with
        | Initializing(currentOpId, _)
        | Repairing(currentOpId, _) when currentOpId = opId ->
            for e in events do
                this.Cache.FoldWan e

            this.EventCountRead <- events.Length
            this.LastKnownSize <- this.SizeOf(stats: obj)
            this.LastReadByteOffset <- int64 validOffset
            this.PartialLineBuffer <- ""
            this.StateKind <- Ready this.Cache.Revision
        | _ -> ()

    member private this.executeInitAction(opId: string) : JS.Promise<unit> =
        promise {
            let! buffer = readFileBufferAsync this.EventFilePath
            let scanRes = scanEventLog buffer

            let needsRepair =
                match scanRes with
                | Clean _ -> false
                | _ -> true

            if needsRepair then
                this.StateKind <- Repairing(opId, "Initial scan corruption")
                do! repairAndTruncateFile this.WorkspaceRoot this.EventFilePath

            let! finalBuffer = readFileBufferAsync this.EventFilePath
            let finalScan = scanEventLog finalBuffer

            let (validOffset, events) =
                match finalScan with
                | Clean(off, evs) -> (off, evs)
                | ValidFinalLineMissingNewline(off, evs) -> (off, evs)
                | CorruptTail(off, _, _, _, _, evs) -> (off, evs)
                | CorruptMiddle(off, _, _, _, _, evs) -> (off, evs)

            let! stats = statAsync this.EventFilePath
            this.onInitSuccess (events, validOffset, stats, opId)
        }

    member private this.handleInitFailure(ex: exn) : unit =
        this.StateKind <- Degraded ex.Message
        this.InitPromise <- None

    member internal this.EnsureInitializedInternal() : JS.Promise<unit> =
        promise {
            match this.StateKind with
            | Ready _
            | Disposed -> ()
            | Initializing(opId, dl) when now () > dl ->
                this.handleInitFailure (exn "InitializationTimeout: watchdog triggered")
                return failwith "InitializationTimeout: watchdog triggered"
            | Initializing _
            | Repairing _ ->
                match this.InitPromise with
                | Some p -> do! p
                | None -> ()
            | Uninitialized
            | Degraded _ ->
                let opId = System.Guid.NewGuid().ToString()
                let deadline = now () + 10000.0
                this.StateKind <- Initializing(opId, deadline)

                let runInit () =
                    promise {
                        let! exists = fileExists this.EventFilePath

                        match this.StateKind with
                        | Initializing(currentOpId, _) when currentOpId = opId ->
                            if not exists then
                                this.StateKind <- Ready 0
                            else
                                do! withWorkspaceLock this.EventFilePath (fun () -> this.executeInitAction (opId))
                        | _ -> ()
                    }

                try
                    let! initRes = PromiseQueue.withTimeout 10000 (runInit ())

                    match initRes with
                    | None ->
                        this.handleInitFailure (exn "InitializationTimeout: EventStore initialization timed out")
                        return failwith "InitializationTimeout: EventStore initialization timed out"
                    | Some _ -> ()
                with ex ->
                    this.handleInitFailure (ex)
                    return raise ex
        }

    member internal this.EnsureInitialized() : JS.Promise<unit> =
        match this.InitPromise with
        | Some p -> p
        | None ->
            let p = this.EnsureInitializedInternal()
            this.InitPromise <- Some p
            p
