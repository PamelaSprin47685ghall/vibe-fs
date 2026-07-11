module Wanxiangshu.Kernel.EventLog.ReviewLoopFold

open Wanxiangshu.Kernel.EventLog.Types

type ReviewLoopFold =
    | Inactive
    | Active of task: string

let initial = Inactive

let isLoopActive (s: ReviewLoopFold) : bool =
    match s with
    | Inactive -> false
    | Active _ -> true

let activeTask (s: ReviewLoopFold) : string option =
    match s with
    | Inactive -> None
    | Active task -> Some task

let private payloadTask (e: WanEvent) : string option =
    e.Payload
    |> Map.tryFind "task"
    |> Option.bind (fun t -> if t = "" then None else Some t)

let private payloadVerdict (e: WanEvent) : string option = e.Payload |> Map.tryFind "verdict"

let foldEvent (current: ReviewLoopFold) (e: WanEvent) : ReviewLoopFold =
    match e.Kind with
    | k when k = eventKindLoopActivated ->
        match payloadTask e with
        | Some task -> Active task
        | None -> current
    | k when k = eventKindLoopCancelled -> Inactive
    | k when k = eventKindReviewVerdict ->
        match payloadVerdict e with
        | Some v when isEndVerdict v -> Inactive
        | _ ->
            match current with
            | Active _ -> current
            | Inactive -> Inactive
    | _ -> current

let foldEvents (events: WanEvent list) : ReviewLoopFold = List.fold foldEvent initial events
