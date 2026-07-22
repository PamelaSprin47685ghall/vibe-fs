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

/// Inspect a raw JS object and classify it as quiescent / still-running /
/// unknown. Used by the OMP host.
let detectStatus (obj: obj) : QuiescenceStatus option =
    if isNullish obj then
        None
    else
        match detectBool (get obj "isIdle") Stopped StillRunning with
        | Some status -> Some status
        | None ->
            match detectBool (get obj "isBusy") StillRunning Stopped with
            | Some status -> Some status
            | None ->
                match detectString (get obj "status") with
                | Some status -> Some status
                | None ->
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
