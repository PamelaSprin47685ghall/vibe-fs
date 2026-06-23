module VibeFs.Tests.MagicReplaySpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.MagicMessageBuilders

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.MagicCore
open VibeFs.Kernel.MagicProjection
open VibeFs.Opencode.MagicTodo


let replayBacklogOpencodeDoesNotMergeConsecutiveTodoWrite () =
    let msgs = [ todoWriteMsg "m1" "c1" "W1"; todoWriteMsg "m2" "c2" "W2"; todoWriteMsg "m3" "c3" "W3" ]
    let backlog = replayBacklogFor Opencode msgs
    check "opencode: each todowrite is one backlog entry" (backlog.Length = 3)
    check "opencode: reports not merged" (backlog.[0].report = "W1" && backlog.[1].report = "W2" && backlog.[2].report = "W3")

let replayBacklogTest () =
    let msgs = [ todoWriteMsg "m1" "c1" "Implemented parser"; todoWriteMsg "m2" "c2" "Fixed critical bug" ]
    let backlog = replayBacklog msgs
    check "replay: backlog count" (backlog.Length = 2)
    check "replay: entry 1 report" (backlog.[0].report = "Implemented parser")
    check "replay: entry 2 report" (backlog.[1].report = "Fixed critical bug")

let replayEmpty () =
    let backlog = replayBacklog []
    check "replay empty: no backlog" (backlog.IsEmpty)

let replaySkipsEmpty () =
    let msgs = [ todoWriteMsg "m1" "c1" "Report A"; todoWriteMsg "m2" "c2" "" ]
    let backlog = replayBacklog msgs
    check "replay skips empty: only 1" (backlog.Length = 1)

let replayBacklogForMimocodeUsesTask () =
    let msgs = [ toolMsg "task" "m1" "c1" "Implemented parser" ]
    let backlog = replayBacklogFor Mimocode msgs
    check "mimocode replay: task enters backlog" (backlog.Length = 1)
    check "mimocode replay: task report preserved" (backlog.[0].report = "Implemented parser")

let replayBacklogForMimocodeIgnoresActor () =
    let msgs = [ toolMsg "actor" "m1" "c1" "Implemented parser" ]
    let backlog = replayBacklogFor Mimocode msgs
    check "mimocode replay: actor does not enter backlog" (backlog.IsEmpty)

let magicSessionCaptureRoundTrip () =
    let session = MagicSession(Mimocode)
    session.CaptureReport("session-call-1", "  scoped text  ")
    check "session capture: round trip" (session.TakeReport "session-call-1" = "scoped text")
    check "session capture: consumed" (session.TakeReport "session-call-1" = "")

let magicSessionShareReportTableAcrossInstances () =
    let producer = MagicSession(Mimocode)
    let consumer = MagicSession(Mimocode)
    producer.CaptureReport("shared-call", "carry over")
    check "session capture: visible across mimocode instances" (consumer.TakeReport "shared-call" = "carry over")

let magicSessionRestoresMimocodeReportDuringBacklogRebuild () =
    let session = MagicSession(Mimocode)
    let msgs = [ taskCreateMsg "m1" "restore-c1" ]
    session.CaptureReport("restore-c1", "captured before execute")
    let backlog = session.GetOrRebuildBacklog("test", msgs)
    check "session rebuild: no captured report injection without host rewrite" (backlog.IsEmpty)
    check "session rebuild: report remains untouched" (session.TakeReport "restore-c1" = "captured before execute")

let replayBacklogForMimocodeMergesConsecutiveWorkReports () =
    let msgs = [ taskMsgWithReport "m1" "c1" "Work A"; taskMsgWithReport "m2" "c2" "Work B"; taskMsgWithReport "m3" "c3" "Work C" ]
    let backlog = replayBacklogFor Mimocode msgs
    check "mimocode work reports: one entry per task now" (backlog.Length = 3)
    check "mimocode work reports: preserve order" (backlog.[0].report = "Work A" && backlog.[1].report = "Work B" && backlog.[2].report = "Work C")

