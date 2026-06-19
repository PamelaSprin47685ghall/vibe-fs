module VibeFs.Tests.MagicTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.HostTools
open VibeFs.Opencode.MagicCore
open VibeFs.Opencode.MagicProjection
open VibeFs.Opencode.MagicTodo
open VibeFs.Kernel.Message

let private userMsg (id: string) (text: string) : obj =
    createObj
        [ "info", box (createObj [ "id", box id; "role", box "user"; "sessionID", box "test" ])
          "parts", box [| box {| ``type`` = "text"; text = text |} |] ]

let private timedTodoWriteMsg (id: string) (callID: string) (report: string) (created: int) (completed: int) : obj =
    createObj
        [ "info",
          box (
              createObj
                  [ "id", box id
                    "role", box "assistant"
                    "sessionID", box "test"
                    "time", box (createObj [ "created", box created; "completed", box completed ]) ]
          )
          "parts",
          box
              [| createObj
                     [ "type", box "tool"
                       "tool", box magicTodoToolName
                       "callID", box callID
                       "state",
                       box (
                           createObj
                               [ "status", box "completed"
                                 "input", box (createObj [ "completedWorkReport", box report; "todos", box [||] ])
                                 "output", box "Todos updated." ]
                       ) ] |] ]

let private todoWriteMsg (id: string) (callID: string) (report: string) : obj =
    createObj
        [ "info", box (createObj [ "id", box id; "role", box "assistant"; "sessionID", box "test" ])
          "parts",
          box
              [| createObj
                     [ "type", box "tool"
                       "tool", box magicTodoToolName
                       "callID", box callID
                       "state",
                       box (
                           createObj
                               [ "status", box "completed"
                                 "input", box (createObj [ "completedWorkReport", box report; "todos", box [||] ])
                                 "output", box "Todos updated." ]
                       ) ] |] ]

let private todoWriteErrorMsg (id: string) (callID: string) (errorText: string) : obj =
    createObj
        [ "info", box (createObj [ "id", box id; "role", box "assistant"; "sessionID", box "test" ])
          "parts",
          box
              [| createObj
                     [ "type", box "tool"
                       "tool", box magicTodoToolName
                       "callID", box callID
                       "state",
                       box (createObj [ "status", box "error"; "input", box (createObj []); "error", box errorText ]) ] |] ]

let private reviewMsg (id: string) (callID: string) (output: string) : obj =
    createObj
        [ "info", box (createObj [ "id", box id; "role", box "assistant"; "sessionID", box "test" ])
          "parts",
          box
              [| createObj
                     [ "type", box "tool"
                       "tool", box magicReviewToolName
                       "callID", box callID
                       "state",
                       box (
                           createObj
                                [ "status", box "completed"
                                  "input", box (createObj [ "review", box "looks good" ])
                                  "output", box output ]
                        ) ] |] ]

let private taskMsgWithReport (id: string) (callID: string) (report: string) : obj =
    createObj
        [ "info", box (createObj [ "id", box id; "role", box "assistant"; "sessionID", box "test" ])
          "parts",
          box
              [| createObj
                     [ "type", box "tool"
                       "tool", box "task"
                       "callID", box callID
                       "state",
                       box (
                           createObj
                               [ "status", box "completed"
                                 "input",
                                 box (
                                     createObj
                                         [ "operation", box (createObj [ "action", box "list" ])
                                           "completedWorkReport", box report ]
                                 )
                                 "output", box "ok" ]
                        ) ] |] ]

let private taskMsgWithActionAndReport (action: string) (id: string) (callID: string) (report: string) : obj =
    createObj
        [ "info", box (createObj [ "id", box id; "role", box "assistant"; "sessionID", box "test" ])
          "parts",
          box
              [| createObj
                     [ "type", box "tool"
                       "tool", box "task"
                       "callID", box callID
                       "state",
                       box (
                           createObj
                               [ "status", box "completed"
                                 "input",
                                 box (
                                     createObj
                                         [ "operation", box (createObj [ "action", box action ])
                                           "completedWorkReport", box report ]
                                 )
                                 "output", box "ok" ]
                       ) ] |] ]

let private toolMsg (toolName: string) (id: string) (callID: string) (report: string) : obj =
    createObj
        [ "info", box (createObj [ "id", box id; "role", box "assistant"; "sessionID", box "test" ])
          "parts",
          box
              [| createObj
                     [ "type", box "tool"
                       "tool", box toolName
                       "callID", box callID
                       "state",
                       box (
                           createObj
                               [ "status", box "completed"
                                 "input", box (createObj [ "completedWorkReport", box report; "todos", box [||] ])
                                 "output", box "Todos updated." ]
                       ) ] |] ]

