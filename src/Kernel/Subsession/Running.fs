module Wanxiangshu.Kernel.Subsession.Running

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.Rules

let decide (nowMs: int64) state cmd =
    match state, cmd with
    | Running(ctx, started, _, turnDeadlineAtMs), CancelRequested ->
        Ok(beginAbort nowMs ctx (Started started) UserRequested FinishCancelled)
    | Running(ctx, started, _, turnDeadlineAtMs), TurnDeadlineExpired tid when tid = started.Plan.TurnId ->
        Ok(
            beginAbort
                nowMs
                ctx
                (Started started)
                TurnDeadline
                (FinishFailed(InfrastructureFailure "turn deadline expired"))
        )
    | Running _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | Running _, (DispatchAccepted _ | DispatchRejected _) -> Ok(noChange StaleTurnMarker)
    | Running(ctx, started, _, turnDeadlineAtMs), SessionClosed -> Ok(closeActive ctx started.Plan.TurnId)
    | Running _, cmd when isIllegalWhenRunning cmd -> illegal (stateName state) (cmdName cmd)

    | _ -> illegal (stateName state) (cmdName cmd)
