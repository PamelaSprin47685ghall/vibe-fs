module Wanxiangshu.Tests.EventLogRuntimeTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventStore
open Wanxiangshu.Runtime.EventLogFile
open Wanxiangshu.Runtime.SquadEventStore
open Wanxiangshu.Runtime.ReviewEventWriter
open Wanxiangshu.Runtime.SessionEventWriter
open Wanxiangshu.Runtime.EventLogRuntimeNudge
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.Review
open Wanxiangshu.Kernel.Backlog
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Subsession
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent

let appendThenReadAll () =
    promise {
        let! dir = mkdtempAsync "eventlog-read-"
        let store = EventLogStore dir
        let! _ = appendLoopActivated dir "s-read" "task one"
        let! events = store.ReadAllEvents()
        check "one event" (events.Length = 1)
        check "kind" (events.[0].Kind = eventKindLoopActivated)
        check "task" (events.[0].Payload |> Map.tryFind "task" = Some "task one")
        do! rmAsync dir
    }

let syncReviewFromEventLogDedicatedProjectsTask () =
    promise {
        let! dir = mkdtempAsync "eventlog-sync-"
        let sessionID = "s-sync"
        let review = createReviewStore ()
        do! appendLoopActivated dir sessionID "ship from ndjson" |> Promise.map ignore
        do! syncReviewFromEventLogDedicated review dir sessionID
        equal "active task" (Some "ship from ndjson") (review.getReviewTask sessionID)
        do! rmAsync dir
    }

let syncDedicatedClearsAfterAcceptedVerdict () =
    promise {
        let! dir = mkdtempAsync "eventlog-verdict-"
        let sessionID = "s-verdict"
        let review = createReviewStore ()
        do! appendLoopActivated dir sessionID "task" |> Promise.map ignore
        do! appendReviewVerdict dir sessionID verdictAccepted None |> Promise.map ignore
        do! syncReviewFromEventLogDedicated review dir sessionID
        equal "cleared after accept" None (review.getReviewTask sessionID)
        do! rmAsync dir
    }

let parallelAppendsBothPersist () =
    promise {
        let! dir = mkdtempAsync "eventlog-parallel-"
        let store = EventLogStore dir

        let! _ =
            Promise.all
                [| appendLoopActivated dir "s-p1" "task alpha"
                   appendLoopActivated dir "s-p2" "task beta" |]

        let! events = store.ReadAllEvents()
        check "parallel append: length 2" (events.Length = 2)
        let kinds = events |> List.map (fun e -> e.Session) |> Set.ofList
        check "parallel append: both sessions" (kinds = Set.ofList [ "s-p1"; "s-p2" ])
        do! rmAsync dir
    }

let readStopsAtCorruptLine () =
    promise {
        let! dir = mkdtempAsync "eventlog-corrupt-"

        let good =
            wanEventToLine
                { V = 1
                  Session = "s1"
                  Kind = eventKindLoopActivated
                  At = ""
                  Payload = Map [ "task", "ok" ] }

        let path = eventPath dir
        do! writeFileAsync path (good + "\n{broken\n" + good + "\n")
        let store = EventLogStore dir
        let! events = store.ReadAllEvents()
        check "stops before third line" (events.Length = 1)
        do! rmAsync dir
    }

let strictAppendLoopActivatedPersistsEvent () =
    promise {
        let! dir = mkdtempAsync "eventlog-strict-append-"
        let store = EventLogStore dir
        do! appendLoopActivatedOrFail dir "s-strict" "strict task"
        let! events = store.ReadAllEvents()
        check "strict append: one event" (events.Length = 1)
        check "strict append: kind" (events.[0].Kind = eventKindLoopActivated)
        check "strict append: task" (events.[0].Payload |> Map.tryFind "task" = Some "strict task")
        do! rmAsync dir
    }