let private backlogEntry (seq: int) (report: string) : BacklogEntry =
    { sequence = seq
      timestamp = ""
      report = report }

let replayBacklogOpencodeDoesNotMergeConsecutiveTodoWrite () =
    let msgs =
        [| todoWriteMsg "m1" "c1" "W1"
           todoWriteMsg "m2" "c2" "W2"
           todoWriteMsg "m3" "c3" "W3" |]
    let backlog = replayBacklogFor Opencode msgs
    check "opencode: each todowrite is one backlog entry" (backlog.Length = 3)
    check "opencode: reports not merged" (backlog.[0].report = "W1" && backlog.[1].report = "W2" && backlog.[2].report = "W3")

let replayBacklogTest () =
    let msgs =
        [| todoWriteMsg "m1" "c1" "Implemented parser"
           todoWriteMsg "m2" "c2" "Fixed critical bug" |]

    let backlog = replayBacklog msgs
    check "replay: backlog count" (backlog.Length = 2)
    check "replay: entry 1 report" (backlog.[0].report = "Implemented parser")
    check "replay: entry 2 report" (backlog.[1].report = "Fixed critical bug")

let replayEmpty () =
    let backlog = replayBacklog [||]
    check "replay empty: no backlog" (backlog.IsEmpty)

let replaySkipsEmpty () =
    let msgs = [| todoWriteMsg "m1" "c1" "Report A"; todoWriteMsg "m2" "c2" "" |]
    let backlog = replayBacklog msgs
    check "replay skips empty: only 1" (backlog.Length = 1)

let replayBacklogForMimocodeUsesTask () =
    let msgs = [| toolMsg "task" "m1" "c1" "Implemented parser" |]
    let backlog = replayBacklogFor Mimocode msgs
    check "mimocode replay: task enters backlog" (backlog.Length = 1)
    check "mimocode replay: task report preserved" (backlog.[0].report = "Implemented parser")

let replayBacklogForMimocodeIgnoresActor () =
    let msgs = [| toolMsg "actor" "m1" "c1" "Implemented parser" |]
    let backlog = replayBacklogFor Mimocode msgs
    check "mimocode replay: actor does not enter backlog" (backlog.IsEmpty)

let mimocodeTaskReportCaptureRoundTrip () =
    captureCompletedWorkReport "call-1" "  backlog text  "
    let taken = takeCompletedWorkReport "call-1"
    check "capture report: round trip" (taken = "backlog text")
    check "capture report: consumed" (takeCompletedWorkReport "call-1" = "")

let replayBacklogForMimocodeMergesConsecutiveWorkReports () =
    let msgs =
        [| taskMsgWithReport "m1" "c1" "Work A"
           taskMsgWithReport "m2" "c2" "Work B"
           taskMsgWithReport "m3" "c3" "Work C" |]
    let backlog = replayBacklogFor Mimocode msgs
    check "mimocode work reports: one merged entry" (backlog.Length = 1)
    check "mimocode work reports: all present" (
        backlog.[0].report.Contains("Work A")
        && backlog.[0].report.Contains("Work B")
        && backlog.[0].report.Contains("Work C"))

let replayBacklogForMimocodeMergesConsecutiveTaskBurst () =
    let msgs =
        [| toolMsg "task" "m1" "c1" "R1"
           toolMsg "task" "m2" "c2" "R2"
           toolMsg "task" "m3" "c3" "R3" |]
    let backlog = replayBacklogFor Mimocode msgs
    check "mimocode burst: one backlog entry" (backlog.Length = 1)
    check "mimocode burst: merges reports" (
        backlog.[0].report.Contains("R1")
        && backlog.[0].report.Contains("R2")
        && backlog.[0].report.Contains("R3"))

let replayBacklogForMimocodeSplitsBurstsOnGap () =
    let msgs =
        [| toolMsg "task" "m1" "c1" "A"
           userMsg "u1" "gap"
           toolMsg "task" "m2" "c2" "B" |]
    let backlog = replayBacklogFor Mimocode msgs
    check "mimocode gap: two backlog entries" (backlog.Length = 2)
    check "mimocode gap: preserves order" (backlog.[0].report.Contains("A") && backlog.[1].report.Contains("B"))

