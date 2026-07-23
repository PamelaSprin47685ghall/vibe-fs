module Wanxiangshu.Tests.EventLogRuntimeStoreTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventStore
open Wanxiangshu.Runtime.EventLogFile
open Wanxiangshu.Runtime.EventLogIoRaw
open Wanxiangshu.Runtime.EventLogLock
open Wanxiangshu.Runtime.ReviewEventWriter
open Wanxiangshu.Runtime.SessionEventWriter
open Wanxiangshu.Runtime.EventLogRuntimeNudge
open Wanxiangshu.Kernel.Nudge


let testReadAllEventsIdempotent () : JS.Promise<unit> =
    promise {
        let! dir = mkdtempAsync "eventlog-readonce-"
        let store = EventLogStore dir
        let! _ = appendLoopActivated dir "s-once" "task once"
        let! events1 = store.ReadAllEvents()
        check "first read: 1 event" (events1.Length = 1)
        let! events2 = store.ReadAllEvents()
        check "second read: same 1 event" (events2.Length = 1)
        check "second read: same content" (events2.[0].Payload |> Map.tryFind "task" = Some "task once")
        do! rmAsync dir
    }

let appendWithEmptyWorkspaceRootWritesNothing () =
    promise {
        let cwdLogPath = eventPath ""
        let! before = tryReadFileAsync cwdLogPath
        do! appendLoopActivatedOrFail "" "s-empty-root" "task"

        let! claimed =
            tryClaimNudgeDispatch "" "s-empty-root" Wanxiangshu.Kernel.Nudge.NudgeTodo "anchor" "" "" 0 0 "" 1

        check "claim disabled without workspace" (not claimed)
        let! after = tryReadFileAsync cwdLogPath
        equal "empty workspace root leaves cwd log untouched" before after
    }

let deletingInitializedLogClearsProjection () =
    promise {
        let! dir = mkdtempAsync "eventlog-delete-"
        let sessionId = "s-delete"
        let store = EventLogStore dir
        do! appendLoopActivatedOrFail dir sessionId "task"
        let! beforeDelete = store.GetSessionState sessionId
        check "projection populated before delete" (beforeDelete.ReviewTask = Some "task")
        do! rmAsync (eventPath dir)
        let! afterDelete = store.GetSessionState sessionId
        equal "deleted log clears projection" None afterDelete.ReviewTask
        do! rmAsync dir
    }

let ensureSyncedPropagatesNonMissingPathErrors () =
    promise {
        let! dir = mkdtempAsync "eventlog-err-propagate-"
        let workspaceFile = dir + "/workspace-file"
        do! writeFileAsync workspaceFile "not a directory"
        let store = EventLogStore workspaceFile

        let! result =
            promise {
                try
                    do! store.EnsureSynced()
                    return Ok()
                with ex ->
                    return Error ex
            }

        match result with
        | Error _ -> check "EnsureSynced propagated non-missing path error" true
        | Ok _ -> failwith "Expected EnsureSynced to fail for a file workspace root"

        do! rmAsync dir
    }

let testEventLogStoreResetPoisonRecoversEnqueue () =
    promise {
        let! dir = mkdtempAsync "eventlog-reset-poison-"
        let mutable resolveSlowWrite = fun () -> ()
        let slowWritePromise = Promise.create (fun resolve _ -> resolveSlowWrite <- resolve)
        let mutable slowWrite = true

        let mockAppend (filePath: string) (ev: WanEvent) =
            promise {
                if slowWrite then
                    do! slowWritePromise

                do! appendFileAsync filePath (wanEventToLine ev + "\n")
            }

        let store = EventLogStore(dir, appendLineOverride = mockAppend, timeoutMs = 50)

        let ev =
            { V = 1
              Session = "s1"
              Kind = "test_reset"
              At = ""
              Payload = Map [ "k", "v" ]
              EventId = None
              WriterId = None
              Sequence = None
              Checksum = None }

        let! res = store.AppendEvent ev

        match res with
        | Error msg ->
            check "store timed out and was poisoned" (msg.Contains("TimeoutError") || msg.Contains("QueuePoisoned"))
        | Ok _ -> failwith "expected timeout"

        check "store is poisoned" store.Poisoned
        store.ResetPoison()
        check "store is unpoisoned after ResetPoison" (not store.Poisoned)

        slowWrite <- false
        resolveSlowWrite ()

        let! res2 = store.AppendEvent ev

        match res2 with
        | Ok() -> check "append succeeded after ResetPoison" true
        | Error msg -> failwith ("expected Ok after ResetPoison, got: " + msg)

        let! events = store.ReadAllEvents()
        check "events persisted after recovery" (events.Length >= 1)
        do! rmAsync dir
    }

let testEventLogRuntimeStoreCacheAutoResetsPoisonedEntry () =
    promise {
        Wanxiangshu.Runtime.EventLogRuntimeStore.clear ()
        let! dir = mkdtempAsync "eventlog-runtime-autoreset-"

        let store = Wanxiangshu.Runtime.EventLogRuntimeStore.getStore dir
        store.Dispose()

        check "store poisoned after dispose" store.Poisoned

        let store2 = Wanxiangshu.Runtime.EventLogRuntimeStore.getStore dir
        check "getStore auto resets poisoned instance" (not store2.Poisoned)

        Wanxiangshu.Runtime.EventLogRuntimeStore.clear ()
        do! rmAsync dir
    }

let run () =
    promise {
        do! testReadAllEventsIdempotent ()
        do! appendWithEmptyWorkspaceRootWritesNothing ()
        do! deletingInitializedLogClearsProjection ()
        do! ensureSyncedPropagatesNonMissingPathErrors ()
        do! testEventLogStoreResetPoisonRecoversEnqueue ()
        do! testEventLogRuntimeStoreCacheAutoResetsPoisonedEntry ()
    }