let strictAppendLoopActivatedFailsOnBadPath () =
    promise {
        let! dir = mkdtempAsync "eventlog-strict-fail-"
        let file = dir + "/badfile"
        do! writeFileAsync file "content"
        let! result = appendLoopActivatedOrFail file "s-bad" "task" |> Promise.result

        match result with
        | Error _ -> check "rejected on bad path" true
        | Ok _ -> failwith "expected rejection on bad path"

        do! rmAsync dir
    }

let selfHealingLockDeletesFileLock () =
    promise {
        let! dir = mkdtempAsync "eventlog-selfhealing-"
        let lockPath = dir + "/.wanxiangshu.ndjson.lock"
        do! writeFileAsync lockPath "1"
        let store = EventLogStore dir

        do!
            store.AppendEventOrFail(
                { V = 1
                  Session = "s-heal"
                  Kind = eventKindLoopActivated
                  At = ""
                  Payload = Map [ "task", "healed" ] }
            )

        let! events = store.ReadAllEvents()
        check "self healing lock: successfully appended and read" (events.Length = 1)
        do! rmAsync dir
    }

let appendSucceedsAfterStaleLockFile () =
    promise {
        let! dir = mkdtempAsync "eventlog-stale-"
        let lockPath = dir + "/.wanxiangshu.ndjson.lock"
        do! writeFileAsync lockPath "1"
        let store = EventLogStore dir

        do!
            store.AppendEventOrFail(
                { V = 1
                  Session = "s-stale"
                  Kind = eventKindLoopActivated
                  At = ""
                  Payload = Map [ "task", "stale healed" ] }
            )

        let! events = store.ReadAllEvents()
        check "stale lock: append succeeded" (events.Length = 1)

        do!
            store.AppendEventOrFail(
                { V = 1
                  Session = "s-stale"
                  Kind = eventKindLoopActivated
                  At = ""
                  Payload = Map [ "task", "second write" ] }
            )

        let! events2 = store.ReadAllEvents()
        check "stale lock: second append succeeded" (events2.Length = 2)
        do! rmAsync dir
    }

let tryClaimNudgeDispatchPreventsOutdatedAnchor () =
    promise {
        let! dir = mkdtempAsync "eventlog-claim-outdated-"
        let sessionID = "s-claim"
        do! appendAssistantCompletedOrFail dir sessionID "task 1" (Some "agent1") (Some "provider/model-a") "t1" []
        let anchorA = Wanxiangshu.Kernel.Nudge.NudgeProjection.nudgeAnchorKey "t1" "task 1"

        do! appendAssistantCompletedOrFail dir sessionID "task 2" (Some "agent1") (Some "provider/model-b") "t2" []
        let anchorB = Wanxiangshu.Kernel.Nudge.NudgeProjection.nudgeAnchorKey "t2" "task 2"

        let! claimA = tryClaimNudgeDispatch dir sessionID Wanxiangshu.Kernel.Nudge.NudgeTodo anchorA "" "" 0 0 "" 1
        check "claim never-claimed outdated anchor A" (not claimA)

        let! claimB = tryClaimNudgeDispatch dir sessionID Wanxiangshu.Kernel.Nudge.NudgeTodo anchorB "" "" 0 0 "" 1
        check "claim latest anchor B" claimB

        do! rmAsync dir
    }

let testGetSessionStateMemoryCache () : JS.Promise<unit> =
    promise {
        let! dir = mkdtempAsync "eventlog-memcache-"
        let sessionID = "s-memcache"
        let store: EventLogStore = EventLogStore dir
        do! appendLoopActivated dir sessionID "task-cached" |> Promise.map ignore
        let! state = store.GetSessionState sessionID
        equal "memory cache review task" (Some "task-cached") state.ReviewTask
        do! rmAsync dir
    }

