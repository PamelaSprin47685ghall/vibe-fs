module Wanxiangshu.Shell.Wanxiangzhen.SquadEventWanCodec

open Thoth.Json
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Shell.EventLogCodec

type private TaskRow =
    { task_id: string
      title: string
      description: string
      depends_on: string list option }

let private payloadStr (payload: Map<string, string>) (key: string) : string =
    payload |> Map.tryFind key |> Option.defaultValue ""

let private payloadOpt (payload: Map<string, string>) (key: string) : string option =
    payload
    |> Map.tryFind key
    |> Option.bind (fun s -> if s = "" then None else Some s)

let private encodeTasks (tasks: TaskItem list) : string =
    let rows =
        tasks
        |> List.map (fun item ->
            { task_id = item.taskId
              title = item.title
              description = item.description
              depends_on = if item.dependsOn = [] then None else Some item.dependsOn })

    Encode.Auto.toString (4, rows)

let private decodeTasks (json: string) : TaskItem list =
    if json = "" then
        []
    else
        match Decode.Auto.fromString<TaskRow list> json with
        | Ok rows ->
            rows
            |> List.map (fun r ->
                { taskId = r.task_id
                  title = r.title
                  description = r.description
                  dependsOn = r.depends_on |> Option.defaultValue [] })
        | Error _ -> []

let squadEventToWanEvent (at: string) (e: SquadEvent) : WanEvent =
    let kind = eventTypeName e
    let session = eventSessionId e

    let payload =
        match e with
        | SquadCreated(_, req) -> Map [ "requirement", req ]
        | TasksCreated(_, tasks) -> Map [ "tasksJson", encodeTasks tasks ]
        | TaskStarted(_, tid, wt, branch) -> Map [ "task_id", tid; "worktree_path", wt; "branch_name", branch ]
        | TaskSubmitted(_, tid, sha) -> Map [ "task_id", tid; "commit_sha", sha ]
        | TaskMerged(_, tid, sha) -> Map [ "task_id", tid; "master_sha", sha ]
        | TaskDone(_, tid, merged) -> Map [ "task_id", tid; "merged", string merged ]
        | TaskError(_, tid, err) -> Map [ "task_id", tid; "error", err ]
        | SquadCancelled _ -> Map.empty

    buildEvent session kind payload at

let trySquadEventFromWanEvent (e: WanEvent) : SquadEvent option =
    if not (isSquadEventKind e.Kind) then
        None
    else
        let sid = e.Session
        let p = e.Payload

        match e.Kind with
        | k when k = eventKindSquadCreated -> Some(SquadCreated(sid, payloadStr p "requirement"))
        | k when k = eventKindTasksCreated ->
            let tasks = decodeTasks (payloadStr p "tasksJson")
            Some(TasksCreated(sid, tasks))
        | k when k = eventKindTaskStarted ->
            Some(TaskStarted(sid, payloadStr p "task_id", payloadStr p "worktree_path", payloadStr p "branch_name"))
        | k when k = eventKindTaskSubmitted ->
            Some(TaskSubmitted(sid, payloadStr p "task_id", payloadStr p "commit_sha"))
        | k when k = eventKindTaskMerged -> Some(TaskMerged(sid, payloadStr p "task_id", payloadStr p "master_sha"))
        | k when k = eventKindTaskDone ->
            let merged =
                match payloadOpt p "merged" with
                | Some s when s.ToLower() = "true" -> true
                | _ -> false

            Some(TaskDone(sid, payloadStr p "task_id", merged))
        | k when k = eventKindTaskError -> Some(TaskError(sid, payloadStr p "task_id", payloadStr p "error"))
        | k when k = eventKindSquadCancelled -> Some(SquadCancelled sid)
        | _ -> None
