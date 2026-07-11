module Wanxiangshu.Tests.BacklogReplaySpecsFold

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.BacklogMessageBuilders
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.BacklogProjection

let findFoldRangeTest () =
    let flat =
        flatten
            [ userMsg "u1" "start"
              todoWriteMsg "m1" "c1" "R1"
              todoWriteMsg "m2" "c2" "R2"
              todoWriteMsg "m3" "c3" "R3" ]

    match findFoldRange flat FoldStrategy.FoldAfterSecond with
    | None -> check "fold range: found" false
    | Some r -> check "fold range: secondToLast > first" (r.secondToLast > r.firstResult)

let findFoldRangeOpencodePerCallMimicodePerBurst () =
    let flatOpencode =
        flatten
            [ userMsg "u1" "start"
              todoWriteMsg "m1" "c1" "R1"
              todoWriteMsg "m2" "c2" "R2"
              todoWriteMsg "m3" "c3" "R3" ]

    check
        "opencode: three todowrites enable fold"
        (findFoldRangeFor Opencode flatOpencode FoldStrategy.FoldAfterSecond
         |> Option.isSome)

    let flatMimo =
        flatten
            [ userMsg "u1" "start"
              taskMsgWithReport "m1" "c1" "A"
              taskMsgWithReport "m2" "c2" "B"
              taskMsgWithReport "m3" "c3" "C" ]

    check
        "mimocode: three task calls now enable fold like opencode"
        (findFoldRangeFor Mimocode flatMimo FoldStrategy.FoldAfterSecond |> Option.isSome)

let findFoldRangeForMimocodeIgnoresReadOnlyTaskCalls () =
    let flat =
        flatten
            [ userMsg "u1" "start"
              taskMsgWithActionAndReport "list" "m1" "c1" "Read 1"
              taskMsgWithActionAndReport "get" "m2" "c2" "Read 2"
              taskMsgWithActionAndReport "list" "m3" "c3" "Read 3" ]

    check
        "mimocode: read-only concept removed; task calls are anchors"
        (findFoldRangeFor Mimocode flat FoldStrategy.FoldAfterSecond |> Option.isSome)

let findFoldRangeForMimocodeRequiresThreeProgressBursts () =
    let flat =
        flatten
            [ userMsg "u1" "start"
              taskMsgWithActionAndReport "list" "m1" "c1" "Read 1"
              taskMsgWithActionAndReport "start" "m2" "c2" "Work 1"
              taskMsgWithActionAndReport "get" "m3" "c3" "Read 2"
              taskMsgWithActionAndReport "done" "m4" "c4" "Work 2"
              userMsg "u2" "gap"
              taskMsgWithActionAndReport "block" "m5" "c5" "Work 3" ]

    check
        "mimocode: three task calls satisfy 3-anchor fold"
        (findFoldRangeFor Mimocode flat FoldStrategy.FoldAfterSecond |> Option.isSome)

let findFoldRangeForMimocodeUsesLastProgressCallInBurst () =
    let flat =
        flatten
            [ userMsg "u1" "start"
              taskMsgWithActionAndReport "start" "m1" "c1" "Work 1"
              taskMsgWithActionAndReport "list" "m2" "c2" "Read 1"
              userMsg "u2" "gap"
              taskMsgWithActionAndReport "done" "m3" "c3" "Work 2"
              taskMsgWithActionAndReport "get" "m4" "c4" "Read 2"
              userMsg "u3" "gap"
              taskMsgWithActionAndReport "block" "m5" "c5" "Work 3"
              taskMsgWithActionAndReport "list" "m6" "c6" "Read 3" ]

    check
        "mimocode: first and second-to-last anchors follow raw call order"
        (findFoldRangeFor Mimocode flat FoldStrategy.FoldAfterSecond
         |> Option.exists (fun range ->
             partCallID flat.[range.firstResult].part = "c1"
             && partCallID flat.[range.secondToLast].part = "c5"))

let findFoldRangeForMimocodeAssistantTextKeepsBurst () =
    let flat =
        flatten
            [ userMsg "u1" "start"
              taskMsgWithActionAndReport "start" "m1" "c1" "Work 1"
              assistantTextMsg "a1" "thinking aloud"
              taskMsgWithActionAndReport "done" "m2" "c2" "Work 2"
              userMsg "u2" "gap"
              taskMsgWithActionAndReport "start" "m3" "c3" "Work 3"
              assistantTextMsg "a2" "more thinking"
              taskMsgWithActionAndReport "done" "m4" "c4" "Work 4"
              userMsg "u3" "gap"
              taskMsgWithActionAndReport "done" "m5" "c5" "Work 5" ]

    match findFoldRangeFor Mimocode flat FoldStrategy.FoldAfterSecond with
    | None -> check "mimocode assistant text in burst: fold found" false
    | Some range ->
        check
            "mimocode assistant text in burst: first anchor stays first task call"
            (partCallID flat.[range.firstResult].part = "c1")
