module Wanxiangshu.Tests.BacklogProjectionSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.BacklogMessageBuilders

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Message
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.BacklogProjection
open Wanxiangshu.Opencode.BacklogSession
open Wanxiangshu.Opencode.MessagingCodec
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.RuntimeScope


let projectBacklogFolds () =
    let msgs =
        [ userMsg "u1" "start project"
          todoWriteMsg "m1" "c1" "Report 1"
          todoWriteMsg "m2" "c2" "Report 2"
          todoWriteMsg "m3" "c3" "Report 3" ]
    let backlog = [ backlogEntry 1 "Report 1"; backlogEntry 2 "Report 2"; backlogEntry 3 "Report 3" ]
    let r = projectBacklog msgs backlog false "test"
    let allJson: string = Fable.Core.JS.JSON.stringify (encodeMessages r)
    check "backlog fold: has prefix" (allJson.Contains(backlogPrefixIdPrefix))
    check "backlog fold: has Report 1" (allJson.Contains("Report 1"))
    check "backlog fold: latest report present" (allJson.Contains("Report 3"))

let projectBacklogNoFold () =
    let msgs = [ todoWriteMsg "m1" "c1" "R1"; todoWriteMsg "m2" "c2" "R2" ]
    let backlog = [ backlogEntry 1 "R1"; backlogEntry 2 "R2" ]
    let r = projectBacklog msgs backlog false "test"
    check "magic no fold: passthrough" (obj.ReferenceEquals(r, msgs))

let projectBacklogForMimocodeUsesTask () =
    let msgs =
        [ userMsg "u1" "start"
          toolMsg "task" "m1" "c1" "Report 1"
          userMsg "u2" "gap"
          toolMsg "task" "m2" "c2" "Report 2"
          userMsg "u3" "gap"
          toolMsg "task" "m3" "c3" "Report 3a"
          toolMsg "task" "m4" "c4" "Report 3b" ]
    let backlog = replayBacklogFor Mimocode (create ()) msgs
    let r = projectBacklogFor Mimocode msgs backlog false "test"
    let allJson: string = Fable.Core.JS.JSON.stringify (encodeMessages r)
    check "mimocode project: has prefix" (allJson.Contains(backlogPrefixIdPrefix))
    check "mimocode project: has Report 1" (allJson.Contains("Report 1"))
    check "mimocode project: latest task reports present" (allJson.Contains("Report 3a") && allJson.Contains("Report 3b"))
    check "mimocode project: task is the exposed todo alias" (allJson.Contains("task"))

