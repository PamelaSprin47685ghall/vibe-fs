module Wanxiangshu.Tests.Wanxiangzhen.EventReplayTests

open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat

let private t = Wanxiangshu.Kernel.Wanxiangzhen.SquadTask.create

let entries () : (string * (unit -> unit)) list =
    [ ("Event.fold SquadCreated",
       fun () ->
           let d = empty "" ""
           let d2 = foldEvent d (SquadCreated("s1", "req"))
           equal "s1" d2.SessionId
           equal "req" d2.RootRequirement)

      ("Event.fold TasksCreated",
       fun () ->
           let d = empty "s1" ""

           let d2 =
               foldEvent
                   d
                   (TasksCreated(
                       "s1",
                       [ { taskId = "a"
                           title = "t"
                           description = "d"
                           dependsOn = [] }
                         { taskId = "b"
                           title = "u"
                           description = "e"
                           dependsOn = [ "a" ] } ]
                   ))

           equal Pending (findTask "a" d2).Value.Status
           equal Pending (findTask "b" d2).Value.Status
           equal [ "a" ] (findTask "b" d2).Value.DependsOn)

      ("Event.fold TaskStarted",
       fun () ->
           let d = empty "s1" "" |> addTask (t "a" "t" "d" [] "now")
           let d2 = foldEvent d (TaskStarted("s1", "a", "/wt", "a"))
           equal Running (findTask "a" d2).Value.Status
           equal (Some "/wt") (findTask "a" d2).Value.WorktreePath)

      ("Event.fold TaskSubmitted",
       fun () ->
           let d = empty "s1" "" |> addTask (t "a" "t" "d" [] "now")
           let d2 = foldEvent d (TaskSubmitted("s1", "a", "sha"))
           equal Submitted (findTask "a" d2).Value.Status)

      ("Event.fold TaskMerged",
       fun () ->
           let d = empty "s1" "" |> addTask (t "a" "t" "d" [] "now")
           let d2 = foldEvent d (TaskMerged("s1", "a", "sha123"))
           equal Merged (findTask "a" d2).Value.Status
           equal (Some "sha123") (findTask "a" d2).Value.MergedSha)

      ("Event.fold TaskDone",
       fun () ->
           let d = empty "s1" "" |> addTask (t "a" "t" "d" [] "now")
           let d2 = foldEvent d (TaskDone("s1", "a", false))
           equal Done (findTask "a" d2).Value.Status)

      ("Event.fold SquadCancelled",
       fun () ->
           let d =
               empty "s1" ""
               |> addTask (t "a" "t" "d" [] "now")
               |> addTask (
                   { t "b" "t" "d" [] "now" with
                       Status = Merged }
               )

           let d2 = foldEvent d (SquadCancelled "s1")
           equal Cancelled (findTask "a" d2).Value.Status
           equal Merged (findTask "b" d2).Value.Status)

      ("Event.foldEvents sequence",
       fun () ->
           let events =
               [ TasksCreated(
                     "s1",
                     [ { taskId = "a"
                         title = "t"
                         description = "d"
                         dependsOn = [] } ]
                 )
                 TaskStarted("s1", "a", "/wt", "a")
                 TaskSubmitted("s1", "a", "sha")
                 TaskMerged("s1", "a", "sha") ]

           let d = foldEvents events (empty "s1" "")
           equal Merged (findTask "a" d).Value.Status) ]
