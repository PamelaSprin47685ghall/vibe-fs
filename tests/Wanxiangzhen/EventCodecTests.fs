module Wanxiangshu.Tests.Wanxiangzhen.EventCodecTests

open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Runtime.Wanxiangzhen.SquadEventDisplayCodec
open Wanxiangshu.Runtime.Tooling.ToolOutputSquadToml
open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat

let entries () : (string * (unit -> unit)) list =
    [ ("Codec.SquadCreated toTomlView and encodeEvent",
       fun () ->
           let ev = SquadEvent.SquadCreated("s1", "decompose this requirement")
           let view: SquadEventTomlView = toTomlView ev
           equal "squad_created" view.eventKind
           equal "s1" view.sessionId
           isNone view.taskId
           isNone view.commitSha
           equal (SquadEventPayload.SquadCreated(Some "decompose this requirement")) view.payload

           let encoded = encodeEvent ev
           checkBare (encoded.Contains "event_kind = \"squad_created\"")
           checkBare (encoded.Contains "session_id = \"s1\""))

      ("Codec.TasksCreated toTomlView and encodeEvent",
       fun () ->
           let tasks =
               [ { taskId = "a1"
                   title = "title1"
                   description = "desc1"
                   dependsOn = [] }
                 { taskId = "a2"
                   title = "title2"
                   description = "desc2"
                   dependsOn = [ "a1" ] } ]

           let ev = SquadEvent.TasksCreated("s1", tasks)
           let view: SquadEventTomlView = toTomlView ev
           equal "tasks_created" view.eventKind
           equal "s1" view.sessionId
           equal SquadEventPayload.TasksCreated view.payload

           let encoded = encodeEvent ev
           checkBare (encoded.Contains "event_kind = \"tasks_created\""))

      ("Codec.TaskStarted toTomlView includes worktree and branch in payload",
       fun () ->
           let ev = SquadEvent.TaskStarted("s1", "t1", "/wt/path", "branch-x")
           let view: SquadEventTomlView = toTomlView ev
           equal "task_started" view.eventKind
           equal (Some "t1") view.taskId
           isNone view.commitSha
           equal (SquadEventPayload.TaskStarted("t1", "/wt/path", "branch-x")) view.payload

           let encoded = encodeEvent ev
           checkBare (encoded.Contains "task_id = \"t1\""))

      ("Codec.TaskSubmitted toTomlView includes commit_sha",
       fun () ->
           let ev = SquadEvent.TaskSubmitted("s1", "t1", "abc123sha")
           let view: SquadEventTomlView = toTomlView ev
           equal "task_submitted" view.eventKind
           equal (Some "t1") view.taskId
           equal (Some "abc123sha") view.commitSha
           equal (SquadEventPayload.TaskSubmitted("t1", "abc123sha")) view.payload

           let encoded = encodeEvent ev
           checkBare (encoded.Contains "commit_sha = \"abc123sha\""))

      ("Codec.TaskMerged toTomlView includes master_sha as commit_sha",
       fun () ->
           let ev = SquadEvent.TaskMerged("s1", "t1", "sha999")
           let view: SquadEventTomlView = toTomlView ev
           equal "task_merged" view.eventKind
           equal (Some "t1") view.taskId
           equal (Some "sha999") view.commitSha
           equal (SquadEventPayload.TaskMerged("t1", "sha999")) view.payload

           let encoded = encodeEvent ev
           checkBare (encoded.Contains "commit_sha = \"sha999\""))

      ("Codec.TaskDone toTomlView",
       fun () ->
           let ev = SquadEvent.TaskDone("s1", "t1", true)
           let view: SquadEventTomlView = toTomlView ev
           equal "task_done" view.eventKind
           equal (Some "t1") view.taskId
           isNone view.commitSha
           equal (SquadEventPayload.TaskDone("t1", true)) view.payload)

      ("Codec.TaskError toTomlView",
       fun () ->
           let ev = SquadEvent.TaskError("s1", "t1", "git fail")
           let view: SquadEventTomlView = toTomlView ev
           equal "task_error" view.eventKind
           equal (Some "t1") view.taskId
           equal (SquadEventPayload.TaskError("t1", "git fail")) view.payload)

      ("Codec.SquadCancelled toTomlView",
       fun () ->
           let ev = SquadEvent.SquadCancelled "s1"
           let view: SquadEventTomlView = toTomlView ev
           equal "squad_cancelled" view.eventKind
           equal "s1" view.sessionId
           equal SquadEventPayload.SquadCancelled view.payload)

      ("Codec.encodeEvents produces combined TOML blocks",
       fun () ->
           let ev1 = SquadEvent.SquadCreated("s1", "req one")
           let ev2 = SquadEvent.TaskMerged("s1", "t1", "sha999")
           let encoded = encodeEvents [ ev1; ev2 ]
           checkBare (encoded.Contains "event_kind = \"squad_created\"")
           checkBare (encoded.Contains "event_kind = \"task_merged\"")) ]
