module VibeFs.Tests.MagicTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Dyn
open VibeFs.Opencode.MagicTypes
open VibeFs.Opencode.MagicProjector
open VibeFs.Opencode.MagicReplay
open VibeFs.Kernel.MessageDecoder
open VibeFs.Kernel.SyntheticIds
open VibeFs.Opencode.MagicSession

let private userMsg (id: string) (text: string) : obj =
    createObj [
        "info", box (createObj [ "id", box id; "role", box "user"; "sessionID", box "test" ])
        "parts", box [| box {| ``type`` = "text"; text = text |} |]
    ]

let private timedTodoWriteMsg (id: string) (callID: string) (report: string) (created: int) (completed: int) : obj =
    createObj [
        "info", box (createObj [
            "id", box id
            "role", box "assistant"
            "sessionID", box "test"
            "time", box (createObj [ "created", box created; "completed", box completed ])
        ])
        "parts", box [| createObj [
            "type", box "tool"; "tool", box magicTodoToolName; "callID", box callID
            "state", box (createObj [
                "status", box "completed"
                "input", box (createObj [ "completedWorkReport", box report; "todos", box [||] ])
                "output", box "Todos updated."
            ])
        ] |]
    ]

let private todoWriteMsg (id: string) (callID: string) (report: string) : obj =
    createObj [
        "info", box (createObj [ "id", box id; "role", box "assistant"; "sessionID", box "test" ])
        "parts", box [| createObj [
            "type", box "tool"; "tool", box magicTodoToolName; "callID", box callID
            "state", box (createObj [
                "status", box "completed"
                "input", box (createObj [ "completedWorkReport", box report; "todos", box [||] ])
                "output", box "Todos updated."
            ])
        ] |]
    ]

let private todoWriteErrorMsg (id: string) (callID: string) (errorText: string) : obj =
    createObj [
        "info", box (createObj [ "id", box id; "role", box "assistant"; "sessionID", box "test" ])
        "parts", box [| createObj [
            "type", box "tool"; "tool", box magicTodoToolName; "callID", box callID
            "state", box (createObj [ "status", box "error"; "input", box (createObj []); "error", box errorText ])
        ] |]
    ]

let private reviewMsg (id: string) (callID: string) (output: string) : obj =
    createObj [
        "info", box (createObj [ "id", box id; "role", box "assistant"; "sessionID", box "test" ])
        "parts", box [| createObj [
            "type", box "tool"; "tool", box magicReviewToolName; "callID", box callID
            "state", box (createObj [
                "status", box "completed"
                "input", box (createObj [ "review", box "looks good" ])
                "output", box output
            ])
        ] |]
    ]

let private backlogEntry (seq: int) (report: string) : BacklogEntry =
    { sequence = seq; timestamp = ""; report = report }

let replayBacklogTest () =
    let msgs = [|
        todoWriteMsg "m1" "c1" "Implemented parser"
        todoWriteMsg "m2" "c2" "Fixed critical bug"
    |]
    let backlog = replayBacklog msgs
    check "replay: backlog count" (backlog.Length = 2)
    check "replay: entry 1 report" (backlog.[0].report = "Implemented parser")
    check "replay: entry 2 report" (backlog.[1].report = "Fixed critical bug")

let replayEmpty () =
    let backlog = replayBacklog [||]
    check "replay empty: no backlog" (backlog.IsEmpty)

let replaySkipsEmpty () =
    let msgs = [|
        todoWriteMsg "m1" "c1" "Report A"
        todoWriteMsg "m2" "c2" ""
    |]
    let backlog = replayBacklog msgs
    check "replay skips empty: only 1" (backlog.Length = 1)

let findFoldRangeTest () =
    let flat = VibeFs.Kernel.PartStream.flatten([|
        userMsg "u1" "start"
        todoWriteMsg "m1" "c1" "R1"
        todoWriteMsg "m2" "c2" "R2"
        todoWriteMsg "m3" "c3" "R3"
    |])
    match findFoldRange flat false with
    | None -> check "fold range: found" false
    | Some r -> check "fold range: secondToLast > first" (r.secondToLast > r.firstResult)

let projectMagicFolds () =
    let msgs = [|
        userMsg "u1" "start project"
        todoWriteMsg "m1" "c1" "Report 1"
        todoWriteMsg "m2" "c2" "Report 2"
        todoWriteMsg "m3" "c3" "Report 3"
    |]
    let backlog = [backlogEntry 1 "Report 1"; backlogEntry 2 "Report 2"; backlogEntry 3 "Report 3"]
    let r = projectMagic msgs backlog false "test"
    let allJson : string = Fable.Core.JS.JSON.stringify(r)
    check "magic fold: has prefix" (allJson.Contains(magicTodoPrefixPrefix))
    check "magic fold: has Report 1" (allJson.Contains("Report 1"))
    check "magic fold: latest report present" (allJson.Contains("Report 3"))

