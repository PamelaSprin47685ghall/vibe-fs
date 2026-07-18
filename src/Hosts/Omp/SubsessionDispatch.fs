module Wanxiangshu.Hosts.Omp.SubsessionDispatch

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn

let dispatch
    (session: obj)
    (_agent: string)
    (_sessionId: SessionId)
    (turn: TurnPlan)
    : JS.Promise<Result<HostStartReceipt, DispatchFailure>> =
    promise {
        try
            // OMP session.prompt accepts the raw prompt string.
            let! _response = unbox<JS.Promise<obj>> (session?prompt (turn.Prompt))
            return Ok OrderedTurnMarkerObserved
        with ex ->
            return
                Error(
                    DispatchFailure.HostAcceptanceUnknown
                        { ErrorName = "DispatchFailed"
                          DomainError = None
                          Message = ex.Message
                          StatusCode = None
                          IsRetryable = Some true }
                )
    }

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
