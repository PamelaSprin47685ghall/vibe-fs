module Wanxiangshu.Tests.EventLogRuntimeRobustnessTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Review
open Wanxiangshu.Kernel.Backlog
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventStore


let testAppendEventFailsMemoryNotPolluted () =
    promise {
        let! dir = mkdtempAsync "eventlog-fail-append-"
        let mockAppend (_: string) (_: WanEvent) : JS.Promise<unit> = Promise.reject (exn "mock disk full")
        let store = EventLogStore(dir, appendLineOverride = mockAppend)

        let ev =
            { V = 1
              Session = "s-fail"
              Kind = eventKindLoopActivated
              At = ""
              Payload = Map [ "task", "failed task" ] }

        let! res = store.AppendEvent ev

        match res with
        | Error msg -> check "returned error" (msg.Contains("mock disk full"))
        | Ok _ -> failwith "expected append to fail"

        let! state = store.GetSessionState "s-fail"
        equal "review task is not updated" None state.ReviewTask
        let! all = store.ReadAllEvents()
        equal "no events stored" 0 all.Length
        do! rmAsync dir
    }

let testAppendEventOrFailFailsMemoryNotPolluted () =
    promise {
        let! dir = mkdtempAsync "eventlog-fail-or-fail-"
        let mockAppend (_: string) (_: WanEvent) : JS.Promise<unit> = Promise.reject (exn "mock disk full")
        let store = EventLogStore(dir, appendLineOverride = mockAppend)

        let ev =
            { V = 1
              Session = "s-fail"
              Kind = eventKindLoopActivated
              At = ""
              Payload = Map [ "task", "failed task" ] }

        let! res = store.AppendEventOrFail ev |> Promise.result

        match res with
        | Error ex -> check "rejected with error" (ex.Message.Contains("mock disk full"))
        | Ok _ -> failwith "expected append to fail"

        let! state = store.GetSessionState "s-fail"
        equal "review task is not updated" None state.ReviewTask
        let! all = store.ReadAllEvents()
        equal "no events stored" 0 all.Length
        do! rmAsync dir
    }

let testTryClaimNudgeDispatchFailsMemoryNotPolluted () =
    promise {
        let! dir = mkdtempAsync "eventlog-fail-claim-"
        let mockAppend (_: string) (_: WanEvent) : JS.Promise<unit> = Promise.reject (exn "mock disk full")
        let store = EventLogStore(dir, appendLineOverride = mockAppend)
        let sessionId = "s-claim-fail"
        let anchor = Wanxiangshu.Kernel.Nudge.NudgeProjection.nudgeAnchorKey "" ""
        let isBlocked _ _ = false

        let! claimRes =
            store.TryClaimNudgeDispatch sessionId NudgeTodo anchor "" "" 0 0 "" 1 isBlocked
            |> Promise.result

        match claimRes with
        | Error ex -> check "rejected on disk failure" (ex.Message.Contains("mock disk full"))
        | Ok _ -> failwith "expected nudge claim to reject"

        let! state = store.GetSessionState sessionId
        check "nudge dispatch should not be recorded in memory" state.NudgeDedup.LastDispatchedAnchor.IsNone
        do! rmAsync dir
    }

let run () =
    promise {
        do! testAppendEventFailsMemoryNotPolluted ()
        do! testAppendEventOrFailFailsMemoryNotPolluted ()
        do! testTryClaimNudgeDispatchFailsMemoryNotPolluted ()
    }
