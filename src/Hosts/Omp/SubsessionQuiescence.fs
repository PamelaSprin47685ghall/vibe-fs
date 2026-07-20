module Wanxiangshu.Hosts.Omp.SubsessionQuiescence
open Wanxiangshu.Runtime

open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Dyn

/// Inspect a raw JS object and classify it as quiescent / still-running /
/// unknown.  Used by both the OpenCode adapter and the OMP host.
let detectStatus (obj: obj) : QuiescenceStatus option =
    if Dyn.isNullish obj then
        None
    else
        let isIdleVal = Dyn.get obj "isIdle"

        if not (Dyn.isNullish isIdleVal) && Dyn.typeIs isIdleVal "boolean" then
            if unbox<bool> isIdleVal then
                Some Stopped
            else
                Some StillRunning
        else
            let isBusyVal = Dyn.get obj "isBusy"

            if not (Dyn.isNullish isBusyVal) && Dyn.typeIs isBusyVal "boolean" then
                if unbox<bool> isBusyVal then
                    Some StillRunning
                else
                    Some Stopped
            else
                let statusVal = Dyn.get obj "status"

                if not (Dyn.isNullish statusVal) && Dyn.typeIs statusVal "string" then
                    let status = (string statusVal).ToLowerInvariant()

                    match status with
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
                    let activeTurn = Dyn.get obj "activeTurn"
                    let runningTurn = Dyn.get obj "runningTurn"
                    let currentTurn = Dyn.get obj "currentTurn"

                    if
                        (not (Dyn.isNullish activeTurn))
                        || (not (Dyn.isNullish runningTurn))
                        || (not (Dyn.isNullish currentTurn))
                    then
                        Some StillRunning
                    else
                        None
