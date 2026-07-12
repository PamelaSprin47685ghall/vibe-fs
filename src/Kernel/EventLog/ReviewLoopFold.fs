module Wanxiangshu.Kernel.EventLog.ReviewLoopFold

open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.EventLog.ReviewVerdictWire

type ActiveReviewLoopInfo =
    { task: string
      reviewLoopId: string
      currentRound: int
      latestVerdict: string option
      latestFeedback: string option }

type ReviewLoopFold =
    | Inactive
    | Active of ActiveReviewLoopInfo

let initial = Inactive

let isLoopActive (s: ReviewLoopFold) : bool =
    match s with
    | Inactive -> false
    | Active _ -> true

let activeTask (s: ReviewLoopFold) : string option =
    match s with
    | Inactive -> None
    | Active info -> Some info.task

let private payloadTask (e: WanEvent) : string option =
    e.Payload
    |> Map.tryFind "task"
    |> Option.bind (fun t -> if t = "" then None else Some t)

let private payloadVerdict (e: WanEvent) : string option = e.Payload |> Map.tryFind "verdict"

let foldEvent (current: ReviewLoopFold) (e: WanEvent) : ReviewLoopFold =
    match e.Kind with
    | k when k = eventKindLoopActivated ->
        match payloadTask e with
        | Some task ->
            let loopId = e.At

            Active
                { task = task
                  reviewLoopId = loopId
                  currentRound = 1
                  latestVerdict = None
                  latestFeedback = None }
        | None -> current
    | k when k = eventKindLoopCancelled -> Inactive
    | k when k = eventKindReviewVerdict ->
        match payloadVerdict e with
        | Some v when isEndVerdict v -> Inactive
        | Some v ->
            match current with
            | Active info ->
                let fb = e.Payload |> Map.tryFind "feedback" |> Option.filter (fun s -> s <> "")

                Active
                    { info with
                        currentRound = info.currentRound + 1
                        latestVerdict = Some v
                        latestFeedback = fb }
            | Inactive -> Inactive
        | None -> current
    | _ -> current

let foldEvents (events: WanEvent list) : ReviewLoopFold = List.fold foldEvent initial events
