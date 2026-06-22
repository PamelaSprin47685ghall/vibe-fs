module VibeFs.Tests.MagicTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.MagicCore
open VibeFs.Kernel.MagicProjection
open VibeFs.Opencode.MagicTodo
open VibeFs.Opencode.MessagingCodec

let private mkInfo (id: string) (role: Role) : MessageInfo =
    { id = id; sessionID = "test"; role = role; agent = ""; isError = false
      toolName = ""; details = null; time = null }

let private mkState (status: string) (output: string) (input: obj) : ToolState =
    { status = status; output = output; error = ""; input = input; operationAction = "" }

let private userMsg (id: string) (text: string) : Message =
    { info = mkInfo id User; parts = [ TextPart text ]; source = Native; raw = null }

let private timedTodoWriteMsg (id: string) (callID: string) (report: string) (created: int) (completed: int) : Message =
    let input = box (createObj [ "completedWorkReport", box report; "todos", box [||] ])
    let time = box (createObj [ "created", box created; "completed", box completed ])
    { info = { mkInfo id Assistant with time = time }
      parts = [ ToolPart(magicTodoToolName, callID, Some (mkState "completed" "Todos updated." input), null) ]
      source = Native; raw = null }

let private todoWriteMsg (id: string) (callID: string) (report: string) : Message =
    let input = box (createObj [ "completedWorkReport", box report; "todos", box [||] ])
    { info = mkInfo id Assistant
      parts = [ ToolPart(magicTodoToolName, callID, Some (mkState "completed" "Todos updated." input), null) ]
      source = Native; raw = null }

let private todoWriteErrorMsg (id: string) (callID: string) (errorText: string) : Message =
    { info = mkInfo id Assistant
      parts = [ ToolPart(magicTodoToolName, callID, Some ({ status = "error"; output = ""; error = errorText; input = box (createObj []); operationAction = "" }), null) ]
      source = Native; raw = null }

let private assistantTextMsg (id: string) (text: string) : Message =
    { info = mkInfo id Assistant; parts = [ TextPart text ]; source = Native; raw = null }

let private reasoningMsg (id: string) (text: string) : Message =
    { info = mkInfo id Assistant
      parts = [ RawPart (box (createObj [ "type", box "reasoning"; "text", box text ])) ]
      source = Native; raw = null }

let private reviewMsg (id: string) (callID: string) (output: string) : Message =
    let input = box (createObj [ "review", box "looks good" ])
    { info = mkInfo id Assistant
      parts = [ ToolPart(magicReviewToolName, callID, Some (mkState "completed" output input), null) ]
      source = Native; raw = null }

let private taskMsgWithReport (id: string) (callID: string) (report: string) : Message =
    let input = box (createObj [ "operation", box (createObj [ "action", box "list" ]); "completedWorkReport", box report ])
    { info = mkInfo id Assistant
      parts = [ ToolPart("task", callID, Some ({ status = "completed"; output = "ok"; error = ""; input = input; operationAction = "list" }), null) ]
      source = Native; raw = null }

let private taskMsgWithActionAndReport (action: string) (id: string) (callID: string) (report: string) : Message =
    let input = box (createObj [ "operation", box (createObj [ "action", box action ]); "completedWorkReport", box report ])
    { info = mkInfo id Assistant
      parts = [ ToolPart("task", callID, Some ({ status = "completed"; output = "ok"; error = ""; input = input; operationAction = action }), null) ]
      source = Native; raw = null }

let private taskCreateMsg (id: string) (callID: string) : Message =
    let input = box (createObj [ "operation", box (createObj [ "action", box "create"; "summary", box "ignored" ]) ])
    { info = mkInfo id Assistant
      parts = [ ToolPart("task", callID, Some ({ status = "completed"; output = "ok"; error = ""; input = input; operationAction = "create" }), null) ]
      source = Native; raw = null }

let private toolMsg (toolName: string) (id: string) (callID: string) (report: string) : Message =
    let input = box (createObj [ "completedWorkReport", box report; "todos", box [||] ])
    { info = mkInfo id Assistant
      parts = [ ToolPart(toolName, callID, Some (mkState "completed" "Todos updated." input), null) ]
      source = Native; raw = null }

