module Wanxiangshu.Tests.EventLogRuntimeTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Shell.EventLogCodec
open Wanxiangshu.Shell.EventLogFiles
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.ReviewRuntime

let appendThenReadAll () = promise {
    let! dir = mkdtempAsync "eventlog-read-"
    let store = EventLogStore dir
    let! _ = appendLoopActivated dir "s-read" "task one"
    let! events = store.ReadAllEvents()
    check "one event" (events.Length = 1)
    check "kind" (events.[0].Kind = eventKindLoopActivated)
    check "task" (events.[0].Payload |> Map.tryFind "task" = Some "task one")
    do! rmAsync dir
}

let syncReviewFromEventLogProjectsTask () = promise {
    let! dir = mkdtempAsync "eventlog-sync-"
    let sessionID = "s-sync"
    let review = createReviewStore ()
    do! appendLoopActivated dir sessionID "ship from ndjson" |> Promise.map ignore
    do! syncReviewFromEventLog review dir sessionID
    equal "active task" (Some "ship from ndjson") (review.getReviewTask sessionID)
    do! rmAsync dir
}

let syncClearsAfterAcceptedVerdict () = promise {
    let! dir = mkdtempAsync "eventlog-verdict-"
    let sessionID = "s-verdict"
    let review = createReviewStore ()
    do! appendLoopActivated dir sessionID "task" |> Promise.map ignore
    do! appendReviewVerdict dir sessionID verdictAccepted None |> Promise.map ignore
    do! syncReviewFromEventLog review dir sessionID
    equal "cleared after accept" None (review.getReviewTask sessionID)
    do! rmAsync dir
}

let parallelAppendsBothPersist () = promise {
    let! dir = mkdtempAsync "eventlog-parallel-"
    let store = EventLogStore dir
    let! _ =
        Promise.all [|
            appendLoopActivated dir "s-p1" "task alpha"
            appendLoopActivated dir "s-p2" "task beta"
        |]
    let! events = store.ReadAllEvents()
    check "parallel append: length 2" (events.Length = 2)
    let kinds = events |> List.map (fun e -> e.Session) |> Set.ofList
    check "parallel append: both sessions" (kinds = Set.ofList ["s-p1"; "s-p2"])
    do! rmAsync dir
}

let readStopsAtCorruptLine () = promise {
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

let strictAppendLoopActivatedPersistsEvent () = promise {
    let! dir = mkdtempAsync "eventlog-strict-append-"
    let store = EventLogStore dir
    do! appendLoopActivatedOrFail dir "s-strict" "strict task"
    let! events = store.ReadAllEvents()
    check "strict append: one event" (events.Length = 1)
    check "strict append: kind" (events.[0].Kind = eventKindLoopActivated)
    check "strict append: task" (events.[0].Payload |> Map.tryFind "task" = Some "strict task")
    do! rmAsync dir
}

let strictAppendLoopActivatedFailsOnBadPath () = promise {
    let! dir = mkdtempAsync "eventlog-strict-fail-"
    let file = dir + "/badfile"
    do! writeFileAsync file "content"
    let! result = appendLoopActivatedOrFail file "s-bad" "task" |> Promise.result
    match result with
    | Error _ -> check "rejected on bad path" true
    | Ok _ -> failwith "expected rejection on bad path"
    do! rmAsync dir
}
let selfHealingLockDeletesFileLock () = promise {
    let! dir = mkdtempAsync "eventlog-selfhealing-"
    let lockPath = dir + "/.wanxiangshu.ndjson.lock"
    do! writeFileAsync lockPath "1"
    let store = EventLogStore dir
    do! store.AppendEventOrFail({ V = 1
                                  Session = "s-heal"
                                  Kind = eventKindLoopActivated
                                  At = ""
                                  Payload = Map [ "task", "healed" ] })
    let! events = store.ReadAllEvents()
    check "self healing lock: successfully appended and read" (events.Length = 1)
    do! rmAsync dir
}

let run () = promise {
    do! appendThenReadAll ()
    do! syncReviewFromEventLogProjectsTask ()
    do! syncClearsAfterAcceptedVerdict ()
    do! parallelAppendsBothPersist ()
    do! readStopsAtCorruptLine ()
    do! strictAppendLoopActivatedPersistsEvent ()
    do! strictAppendLoopActivatedFailsOnBadPath ()
    do! selfHealingLockDeletesFileLock ()
}