let findFoldRangeTest () =
    let flat =
        VibeFs.Kernel.Message.flatten (
            [| userMsg "u1" "start"
               todoWriteMsg "m1" "c1" "R1"
               todoWriteMsg "m2" "c2" "R2"
               todoWriteMsg "m3" "c3" "R3" |]
        )

    match findFoldRange flat false with
    | None -> check "fold range: found" false
    | Some r -> check "fold range: secondToLast > first" (r.secondToLast > r.firstResult)

let findFoldRangeOpencodePerCallMimocodePerBurst () =
    let flatOpencode =
        flatten (
            [| userMsg "u1" "start"
               todoWriteMsg "m1" "c1" "R1"
               todoWriteMsg "m2" "c2" "R2"
               todoWriteMsg "m3" "c3" "R3" |]
        )
    check "opencode: three todowrites enable fold" (findFoldRangeFor Opencode flatOpencode false |> Option.isSome)
    let flatMimo =
        flatten (
            [| userMsg "u1" "start"
               taskMsgWithReport "m1" "c1" "A"
               taskMsgWithReport "m2" "c2" "B"
               taskMsgWithReport "m3" "c3" "C" |]
        )
    check "mimocode: three consecutive task calls are one burst (no 3-anchor fold)" (
        findFoldRangeFor Mimocode flatMimo false |> Option.isNone)

let findFoldRangeForMimocodeIgnoresReadOnlyTaskCalls () =
    let flat =
        flatten (
            [| userMsg "u1" "start"
               taskMsgWithActionAndReport "list" "m1" "c1" "Read 1"
               taskMsgWithActionAndReport "get" "m2" "c2" "Read 2"
               taskMsgWithActionAndReport "list" "m3" "c3" "Read 3" |]
        )
    check "mimocode: read-only task calls do not become fold anchors" (
        findFoldRangeFor Mimocode flat false |> Option.isNone)

let findFoldRangeForMimocodeRequiresThreeProgressBursts () =
    let flat =
        flatten (
            [| userMsg "u1" "start"
               taskMsgWithActionAndReport "list" "m1" "c1" "Read 1"
               taskMsgWithActionAndReport "start" "m2" "c2" "Work 1"
               taskMsgWithActionAndReport "get" "m3" "c3" "Read 2"
               taskMsgWithActionAndReport "done" "m4" "c4" "Work 2"
               userMsg "u2" "gap"
               taskMsgWithActionAndReport "block" "m5" "c5" "Work 3" |]
        )
    check "mimocode: two progress bursts still do not satisfy 3-anchor fold" (
        findFoldRangeFor Mimocode flat false |> Option.isNone)

let findFoldRangeForMimocodeUsesLastProgressCallInBurst () =
    let flat =
        flatten (
            [| userMsg "u1" "start"
               taskMsgWithActionAndReport "start" "m1" "c1" "Work 1"
               taskMsgWithActionAndReport "list" "m2" "c2" "Read 1"
               userMsg "u2" "gap"
               taskMsgWithActionAndReport "done" "m3" "c3" "Work 2"
               taskMsgWithActionAndReport "get" "m4" "c4" "Read 2"
               userMsg "u3" "gap"
               taskMsgWithActionAndReport "block" "m5" "c5" "Work 3"
               taskMsgWithActionAndReport "list" "m6" "c6" "Read 3" |]
        )
    check "mimocode: burst anchor uses last progress call, not trailing read-only call" (
        findFoldRangeFor Mimocode flat false
        |> Option.exists (fun range ->
            partCallID flat.[range.firstResult].part = "c1"
            && partCallID flat.[range.secondToLast].part = "c3"))

let projectMagicFolds () =
    let msgs =
        [| userMsg "u1" "start project"
           todoWriteMsg "m1" "c1" "Report 1"
           todoWriteMsg "m2" "c2" "Report 2"
           todoWriteMsg "m3" "c3" "Report 3" |]

    let backlog =
        [ backlogEntry 1 "Report 1"
          backlogEntry 2 "Report 2"
          backlogEntry 3 "Report 3" ]

    let r = projectMagic msgs backlog false "test"
    let allJson: string = Fable.Core.JS.JSON.stringify (r)
    check "magic fold: has prefix" (allJson.Contains(magicTodoPrefixPrefix))
    check "magic fold: has Report 1" (allJson.Contains("Report 1"))
    check "magic fold: latest report present" (allJson.Contains("Report 3"))