let projectBacklogHidesErrors () =
    let msgs =
        [ userMsg "u1" "start"
          todoWriteMsg "m1" "c1" "R1"
          todoWriteErrorMsg "me" "ce" "Validation failed"
          todoWriteMsg "m2" "c2" "R2"
          todoWriteMsg "m3" "c3" "R3" ]
    let backlog = [ backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3" ]
    let r = projectBacklog msgs backlog false "test"
    let allJson: string = Fable.Core.JS.JSON.stringify (encodeMessages r)
    check "magic errors: error surfaced in notice" (allJson.Contains("Validation failed"))

let projectBacklogDropsFoldedUserMessages () =
    let msgs =
        [ userMsg "u1" "start"
          todoWriteMsg "m1" "c1" "R1"
          userMsg "u2" "please fix this bug"
          todoWriteMsg "m2" "c2" "R2"
          todoWriteMsg "m3" "c3" "R3" ]
    let backlog = [ backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3" ]
    let r = projectBacklog msgs backlog false "test"
    let allJson: string = Fable.Core.JS.JSON.stringify (encodeMessages r)
    let text = visibleText r
    check "backlog fold: hides original folded users" (not (allJson.Contains("\"id\":\"u2\"")))
    check "backlog fold: uses front matter projection summary" (
        text.StartsWith("---\n")
        && text.Contains("- user_message")
        && text.Contains("aha_moments:"))
    check "backlog fold: keeps folded user content in projection" (text.Contains("please fix this bug"))

let projectBacklogKeepsReviewInFold () =
    let msgs =
        [ userMsg "u1" "start"
          todoWriteMsg "m1" "c1" "R1"
          reviewMsg "rv1" "cr1" "Review accepted the work"
          todoWriteMsg "m2" "c2" "R2"
          todoWriteMsg "m3" "c3" "R3" ]
    let backlog = [ backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3" ]
    let r = projectBacklog msgs backlog false "test"
    let allJson: string = Fable.Core.JS.JSON.stringify (encodeMessages r)
    check "magic review: tool name kept" (allJson.Contains(reviewToolName))
    check "magic review: output kept" (allJson.Contains("Review accepted the work"))
    check "magic review: not fully folded away" (r.Length > 4)

let projectBacklogPrefixUsesTodoTime () =
    let msgs =
        [ userMsg "u1" "start"
          timedTodoWriteMsg "m1" "c1" "R1" 111 222
          userMsg "u2" "please fix this bug"
          todoWriteMsg "m2" "c2" "R2"
          todoWriteMsg "m3" "c3" "R3" ]
    let backlog = [ backlogEntry 1 "R1"; backlogEntry 2 "R2"; backlogEntry 3 "R3" ]
    let r = projectBacklog msgs backlog false "test"
    let prefixTime = r.[0].info.time
    check "backlog prefix: keeps folded todo created time" (unbox<int> (get prefixTime "created") = 111)
    check "backlog prefix: keeps folded todo completed time" (unbox<int> (get prefixTime "completed") = 222)

let projectBacklogPrefixStaysStableWhenGrowing () =
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
    let projected3 = projectBacklog msgs3 backlog3 false "test"

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
    let projected4 = projectBacklog msgs4 backlog4 false "test"

    let sharedPrefix3: string = Fable.Core.JS.JSON.stringify (encodeMessages projected3.[0..2])
    let sharedPrefix4: string = Fable.Core.JS.JSON.stringify (encodeMessages projected4.[0..2])
    check "backlog prefix: stable growth keeps shared prefix JSON identical" (sharedPrefix3 = sharedPrefix4)

let buildBacklogTextTest () =
    let text: string = buildBacklogText [ backlogEntry 1 "Did work" ] []
    check "backlog text: has front matter" (text.StartsWith("---\n"))
    check "backlog text: stores reports in front matter" (
        text.Contains("user_message")
        && text.Contains("aha_moments")
        && text.Contains("Completed work from folded turns. File changes are already on disk.")
        && text.Contains("Did work"))
    let empty: string = buildBacklogText [] []
    check "backlog text: empty front matter" (empty.StartsWith("---\n"))
    check "backlog text: empty body still explains folded work" (
        empty.Contains("Completed work from folded turns. File changes are already on disk."))

let buildBacklogTextWithErrorTest () =
    let text = buildBacklogTextWithError [ backlogEntry 1 "Did work" ] [] (Some "bad todo state")
    check "backlog text with error: error moves to body" (
        text.Contains("Last todo write error: bad todo state")
        && not (text.Contains("last_todo_write_error:")))

let buildCompactionAnchorPromptEmptyReturnsEmpty () =
    equal "empty backlog + empty anchors returns empty" "" (buildCompactionAnchorPrompt [] (fun () -> []))

let buildCompactionAnchorPromptWithoutBacklogSkipsEmptyArrayBlock () =
    let prompt =
        buildCompactionAnchorPrompt [] (fun () -> [ "---\ntask: investigate-loop\n---" ])
    check "anchor-only prompt starts with real anchor block" (prompt.StartsWith "---\ntask: investigate-loop")
    check "anchor-only prompt does not prepend empty array block" (not (prompt.Contains "---\n[]\n---"))

let buildCompactionAnchorPromptOrdersHistoryAnchorsBeforeBacklog () =
    let prompt =
        buildCompactionAnchorPrompt
            [ backlogEntry 1 "progress so far" ]
            (fun () -> [ "---\ncommand: with-review\ntask: ship feature\n---" ])
    let anchorIdx = prompt.IndexOf "command: with-review"
    let backlogIdx = prompt.IndexOf "aha_moments"
    check "history task/command anchor precedes backlog projection" (anchorIdx >= 0 && backlogIdx >= 0 && anchorIdx < backlogIdx)
    check "anchor prompt starts with the earliest history block" (prompt.StartsWith "---\ncommand: with-review")
