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
open Wanxiangshu.Runtime.EventLogIoRaw
open Wanxiangshu.Runtime.EventLogLock
open Wanxiangshu.Runtime.ReviewEventWriter
open Wanxiangshu.Runtime.SessionEventWriter
open Wanxiangshu.Runtime.EventLogRuntimeNudge
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Kernel.Review
open Wanxiangshu.Kernel.Nudge

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
                  Payload = Map [ "task", "ok" ]
                  EventId = None
                  WriterId = None
                  Sequence = None
                  Checksum = None }

        let path = eventPath dir
        do! writeFileAsync path (good + "\n{broken\n" + good + "\n")
        let store = EventLogStore dir
        let! events = store.ReadAllEvents()
        check "stops before third line" (events.Length = 2)
        check "contains repaired event" (events |> List.exists (fun e -> e.Kind = "event_log_repaired"))
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

let run () =
    promise {
        do! appendThenReadAll ()
        do! syncReviewFromEventLogDedicatedProjectsTask ()
        do! syncDedicatedClearsAfterAcceptedVerdict ()
        do! parallelAppendsBothPersist ()
        do! readStopsAtCorruptLine ()
        do! strictAppendLoopActivatedPersistsEvent ()
        do! strictAppendLoopActivatedFailsOnBadPath ()
        do! tryClaimNudgeDispatchPreventsOutdatedAnchor ()
        do! testGetSessionStateMemoryCache ()
        do! testMemoryCachingAndNoRepeatedFileReads ()
    }