let projectMagicNoFold () =
    let msgs = [| todoWriteMsg "m1" "c1" "R1"; todoWriteMsg "m2" "c2" "R2" |]
    let backlog = [ backlogEntry 1 "R1"; backlogEntry 2 "R2" ]
    let r = projectMagic msgs backlog false "test"
    check "magic no fold: passthrough" (obj.ReferenceEquals(r, msgs))

let projectMagicForMimocodeUsesTask () =
    let msgs =
        [| userMsg "u1" "start"
           toolMsg "task" "m1" "c1" "Report 1"
           userMsg "u2" "gap"
           toolMsg "task" "m2" "c2" "Report 2"
           userMsg "u3" "gap"
           toolMsg "task" "m3" "c3" "Report 3a"
           toolMsg "task" "m4" "c4" "Report 3b" |]

    let backlog = replayBacklogFor Mimocode msgs
    let r = projectMagicFor Mimocode msgs backlog false "test"
    let allJson: string = Fable.Core.JS.JSON.stringify (r)
    check "mimocode project: has prefix" (allJson.Contains(magicTodoPrefixPrefix))
    check "mimocode project: has Report 1" (allJson.Contains("Report 1"))
    check "mimocode project: latest burst present" (allJson.Contains("Report 3a") && allJson.Contains("Report 3b"))
    check "mimocode project: does not rely on todowrite" (not (allJson.Contains("todowrite")))

