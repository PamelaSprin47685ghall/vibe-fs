module Wanxiangshu.Hosts.Omp.SubsessionQuiescence

open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Dyn

let private detectBool (v: obj) (trueVal: QuiescenceStatus) (falseVal: QuiescenceStatus) : QuiescenceStatus option =
    if not (isNullish v) && typeIs v "boolean" then
        if unbox<bool> v then Some trueVal else Some falseVal
    else
        None

let private detectString (v: obj) : QuiescenceStatus option =
    if not (isNullish v) && typeIs v "string" then
        match (string v).ToLowerInvariant() with
        | "idle"
        | "closed"
        | "completed"
        | "done"
        | "stopped" -> Some Stopped
        | "busy"
        | "running"
        | "active"
        | "pending" -> Some StillRunning
        | _ -> None
    else
        None

let private detectTurns (obj: obj) : QuiescenceStatus option =
    let activeTurn = get obj "activeTurn"
    let runningTurn = get obj "runningTurn"
    let currentTurn = get obj "currentTurn"

    if
        not (isNullish activeTurn)
        || not (isNullish runningTurn)
        || not (isNullish currentTurn)
    then
        Some StillRunning
    else
        None

/// Inspect a raw JS object and classify it as quiescent / still-running /
/// unknown. Used by the OMP host.
let detectStatus (obj: obj) : QuiescenceStatus option =
    if isNullish obj then
        None
    else
        [ (fun () -> detectBool (get obj "isIdle") Stopped StillRunning)
          (fun () -> detectBool (get obj "isBusy") StillRunning Stopped)
          (fun () -> detectString (get obj "status"))
          (fun () -> detectTurns obj) ]
        |> List.tryPick (fun f -> f ())