let projectMagicNoFold () =
    let msgs = [|
        todoWriteMsg "m1" "c1" "R1"
        todoWriteMsg "m2" "c2" "R2"
    |]
    let backlog = [backlogEntry 1 "R1"; backlogEntry 2 "R2"]
    let r = projectMagic msgs backlog false "test"
    check "magic no fold: passthrough" (obj.ReferenceEquals(r, msgs))

let projectMagicHidesErrors () =
    let msgs = [|
        userMsg "u1" "start"
        todoWriteMsg "m1" "c1" "R1"
        todoWriteErrorMsg "me" "ce" "Validation failed"
        todoWriteMsg "m2" "c2" "R2"
        todoWriteMsg "m3" "c3" "R3"
    |]
    let backlog = [backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3"]
    let r = projectMagic msgs backlog false "test"
    let allJson : string = Fable.Core.JS.JSON.stringify(r)
    check "magic errors: error surfaced in notice" (allJson.Contains("Validation failed"))

let projectMagicDropsFoldedUserMessages () =
    let msgs = [|
        userMsg "u1" "start"
        todoWriteMsg "m1" "c1" "R1"
        userMsg "u2" "please fix this bug"
        todoWriteMsg "m2" "c2" "R2"
        todoWriteMsg "m3" "c3" "R3"
    |]
    let backlog = [backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3"]
    let r = projectMagic msgs backlog false "test"
    let allJson : string = Fable.Core.JS.JSON.stringify(r)
    check "magic fold: hides original folded users" (r.Length = 4)
    check "magic fold: marks folded users as summary" (allJson.Contains("工作期间收到的用户消息摘要"))
    check "magic fold: keeps folded user content in projection" (allJson.Contains("please fix this bug"))

let projectMagicKeepsReviewInFold () =
    let msgs = [|
        userMsg "u1" "start"
        todoWriteMsg "m1" "c1" "R1"
        reviewMsg "rv1" "cr1" "Review accepted the work"
        todoWriteMsg "m2" "c2" "R2"
        todoWriteMsg "m3" "c3" "R3"
    |]
    let backlog = [backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3"]
    let r = projectMagic msgs backlog false "test"
    let allJson : string = Fable.Core.JS.JSON.stringify(r)
    check "magic review: tool name kept" (allJson.Contains(magicReviewToolName))
    check "magic review: output kept" (allJson.Contains("Review accepted the work"))
    check "magic review: not fully folded away" (r.Length > 4)

let projectMagicPrefixUsesTodoTime () =
    let msgs = [|
        userMsg "u1" "start"
        timedTodoWriteMsg "m1" "c1" "R1" 111 222
        userMsg "u2" "please fix this bug"
        todoWriteMsg "m2" "c2" "R2"
        todoWriteMsg "m3" "c3" "R3"
    |]
    let backlog = [backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3"]
    let r = projectMagic msgs backlog false "test"
    let prefixInfo = messageInfo r.[0]
    let prefixTime = get prefixInfo "time"
    check "magic prefix: keeps folded todo created time" (unbox<int> (get prefixTime "created") = 111)
    check "magic prefix: keeps folded todo completed time" (unbox<int> (get prefixTime "completed") = 222)

let magicSessionRefreshesBacklog () =
    let session = MagicSession()
    let first = [| todoWriteMsg "m1" "c1" "R1" |]
    let second = [| todoWriteMsg "m1" "c1" "R1"; todoWriteMsg "m2" "c2" "R2" |]
    let _ = session.GetOrRebuildBacklog("test", first)
    let backlog = session.GetOrRebuildBacklog("test", second)
    check "magic session: rebuilds stale backlog" (backlog.Length = 2)

let buildBacklogTextTest () =
    let text : string = buildBacklogText [backlogEntry 1 "Did work"] []
    check "backlog text: has report" (text.Contains("Did work"))
    let empty : string = buildBacklogText [] []
    check "backlog text: empty message" (empty.Contains("Backlog"))

let run () =
    replayBacklogTest ()
    replayEmpty ()
    replaySkipsEmpty ()
    findFoldRangeTest ()
    projectMagicFolds ()
    projectMagicNoFold ()
    projectMagicHidesErrors ()
    projectMagicDropsFoldedUserMessages ()
    projectMagicKeepsReviewInFold ()
    projectMagicPrefixUsesTodoTime ()
    magicSessionRefreshesBacklog ()
    buildBacklogTextTest ()
