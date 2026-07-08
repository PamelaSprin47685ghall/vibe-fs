module Wanxiangshu.Kernel.Wanxiangzhen.SquadTask

type SquadTaskStatus =
    | Pending
    | Running
    | Submitted
    | Merged
    | Done
    | Cancelled

type SquadTask =
    { Id: string
      Title: string
      Description: string
      DependsOn: string list
      Status: SquadTaskStatus
      WorktreePath: string option
      BranchName: string option
      SlavePid: int option
      LastHeartbeatAt: string option
      MergedSha: string option
      CreatedAt: string
      UpdatedAt: string }

let taskIdPrefix = "squad-"

let statusToString (s: SquadTaskStatus) : string =
    match s with
    | Pending -> "pending"
    | Running -> "running"
    | Submitted -> "submitted"
    | Merged -> "merged"
    | Done -> "done"
    | Cancelled -> "cancelled"

let statusFromString (s: string) : SquadTaskStatus option =
    match s with
    | "pending" -> Some Pending
    | "running" -> Some Running
    | "submitted" -> Some Submitted
    | "merged" -> Some Merged
    | "done" -> Some Done
    | "cancelled" -> Some Cancelled
    | _ -> None

let isTerminal (s: SquadTaskStatus) : bool =
    match s with
    | Merged
    | Done
    | Cancelled -> true
    | _ -> false

let canTransition (from: SquadTaskStatus) (toStatus: SquadTaskStatus) : bool =
    match from, toStatus with
    | Pending, Running
    | Pending, Cancelled -> true
    | Running, Submitted
    | Running, Done
    | Running, Cancelled -> true
    | Submitted, Merged
    | Submitted, Running
    | Submitted, Done
    | Submitted, Cancelled -> true
    | _ -> false

let tryWithStatus (task: SquadTask) (newStatus: SquadTaskStatus) (now: string) : Result<SquadTask, string> =
    if canTransition task.Status newStatus then
        Ok
            { task with
                Status = newStatus
                UpdatedAt = now }
    else
        Error(
            sprintf
                "Invalid transition %s → %s for task %s"
                (statusToString task.Status)
                (statusToString newStatus)
                task.Id
        )

let withStatus (task: SquadTask) (newStatus: SquadTaskStatus) (now: string) : SquadTask =
    match tryWithStatus task newStatus now with
    | Ok t -> t
    | Error msg -> failwith msg

let withReconciledStatus (task: SquadTask) (newStatus: SquadTaskStatus) (now: string) : SquadTask =
    { task with
        Status = newStatus
        UpdatedAt = now }

let create (id: string) (title: string) (description: string) (dependsOn: string list) (now: string) : SquadTask =
    { Id = id
      Title = title
      Description = description
      DependsOn = dependsOn
      Status = Pending
      WorktreePath = None
      BranchName = None
      SlavePid = None
      LastHeartbeatAt = None
      MergedSha = None
      CreatedAt = now
      UpdatedAt = now }