let projectMagicHidesErrors () =
    let msgs =
        [| userMsg "u1" "start"
           todoWriteMsg "m1" "c1" "R1"
           todoWriteErrorMsg "me" "ce" "Validation failed"
           todoWriteMsg "m2" "c2" "R2"
           todoWriteMsg "m3" "c3" "R3" |]

    let backlog = [ backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3" ]
    let r = projectMagic msgs backlog false "test"
    let allJson: string = Fable.Core.JS.JSON.stringify (r)
    check "magic errors: error surfaced in notice" (allJson.Contains("Validation failed"))

let projectMagicDropsFoldedUserMessages () =
    let msgs =
        [| userMsg "u1" "start"
           todoWriteMsg "m1" "c1" "R1"
           userMsg "u2" "please fix this bug"
           todoWriteMsg "m2" "c2" "R2"
           todoWriteMsg "m3" "c3" "R3" |]

    let backlog = [ backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3" ]
    let r = projectMagic msgs backlog false "test"
    let allJson: string = Fable.Core.JS.JSON.stringify (r)
    check "magic fold: hides original folded users" (not (allJson.Contains("\"id\":\"u2\"")))
    check "magic fold: marks folded users as summary" (allJson.Contains("工作期间收到的用户消息"))
    check "magic fold: keeps folded user content in projection" (allJson.Contains("please fix this bug"))

let projectMagicKeepsReviewInFold () =
    let msgs =
        [| userMsg "u1" "start"
           todoWriteMsg "m1" "c1" "R1"
           reviewMsg "rv1" "cr1" "Review accepted the work"
           todoWriteMsg "m2" "c2" "R2"
           todoWriteMsg "m3" "c3" "R3" |]

    let backlog = [ backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3" ]
    let r = projectMagic msgs backlog false "test"
    let allJson: string = Fable.Core.JS.JSON.stringify (r)
    check "magic review: tool name kept" (allJson.Contains(magicReviewToolName))
    check "magic review: output kept" (allJson.Contains("Review accepted the work"))
    check "magic review: not fully folded away" (r.Length > 4)

let projectMagicPrefixUsesTodoTime () =
    let msgs =
        [| userMsg "u1" "start"
           timedTodoWriteMsg "m1" "c1" "R1" 111 222
           userMsg "u2" "please fix this bug"
           todoWriteMsg "m2" "c2" "R2"
           todoWriteMsg "m3" "c3" "R3" |]

    let backlog = [ backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3" ]
    let r = projectMagic msgs backlog false "test"
    let prefixInfo = messageInfo r.[0]
    let prefixTime = get prefixInfo "time"
    check "magic prefix: keeps folded todo created time" (unbox<int> (get prefixTime "created") = 111)
    check "magic prefix: keeps folded todo completed time" (unbox<int> (get prefixTime "completed") = 222)

let projectMagicPrefixStaysStableWhenGrowing () =
    let msgs3 =
        [| userMsg "u1" "start"
           userMsg "u2" "between 1 and 2"
           todoWriteMsg "m1" "c1" "R1"
           userMsg "u3" "between 2 and 3"
           todoWriteMsg "m2" "c2" "R2"
           userMsg "u4" "between 3 and 4"
           todoWriteMsg "m3" "c3" "R3"
           todoWriteMsg "m4" "c4" "R4" |]

    let backlog3 = [ backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3"; backlogEntry 4 "R4" ]
    let projected3 = projectMagic msgs3 backlog3 false "test"

    let msgs4 =
        [| userMsg "u1" "start"
           userMsg "u2" "between 1 and 2"
           todoWriteMsg "m1" "c1" "R1"
           userMsg "u3" "between 2 and 3"
           todoWriteMsg "m2" "c2" "R2"
           userMsg "u4" "between 3 and 4"
           todoWriteMsg "m3" "c3" "R3"
           userMsg "u5" "between 4 and 5"
           todoWriteMsg "m4" "c4" "R4"
           todoWriteMsg "m5" "c5" "R5" |]

    let backlog4 =
        [ backlogEntry 1 "R1"
          backlogEntry 2 "R2"
          backlogEntry 3 "R3"
          backlogEntry 4 "R4"
          backlogEntry 5 "R5" ]
    let projected4 = projectMagic msgs4 backlog4 false "test"

    let sharedPrefix3: string = Fable.Core.JS.JSON.stringify (projected3.[0..2])
    let sharedPrefix4: string = Fable.Core.JS.JSON.stringify (projected4.[0..2])
    check "magic prefix: stable growth keeps shared prefix JSON identical" (sharedPrefix3 = sharedPrefix4)

let magicSessionRefreshesBacklogForMimocode () =
    let session = MagicSession(Mimocode)
    let first = [| toolMsg "task" "m1" "c1" "R1" |]
    let second = [| toolMsg "task" "m1" "c1" "R1"; toolMsg "task" "m2" "c2" "R2" |]
    let _ = session.GetOrRebuildBacklog("test", first)
    let backlog = session.GetOrRebuildBacklog("test", second)
    check "mimocode session: consecutive tasks count as one backlog entry" (backlog.Length = 1)

let magicSessionRefreshesBacklog () =
    let session = MagicSession(Opencode)
    let first = [| todoWriteMsg "m1" "c1" "R1" |]
    let second = [| todoWriteMsg "m1" "c1" "R1"; todoWriteMsg "m2" "c2" "R2" |]
    let _ = session.GetOrRebuildBacklog("test", first)
    let backlog = session.GetOrRebuildBacklog("test", second)
    check "magic session: rebuilds stale backlog" (backlog.Length = 2)

let buildBacklogTextTest () =
    let text: string = buildBacklogText [ backlogEntry 1 "Did work" ] []
    check "backlog text: has report" (text.Contains("Did work"))
    let empty: string = buildBacklogText [] []
    check "backlog text: empty message" (empty.Contains("已完成工作报告"))

let run () =
    replayBacklogOpencodeDoesNotMergeConsecutiveTodoWrite ()
    replayBacklogTest ()
    replayEmpty ()
    replaySkipsEmpty ()
    replayBacklogForMimocodeUsesTask ()
    replayBacklogForMimocodeIgnoresActor ()
    mimocodeTaskReportCaptureRoundTrip ()
    replayBacklogForMimocodeMergesConsecutiveWorkReports ()
    replayBacklogForMimocodeMergesConsecutiveTaskBurst ()
    replayBacklogForMimocodeSplitsBurstsOnGap ()
    findFoldRangeTest ()
    findFoldRangeOpencodePerCallMimocodePerBurst ()
    findFoldRangeForMimocodeIgnoresReadOnlyTaskCalls ()
    findFoldRangeForMimocodeRequiresThreeProgressBursts ()
    findFoldRangeForMimocodeUsesLastProgressCallInBurst ()
    projectMagicFolds ()
    projectMagicNoFold ()
    projectMagicForMimocodeUsesTask ()
    projectMagicHidesErrors ()
    projectMagicDropsFoldedUserMessages ()
    projectMagicKeepsReviewInFold ()
    projectMagicPrefixUsesTodoTime ()
    projectMagicPrefixStaysStableWhenGrowing ()
    magicSessionRefreshesBacklog ()
    magicSessionRefreshesBacklogForMimocode ()
    buildBacklogTextTest ()
