module Wanxiangshu.Kernel.Subsession.Draining

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.Rules

let decide state cmd =
    match state, cmd with

    | Draining(ctx, started, _, _), CancelRequested ->
        Ok(beginAbort ctx (Started started) UserRequested FinishCancelled)
    | Draining(ctx, started, _, _), TurnDeadlineExpired tid when tid = started.Plan.TurnId ->
        Ok(
            beginAbort
                ctx
                (Started started)
                TurnDeadline
                (FinishFailed(InfrastructureFailure "turn deadline expired while draining"))
        )
    | Draining _, TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | Draining _, (DispatchAccepted _ | DispatchRejected _) -> Ok(noChange StaleTurnMarker)
    | Draining(ctx, started, _, _), SessionClosed -> Ok(closeActive ctx started.Plan.TurnId)
    | Draining _, cmd when isIllegalWhenDraining cmd -> illegal (stateName state) (cmdName cmd)
    | _ -> illegal (stateName state) (cmdName cmd)