let replayBacklogForMimocodeMergesConsecutiveTaskBurst () =
    let msgs = [ toolMsg "task" "m1" "c1" "R1"; toolMsg "task" "m2" "c2" "R2"; toolMsg "task" "m3" "c3" "R3" ]
    let backlog = replayBacklogFor Mimocode msgs
    check "mimocode task behaves like opencode per call" (backlog.Length = 3)
    check "mimocode task preserves reports" (backlog.[0].report = "R1" && backlog.[1].report = "R2" && backlog.[2].report = "R3")

let replayBacklogForMimocodeSplitsBurstsOnGap () =
    let msgs = [ toolMsg "task" "m1" "c1" "A"; userMsg "u1" "gap"; toolMsg "task" "m2" "c2" "B" ]
    let backlog = replayBacklogFor Mimocode msgs
    check "mimocode gap: two backlog entries" (backlog.Length = 2)
    check "mimocode gap: preserves order" (backlog.[0].report.Contains("A") && backlog.[1].report.Contains("B"))

let replayBacklogForMimocodeIgnoresAssistantTextBetweenTasks () =
    let msgs = [ taskMsgWithReport "m1" "c1" "Work A"; assistantTextMsg "a1" "let me continue"; taskMsgWithReport "m2" "c2" "Work B" ]
    let backlog = replayBacklogFor Mimocode msgs
    check "mimocode assistant text: does not merge calls" (backlog.Length = 2)
    check "mimocode assistant text: preserves reports" (backlog.[0].report = "Work A" && backlog.[1].report = "Work B")

let replayBacklogForMimocodeIgnoresReasoningBetweenTasks () =
    let msgs = [ taskMsgWithReport "m1" "c1" "Work A"; reasoningMsg "r1" "thinking through next step"; taskMsgWithReport "m2" "c2" "Work B" ]
    let backlog = replayBacklogFor Mimocode msgs
    check "mimocode reasoning: does not merge calls" (backlog.Length = 2)
    check "mimocode reasoning: preserves reports" (backlog.[0].report = "Work A" && backlog.[1].report = "Work B")

let replayBacklogForMimocodeSplitsOnOtherToolCall () =
    let msgs = [ taskMsgWithReport "m1" "c1" "Work A"; toolMsg "read" "r1" "rc1" "reading"; taskMsgWithReport "m2" "c2" "Work B" ]
    let backlog = replayBacklogFor Mimocode msgs
    check "mimocode other tool: splits burst" (backlog.Length = 2)
    check "mimocode other tool: preserves order" (
        backlog.[0].report.Contains("Work A") && backlog.[1].report.Contains("Work B"))

let findFoldRangeTest () =
    let flat =
        flatten [
            userMsg "u1" "start"
            todoWriteMsg "m1" "c1" "R1"
            todoWriteMsg "m2" "c2" "R2"
            todoWriteMsg "m3" "c3" "R3"
        ]
    match findFoldRange flat false with
    | None -> check "fold range: found" false
    | Some r -> check "fold range: secondToLast > first" (r.secondToLast > r.firstResult)

let findFoldRangeOpencodePerCallMimicodePerBurst () =
    let flatOpencode =
        flatten [
            userMsg "u1" "start"
            todoWriteMsg "m1" "c1" "R1"
            todoWriteMsg "m2" "c2" "R2"
            todoWriteMsg "m3" "c3" "R3"
        ]
    check "opencode: three todowrites enable fold" (findFoldRangeFor Opencode flatOpencode false |> Option.isSome)
    let flatMimo =
        flatten [
            userMsg "u1" "start"
            taskMsgWithReport "m1" "c1" "A"
            taskMsgWithReport "m2" "c2" "B"
            taskMsgWithReport "m3" "c3" "C"
        ]
    check "mimocode: three task calls now enable fold like opencode" (
        findFoldRangeFor Mimocode flatMimo false |> Option.isSome)

