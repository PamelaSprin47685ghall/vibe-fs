module Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent

open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTaskTransition

type TaskItem =
    { taskId: string
      title: string
      description: string
      dependsOn: string list }

type SquadEvent =
    | SquadCreated of sessionId: string * requirement: string
    | TasksCreated of sessionId: string * tasks: TaskItem list
    | TaskStarted of sessionId: string * taskId: string * worktreePath: string * branchName: string
    | TaskSubmitted of sessionId: string * taskId: string * commitSha: string
    | TaskMerged of sessionId: string * taskId: string * masterSha: string
    | TaskDone of sessionId: string * taskId: string * merged: bool
    | TaskError of sessionId: string * taskId: string * error: string
    | SquadCancelled of sessionId: string

let eventSessionId (e: SquadEvent) : string =
    match e with
    | SquadCreated(sid, _)
    | TasksCreated(sid, _)
    | TaskStarted(sid, _, _, _)
    | TaskSubmitted(sid, _, _)
    | TaskMerged(sid, _, _)
    | TaskDone(sid, _, _)
    | TaskError(sid, _, _)
    | SquadCancelled sid -> sid

let eventTypeName (e: SquadEvent) : string =
    match e with
    | SquadCreated _ -> "squad_created"
    | TasksCreated _ -> "tasks_created"
    | TaskStarted _ -> "task_started"
    | TaskSubmitted _ -> "task_submitted"
    | TaskMerged _ -> "task_merged"
    | TaskDone _ -> "task_done"
    | TaskError _ -> "task_error"
    | SquadCancelled _ -> "squad_cancelled"

let eventTypeNameFromString (s: string) : string option =
    match s with
    | "squad_created"
    | "tasks_created"
    | "task_started"
    | "task_submitted"
    | "task_merged"
    | "task_done"
    | "task_error"
    | "squad_cancelled" -> Some s
    | _ -> None

let isSquadEventKind (kind: string) : bool =
    eventTypeNameFromString kind |> Option.isSome

let eventProse (e: SquadEvent) : string =
    match e with
    | SquadCreated(_, _) ->
        "Decompose this requirement into independently executable tasks. Each task should:\n\
         - Be completable within a single git worktree\n\
         - Have clear completion criteria\n\
         - Minimize file conflicts with other tasks\n\
         Express dependencies via dependsOn (dependency must be merged first).\n\
         Call the squad_update tool with one tasks_created event carrying a tasks[] array."
    | TasksCreated(_, tasks) ->
        let count = List.length tasks

        sprintf
            "%d tasks created. Nothing needs to be done. The scheduler will start them as dependencies are met."
            count
    | TaskStarted(_, _, _, _) -> "Task started in worktree. Nothing needs to be done."
    | TaskSubmitted(_, _, _) -> "Task submitted for fast-forward check. Nothing needs to be done."
    | TaskMerged(_, _, sha) -> sprintf "Task merged into master @ %s. Nothing needs to be done." sha
    | TaskDone(_, _, merged) ->
        if merged then
            "Task slave exited after successful merge."
        else
            "Task slave exited (not merged). DAG continues."
    | TaskError(_, _, err) -> sprintf "Git error for task: %s" err
    | SquadCancelled _ -> "Squad session cancelled by user. Remaining tasks marked cancelled."

let private replayAt = ""

let private mapTask (tid: string) (f: SquadTask -> SquadTask) (dag: Dag) : Dag = dag |> updateTask tid f

let foldEvent (dag: Dag) (e: SquadEvent) : Dag =
    match e with
    | SquadCreated(sid, req) ->
        { dag with
            SessionId = sid
            RootRequirement = req }
    | TasksCreated(_, tasks) ->
        tasks
        |> List.fold
            (fun d item ->
                let t = create item.taskId item.title item.description item.dependsOn ""
                addTask t d)
            dag
    | TaskStarted(_, tid, wt, branch) ->
        mapTask
            tid
            (fun t ->
                applyStatus ReplayFact t Running replayAt
                |> fun t' ->
                    { t' with
                        WorktreePath = Some wt
                        BranchName = Some branch })
            dag
    | TaskSubmitted(_, tid, _) -> mapTask tid (fun t -> applyStatus ReplayFact t Submitted replayAt) dag
    | TaskMerged(_, tid, sha) ->
        mapTask
            tid
            (fun t ->
                applyStatus ReplayFact t Merged replayAt
                |> fun t' -> { t' with MergedSha = Some sha })
            dag
    | TaskDone(_, tid, _) -> mapTask tid (fun t -> applyStatus ReplayFact t Done replayAt) dag
    | TaskError _ -> dag
    | SquadCancelled _ ->
        { dag with
            Tasks =
                dag.Tasks
                |> Map.map (fun _ t ->
                    if isTerminal t.Status then
                        t
                    else
                        applyStatus ReplayFact t Cancelled replayAt) }

let foldEvents (events: SquadEvent list) (dag: Dag) : Dag = List.fold foldEvent dag events
