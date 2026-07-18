module Wanxiangshu.Tests.BacklogReplaySpecsMimocode

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.BacklogMessageBuilders
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.BacklogProjection
open Wanxiangshu.Runtime.BacklogSession
open Wanxiangshu.Runtime.RuntimeScope

let private scope () = create ()

let replayBacklogForMimocodeUsesTask () =
    let s = scope ()
    let msgs = [ toolMsg "task" "m1" "c1" "Implemented parser" ]
    let backlog = replayBacklogFor Mimocode s msgs
    check "mimocode replay: task enters backlog" (backlog.Length = 1)
    check "mimocode replay: task report preserved" (backlog.[0].ahaMoments = "Implemented parser")

let replayBacklogForMimocodeIgnoresActor () =
    let s = scope ()
    let msgs = [ toolMsg "actor" "m1" "c1" "Implemented parser" ]
    let backlog = replayBacklogFor Mimocode s msgs
    check "mimocode replay: actor does not enter backlog" (backlog.IsEmpty)

let backlogSessionCaptureRoundTrip () =
    let s = scope ()
    let session = BacklogSession(Mimocode, s)
    session.CaptureReport("session-call-1", "  scoped text  ")
    check "session capture: round trip" (session.TakeReport "session-call-1" = "scoped text")
    check "session capture: consumed" (session.TakeReport "session-call-1" = "")

let backlogSessionShareReportTableAcrossInstances () =
    let s = scope ()
    let producer = BacklogSession(Mimocode, s)
    let consumer = BacklogSession(Mimocode, s)
    producer.CaptureReport("shared-call", "carry over")
    check "session capture: visible across mimocode instances" (consumer.TakeReport "shared-call" = "carry over")

let backlogSessionRestoresMimocodeReportDuringBacklogRebuild () =
    let s = scope ()
    let session = BacklogSession(Mimocode, s)
    let msgs = [ taskCreateMsg "m1" "restore-c1" ]
    session.CaptureReport("restore-c1", "captured before execute")
    let backlog = session.GetOrRebuildBacklog("test", msgs)
    check "session rebuild: no captured report injection without host rewrite" (backlog.IsEmpty)
    check "session rebuild: report remains untouched" (session.TakeReport "restore-c1" = "captured before execute")

let replayBacklogForMimocodeMergesConsecutiveWorkReports () =
    let s = scope ()

    let msgs =
        [ taskMsgWithReport "m1" "c1" "Work A"
          taskMsgWithReport "m2" "c2" "Work B"
          taskMsgWithReport "m3" "c3" "Work C" ]

    let backlog = replayBacklogFor Mimocode s msgs
    check "mimocode work reports: one entry per task now" (backlog.Length = 3)

    check
        "mimocode work reports: preserve order"
        (backlog.[0].ahaMoments = "Work A"
         && backlog.[1].ahaMoments = "Work B"
         && backlog.[2].ahaMoments = "Work C")

let replayBacklogForMimocodeMergesConsecutiveTaskBurst () =
    let s = scope ()

    let msgs =
        [ toolMsg "task" "m1" "c1" "R1"
          toolMsg "task" "m2" "c2" "R2"
          toolMsg "task" "m3" "c3" "R3" ]

    let backlog = replayBacklogFor Mimocode s msgs
    check "mimocode task behaves like opencode per call" (backlog.Length = 3)

    check
        "mimocode task preserves reports"
        (backlog.[0].ahaMoments = "R1"
         && backlog.[1].ahaMoments = "R2"
         && backlog.[2].ahaMoments = "R3")

let replayBacklogForMimocodeSplitsBurstsOnGap () =
    let s = scope ()

    let msgs =
        [ toolMsg "task" "m1" "c1" "A"
          userMsg "u1" "gap"
          toolMsg "task" "m2" "c2" "B" ]

    let backlog = replayBacklogFor Mimocode s msgs
    check "mimocode gap: two backlog entries" (backlog.Length = 2)
    check "mimocode gap: preserves order" (backlog.[0].ahaMoments.Contains("A") && backlog.[1].ahaMoments.Contains("B"))

let replayBacklogForMimocodeIgnoresAssistantTextBetweenTasks () =
    let s = scope ()

    let msgs =
        [ taskMsgWithReport "m1" "c1" "Work A"
          assistantTextMsg "a1" "let me continue"
          taskMsgWithReport "m2" "c2" "Work B" ]

    let backlog = replayBacklogFor Mimocode s msgs
    check "mimocode assistant text: does not merge calls" (backlog.Length = 2)

    check
        "mimocode assistant text: preserves reports"
        (backlog.[0].ahaMoments = "Work A" && backlog.[1].ahaMoments = "Work B")

let replayBacklogForMimocodeIgnoresReasoningBetweenTasks () =
    let s = scope ()

    let msgs =
        [ taskMsgWithReport "m1" "c1" "Work A"
          reasoningMsg "r1" "thinking through next step"
          taskMsgWithReport "m2" "c2" "Work B" ]

    let backlog = replayBacklogFor Mimocode s msgs
    check "mimocode reasoning: does not merge calls" (backlog.Length = 2)

    check
        "mimocode reasoning: preserves reports"
        (backlog.[0].ahaMoments = "Work A" && backlog.[1].ahaMoments = "Work B")

let replayBacklogForMimocodeSplitsOnOtherToolCall () =
    let s = scope ()

    let msgs =
        [ taskMsgWithReport "m1" "c1" "Work A"
          toolMsg "read" "r1" "rc1" "reading"
          taskMsgWithReport "m2" "c2" "Work B" ]

    let backlog = replayBacklogFor Mimocode s msgs
    check "mimocode other tool: splits burst" (backlog.Length = 2)

    check
        "mimocode other tool: preserves order"
        (backlog.[0].ahaMoments.Contains("Work A")
         && backlog.[1].ahaMoments.Contains("Work B"))

let backlogSessionRefreshesBacklogForMimocode () =
    let s = scope ()
    let session = BacklogSession(Mimocode, s)
    let first = [ toolMsg "task" "m1" "c1" "R1" ]
    let second = [ toolMsg "task" "m1" "c1" "R1"; toolMsg "task" "m2" "c2" "R2" ]
    let _ = session.GetOrRebuildBacklog("test", first)
    let backlog = session.GetOrRebuildBacklog("test", second)
    check "mimocode session: rebuilds stale backlog per call" (backlog.Length = 2)