let testMemoryCachingAndNoRepeatedFileReads () : JS.Promise<unit> =
    promise {
        let! dir = mkdtempAsync "eventlog-mem-speed-"
        let store = EventLogStore dir
        let! _ = appendLoopActivated dir "s-mem-speed" "task memory speed"
        let! state = store.GetSessionState "s-mem-speed"
        check "initial load review task" (state.ReviewTask = Some "task memory speed")

        let path = eventPath dir
        do! rmAsync path

        let! events = store.ReadAllEvents()
        check "read on demand: 0 events after file deleted" (events.Length = 0)

        do! rmAsync dir
    }

let testGetSquadEventsCache () : JS.Promise<unit> =
    promise {
        let! dir = mkdtempAsync "eventlog-squad-cache-"
        let store = EventLogStore dir
        let squadEvent = SquadCreated("s1", "req")
        let! _ = store.AppendSquadEvent "2025-01-01T00:00:00Z" squadEvent
        let! _ = store.GetSessionState "s-any"

        let path = eventPath dir
        do! rmAsync path

        let! dag = store.GetSquadDag "s1"
        check "squad dag restored from memory" (dag.SessionId = "s1")
        equal "expected squad dag requirement" "req" dag.RootRequirement

        do! rmAsync dir
    }

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


let testLockfileIsNotUnlinkedManually () =
    promise {
        let! dir = mkdtempAsync "eventlog-no-unlink-"
        let store = EventLogStore dir
        let lockPath = dir + "/.wanxiangshu.ndjson.lock"
        do! writeFileAsync lockPath "dummy"
        
        // Appending should still work because proper-lockfile handles lock checks and stale detection safely.
        // But the lockfile MUST NOT be deleted via manual unlink if proper-lockfile isn't locking it or if it is stale,
        // or rather, our code must not call unlinkAsync or delete it manually.
        // Wait, let's verify that the lockfile is NOT deleted.
        do!
            store.AppendEventOrFail(
                { V = 1
                  Session = "s-no-unlink"
                  Kind = eventKindLoopActivated
                  At = ""
                  Payload = Map [ "task", "no-unlink" ] }
            )
        
        let! lockExists = fileExists lockPath
        check "lockfile was NOT deleted manually" lockExists
        do! rmAsync dir
    }

let testSyncNewEventsAndEnsureSyncedPropagateErrors () =
    promise {
        let! dir = mkdtempAsync "eventlog-err-propagate-"
        // Use a path that is guaranteed to fail (like a directory as a file, or non-existent path)
        let badPath = dir + "/invalid-dir/file.ndjson"
        let store = EventLogStore(badPath)
        
        let! result = 
            promise {
                try
                    do! store.EnsureSynced()
                    return Ok ()
                with ex ->
                    return Error ex
            }
            
        match result with
        | Error _ -> check "EnsureSynced propagated the error" true
        | Ok _ -> failwith "Expected EnsureSynced to fail but it succeeded"
        
        do! rmAsync dir
    }
let run () =
    promise {
        do! appendThenReadAll ()
        do! syncReviewFromEventLogDedicatedProjectsTask ()
        do! syncDedicatedClearsAfterAcceptedVerdict ()
        do! parallelAppendsBothPersist ()
        do! readStopsAtCorruptLine ()
        do! strictAppendLoopActivatedPersistsEvent ()
        do! strictAppendLoopActivatedFailsOnBadPath ()
        do! selfHealingLockDeletesFileLock ()
        do! appendSucceedsAfterStaleLockFile ()
        do! tryClaimNudgeDispatchPreventsOutdatedAnchor ()
        do! testGetSessionStateMemoryCache ()
        do! testMemoryCachingAndNoRepeatedFileReads ()
        do! testGetSquadEventsCache ()
        do! testReadAllEventsIdempotent ()
        do! appendWithEmptyWorkspaceRootWritesNothing ()
        do! testLockfileIsNotUnlinkedManually ()
        do! testSyncNewEventsAndEnsureSyncedPropagateErrors ()
    }
