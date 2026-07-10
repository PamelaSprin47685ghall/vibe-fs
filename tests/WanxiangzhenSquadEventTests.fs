module Wanxiangshu.Tests.WanxiangzhenSquadEventTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Kernel.Wanxiangzhen.SquadUpdateIdAssign
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorSquadUpdate

let foldEventsMergedChain () =
    let events =
        [ TasksCreated("s1", [ { taskId = "a"; title = "t"; description = "d"; dependsOn = [] } ])
          TaskStarted("s1", "a", "/wt", "a")
          TaskSubmitted("s1", "a", "sha")
          TaskMerged("s1", "a", "sha") ]

    let d = foldEvents events (empty "s1" "")
    equal "merged chain" Merged (findTask "a" d).Value.Status

let squadCreatedSetsRequirement () =
    let d = foldEvent (empty "s1" "") (SquadCreated("s1", "build feature"))
    equal "requirement" "build feature" d.RootRequirement

let squadCancelledMarksNonTerminalCancelled () =
    let d0 =
        foldEvents
            [ TasksCreated("s1", [ { taskId = "a"; title = "t"; description = "d"; dependsOn = [] } ])
              TaskStarted("s1", "a", "/wt", "b") ]
            (empty "s1" "")

    let d1 = foldEvent d0 (SquadCancelled "s1")
    equal "running->cancelled" Cancelled (findTask "a" d1).Value.Status

let assignTaskIdsReusesExplicitId () =
    let gen: IdGen =
        { Generate = fun () -> "squad-x"
          RefExists = fun (_: string) -> false }

    match assignTaskIds Set.empty [ (Some "a", "t", "d", []) ] gen with
    | Ok [ item ] -> equal "explicit id" "a" item.taskId
    | _ -> failwith "expected Ok"

let detectCycleFindsABA () =
    match detectCycle [ ("a", [ "b" ]); ("b", [ "a" ]) ] with
    | Some _ -> ()
    | None -> failwith "expected cycle"

let validateTasksArrayShapeRejectsMissingArray () =
    let ev = createObj [ "type", box "tasks_created" ]

    match validateTasksArrayShape [ ev ] with
    | Some(InvalidInput _) -> ()
    | _ -> failwith "expected InvalidInput"

let validateTaskFieldsRejectsEmptyTitle () =
    let task = createObj [ "taskId", box "x"; "title", box ""; "description", box "d" ]
    let ev = createObj [ "type", box "tasks_created"; "tasks", box [| task |] ]

    match validateTaskFields [ ev ] with
    | Some(InvalidInput msg) when msg.Contains "x" -> ()
    | _ -> failwith "expected InvalidInput for empty title"

let run () =
    foldEventsMergedChain ()
    squadCreatedSetsRequirement ()
    squadCancelledMarksNonTerminalCancelled ()
    assignTaskIdsReusesExplicitId ()
    detectCycleFindsABA ()
    validateTasksArrayShapeRejectsMissingArray ()
    validateTaskFieldsRejectsEmptyTitle ()