let private backlogEntry (seq: int) (report: string) : BacklogEntry =
    { sequence = seq; timestamp = ""; report = report }

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

let projectMagicFolds () =
    let msgs =
        [ userMsg "u1" "start project"
          todoWriteMsg "m1" "c1" "Report 1"
          todoWriteMsg "m2" "c2" "Report 2"
          todoWriteMsg "m3" "c3" "Report 3" ]
    let backlog = [ backlogEntry 1 "Report 1"; backlogEntry 2 "Report 2"; backlogEntry 3 "Report 3" ]
    let r = projectMagic msgs backlog false "test"
    let allJson: string = Fable.Core.JS.JSON.stringify (encodeMessages r)
    check "magic fold: has prefix" (allJson.Contains(magicTodoPrefixPrefix))
    check "magic fold: has Report 1" (allJson.Contains("Report 1"))
    check "magic fold: latest report present" (allJson.Contains("Report 3"))

let projectMagicNoFold () =
    let msgs = [ todoWriteMsg "m1" "c1" "R1"; todoWriteMsg "m2" "c2" "R2" ]
    let backlog = [ backlogEntry 1 "R1"; backlogEntry 2 "R2" ]
    let r = projectMagic msgs backlog false "test"
    check "magic no fold: passthrough" (obj.ReferenceEquals(r, msgs))

let projectMagicForMimocodeUsesTask () =
    let msgs =
        [ userMsg "u1" "start"
          toolMsg "task" "m1" "c1" "Report 1"
          userMsg "u2" "gap"
          toolMsg "task" "m2" "c2" "Report 2"
          userMsg "u3" "gap"
          toolMsg "task" "m3" "c3" "Report 3a"
          toolMsg "task" "m4" "c4" "Report 3b" ]
    let backlog = replayBacklogFor Mimocode msgs
    let r = projectMagicFor Mimocode msgs backlog false "test"
    let allJson: string = Fable.Core.JS.JSON.stringify (encodeMessages r)
    check "mimocode project: has prefix" (allJson.Contains(magicTodoPrefixPrefix))
    check "mimocode project: has Report 1" (allJson.Contains("Report 1"))
    check "mimocode project: latest task reports present" (allJson.Contains("Report 3a") && allJson.Contains("Report 3b"))
    check "mimocode project: task is the exposed todo alias" (allJson.Contains("task"))