let findFoldRangeForMimocodeIgnoresReadOnlyTaskCalls () =
    let flat =
        flatten [
            userMsg "u1" "start"
            taskMsgWithActionAndReport "list" "m1" "c1" "Read 1"
            taskMsgWithActionAndReport "get" "m2" "c2" "Read 2"
            taskMsgWithActionAndReport "list" "m3" "c3" "Read 3"
        ]
    check "mimocode: read-only concept removed; task calls are anchors" (
        findFoldRangeFor Mimocode flat false |> Option.isSome)

let findFoldRangeForMimocodeRequiresThreeProgressBursts () =
    let flat =
        flatten [
            userMsg "u1" "start"
            taskMsgWithActionAndReport "list" "m1" "c1" "Read 1"
            taskMsgWithActionAndReport "start" "m2" "c2" "Work 1"
            taskMsgWithActionAndReport "get" "m3" "c3" "Read 2"
            taskMsgWithActionAndReport "done" "m4" "c4" "Work 2"
            userMsg "u2" "gap"
            taskMsgWithActionAndReport "block" "m5" "c5" "Work 3"
        ]
    check "mimocode: three task calls satisfy 3-anchor fold" (
        findFoldRangeFor Mimocode flat false |> Option.isSome)

let findFoldRangeForMimocodeUsesLastProgressCallInBurst () =
    let flat =
        flatten [
            userMsg "u1" "start"
            taskMsgWithActionAndReport "start" "m1" "c1" "Work 1"
            taskMsgWithActionAndReport "list" "m2" "c2" "Read 1"
            userMsg "u2" "gap"
            taskMsgWithActionAndReport "done" "m3" "c3" "Work 2"
            taskMsgWithActionAndReport "get" "m4" "c4" "Read 2"
            userMsg "u3" "gap"
            taskMsgWithActionAndReport "block" "m5" "c5" "Work 3"
            taskMsgWithActionAndReport "list" "m6" "c6" "Read 3"
        ]
    check "mimocode: first and second-to-last anchors follow raw call order" (
        findFoldRangeFor Mimocode flat false
        |> Option.exists (fun range ->
            partCallID flat.[range.firstResult].part = "c1"
            && partCallID flat.[range.secondToLast].part = "c5"))

let findFoldRangeForMimocodeAssistantTextKeepsBurst () =
    let flat =
        flatten [
            userMsg "u1" "start"
            taskMsgWithActionAndReport "start" "m1" "c1" "Work 1"
            assistantTextMsg "a1" "thinking aloud"
            taskMsgWithActionAndReport "done" "m2" "c2" "Work 2"
            userMsg "u2" "gap"
            taskMsgWithActionAndReport "start" "m3" "c3" "Work 3"
            assistantTextMsg "a2" "more thinking"
            taskMsgWithActionAndReport "done" "m4" "c4" "Work 4"
            userMsg "u3" "gap"
            taskMsgWithActionAndReport "done" "m5" "c5" "Work 5"
        ]
    match findFoldRangeFor Mimocode flat false with
    | None -> check "mimocode assistant text in burst: fold found" false
    | Some range ->
        check "mimocode assistant text in burst: first anchor stays first task call" (
            partCallID flat.[range.firstResult].part = "c1")

let magicSessionRefreshesBacklogForMimocode () =
    let session = MagicSession(Mimocode)
    let first = [ toolMsg "task" "m1" "c1" "R1" ]
    let second = [ toolMsg "task" "m1" "c1" "R1"; toolMsg "task" "m2" "c2" "R2" ]
    let _ = session.GetOrRebuildBacklog("test", first)
    let backlog = session.GetOrRebuildBacklog("test", second)
    check "mimocode session: rebuilds stale backlog per call" (backlog.Length = 2)

let magicSessionRefreshesBacklog () =
    let session = MagicSession(Opencode)
    let first = [ todoWriteMsg "m1" "c1" "R1" ]
    let second = [ todoWriteMsg "m1" "c1" "R1"; todoWriteMsg "m2" "c2" "R2" ]
    let _ = session.GetOrRebuildBacklog("test", first)
    let backlog = session.GetOrRebuildBacklog("test", second)
    check "magic session: rebuilds stale backlog" (backlog.Length = 2)
