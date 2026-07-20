module Wanxiangshu.Kernel.Subsession.Draining

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.Rules

let decide (nowMs: int64) state cmd =
    match state, cmd with

    | Draining(ctx, started, _, _, turnDeadlineAtMs), CancelRequested ->
        Ok(beginAbort nowMs ctx (Started started) UserRequested FinishCancelled)
    | Draining(ctx, started, _, _, turnDeadlineAtMs), TurnDeadlineExpired tid when tid = started.Plan.TurnId ->
        Ok(
            beginAbort
                nowMs
                ctx
                (Started started)
                TurnDeadline
                (FinishFailed(InfrastructureFailure "turn deadline expired while draining"))
        )
    | Draining _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | Draining _, (DispatchAccepted _ | DispatchRejected _) -> Ok(noChange StaleTurnMarker)
    | Draining(ctx, started, _, _, turnDeadlineAtMs), SessionClosed -> Ok(closeActive ctx started.Plan.TurnId)
    | Draining _, cmd when isIllegalWhenDraining cmd -> illegal (stateName state) (cmdName cmd)
    | _ -> illegal (stateName state) (cmdName cmd)