let projectMagicHidesErrors () =
    let msgs =
        [ userMsg "u1" "start"
          todoWriteMsg "m1" "c1" "R1"
          todoWriteErrorMsg "me" "ce" "Validation failed"
          todoWriteMsg "m2" "c2" "R2"
          todoWriteMsg "m3" "c3" "R3" ]
    let backlog = [ backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3" ]
    let r = projectMagic msgs backlog false "test"
    let allJson: string = Fable.Core.JS.JSON.stringify (encodeMessages r)
    check "magic errors: error surfaced in notice" (allJson.Contains("Validation failed"))

let projectMagicDropsFoldedUserMessages () =
    let msgs =
        [ userMsg "u1" "start"
          todoWriteMsg "m1" "c1" "R1"
          userMsg "u2" "please fix this bug"
          todoWriteMsg "m2" "c2" "R2"
          todoWriteMsg "m3" "c3" "R3" ]
    let backlog = [ backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3" ]
    let r = projectMagic msgs backlog false "test"
    let allJson: string = Fable.Core.JS.JSON.stringify (encodeMessages r)
    check "magic fold: hides original folded users" (not (allJson.Contains("\"id\":\"u2\"")))
    check "magic fold: marks folded users as summary" (allJson.Contains("工作期间收到的用户消息"))
    check "magic fold: keeps folded user content in projection" (allJson.Contains("please fix this bug"))

let projectMagicKeepsReviewInFold () =
    let msgs =
        [ userMsg "u1" "start"
          todoWriteMsg "m1" "c1" "R1"
          reviewMsg "rv1" "cr1" "Review accepted the work"
          todoWriteMsg "m2" "c2" "R2"
          todoWriteMsg "m3" "c3" "R3" ]
    let backlog = [ backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3" ]
    let r = projectMagic msgs backlog false "test"
    let allJson: string = Fable.Core.JS.JSON.stringify (encodeMessages r)
    check "magic review: tool name kept" (allJson.Contains(magicReviewToolName))
    check "magic review: output kept" (allJson.Contains("Review accepted the work"))
    check "magic review: not fully folded away" (r.Length > 4)

let projectMagicPrefixUsesTodoTime () =
    let msgs =
        [ userMsg "u1" "start"
          timedTodoWriteMsg "m1" "c1" "R1" 111 222
          userMsg "u2" "please fix this bug"
          todoWriteMsg "m2" "c2" "R2"
          todoWriteMsg "m3" "c3" "R3" ]
    let backlog = [ backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3" ]
    let r = projectMagic msgs backlog false "test"
    let prefixTime = r.[0].info.time
    check "magic prefix: keeps folded todo created time" (unbox<int> (get prefixTime "created") = 111)
    check "magic prefix: keeps folded todo completed time" (unbox<int> (get prefixTime "completed") = 222)

let projectMagicPrefixStaysStableWhenGrowing () =
    let msgs3 =
        [ userMsg "u1" "start"
          userMsg "u2" "between 1 and 2"
          todoWriteMsg "m1" "c1" "R1"
          userMsg "u3" "between 2 and 3"
          todoWriteMsg "m2" "c2" "R2"
          userMsg "u4" "between 3 and 4"
          todoWriteMsg "m3" "c3" "R3"
          todoWriteMsg "m4" "c4" "R4" ]
    let backlog3 = [ backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3"; backlogEntry 4 "R4" ]
    let projected3 = projectMagic msgs3 backlog3 false "test"

    let msgs4 =
        [ userMsg "u1" "start"
          userMsg "u2" "between 1 and 2"
          todoWriteMsg "m1" "c1" "R1"
          userMsg "u3" "between 2 and 3"
          todoWriteMsg "m2" "c2" "R2"
          userMsg "u4" "between 3 and 4"
          todoWriteMsg "m3" "c3" "R3"
          userMsg "u5" "between 4 and 5"
          todoWriteMsg "m4" "c4" "R4"
          todoWriteMsg "m5" "c5" "R5" ]
    let backlog4 =
        [ backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3"; backlogEntry 4 "R4"; backlogEntry 5 "R5" ]
    let projected4 = projectMagic msgs4 backlog4 false "test"

    let sharedPrefix3: string = Fable.Core.JS.JSON.stringify (encodeMessages projected3.[0..2])
    let sharedPrefix4: string = Fable.Core.JS.JSON.stringify (encodeMessages projected4.[0..2])
    check "magic prefix: stable growth keeps shared prefix JSON identical" (sharedPrefix3 = sharedPrefix4)

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
    magicSessionCaptureRoundTrip ()
    magicSessionShareReportTableAcrossInstances ()
    magicSessionRestoresMimocodeReportDuringBacklogRebuild ()
    replayBacklogForMimocodeMergesConsecutiveWorkReports ()
    replayBacklogForMimocodeMergesConsecutiveTaskBurst ()
    replayBacklogForMimocodeSplitsBurstsOnGap ()
    replayBacklogForMimocodeIgnoresAssistantTextBetweenTasks ()
    replayBacklogForMimocodeIgnoresReasoningBetweenTasks ()
    replayBacklogForMimocodeSplitsOnOtherToolCall ()
    findFoldRangeTest ()
    findFoldRangeOpencodePerCallMimicodePerBurst ()
    findFoldRangeForMimocodeIgnoresReadOnlyTaskCalls ()
    findFoldRangeForMimocodeRequiresThreeProgressBursts ()
    findFoldRangeForMimocodeUsesLastProgressCallInBurst ()
    findFoldRangeForMimocodeAssistantTextKeepsBurst ()
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
