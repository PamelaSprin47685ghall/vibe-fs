module Wanxiangshu.Kernel.Wanxiangzhen.SquadTaskTransition

open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask

type TransitionPolicy =
    | Strict
    | ReplayFact

let applyStatus (policy: TransitionPolicy) (task: SquadTask) (newStatus: SquadTaskStatus) (now: string) : SquadTask =
    match policy with
    | Strict ->
        match tryWithStatus task newStatus now with
        | Ok t -> t
        | Error msg -> failwith msg
    | ReplayFact -> withReconciledStatus task newStatus now

let applyStatusOption
    (policy: TransitionPolicy)
    (task: SquadTask)
    (newStatus: SquadTaskStatus)
    (now: string)
    : SquadTask option =
    match policy with
    | Strict ->
        match tryWithStatus task newStatus now with
        | Ok t -> Some t
        | Error _ -> None
    | ReplayFact -> Some(withReconciledStatus task newStatus now)
