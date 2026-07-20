module Wanxiangshu.Kernel.Subsession.Abort

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.Rules

let private decideIssuingAbort (nowMs: int64) ctx turn abortCtx idleObserved abortDeadlineAtMs cmd =
    match cmd with
    | AbortConfirmed tid when tid = activeTurnId turn -> Ok(applyAfterAbort nowMs ctx turn abortCtx)
    | AbortConfirmed _ -> Ok(noChange StaleTurnMarker)
    | AbortHostAccepted tid when tid = activeTurnId turn ->
        if idleObserved then
            Ok(
                decided
                    (ReconcilingAbortSettle(ctx, turn, abortCtx, abortDeadlineAtMs))
                    []
                    [ QuerySessionQuiescence(ctx.SessionId, tid) ]
            )
        else
            Ok(decided (AwaitingAbortSettle(ctx, turn, abortCtx, abortDeadlineAtMs)) [] [])
    | AbortHostAccepted _ -> Ok(noChange StaleTurnMarker)
    | AbortRequestFailed _ -> Ok(noChange AbortInProgress)
    | DispatchAccepted(tid, receipt) when tid = activeTurnId turn ->
        match turn with
        | NotYetStarted plan ->
            Ok(
                decided
                    (IssuingAbort(
                        ctx,
                        Started { Plan = plan; StartReceipt = receipt },
                        abortCtx,
                        idleObserved,
                        abortDeadlineAtMs
                    ))
                    [ TurnStarted
                          { RunId = ctx.RunId
                            TurnId = tid
                            Receipt = receipt } ]
                    []
            )
        | Started _ -> Ok(noChange StaleTurnMarker)
    | DispatchAccepted _
    | DispatchRejected _ -> Ok(noChange StaleTurnMarker)
    | AbortDeadlineExpired tid when tid = activeTurnId turn ->
        let res = Failed(InfrastructureFailure "abort deadline expired")

        Ok(
            decided
                (Poisoned(AbortDidNotSettle tid))
                [ SessionPoisoned(ctx.SessionId, AbortDidNotSettle tid)
                  TurnFinished(tid, TurnInfrastructureFailed "abort deadline expired")
                  RunFinished(ctx.RunId, res) ]
                [ CompleteCaller(ctx.RunId, res) ]
        )
    | AbortDeadlineExpired _
    | CancelRequested
    | TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | SessionClosed -> Ok(closeActive ctx (activeTurnId turn))
    | _ -> illegal "IssuingAbort" (cmdName cmd)

let private decideAwaitingAbort (nowMs: int64) ctx turn abortCtx abortDeadlineAtMs cmd =
    match cmd with
    | AbortConfirmed tid when tid = activeTurnId turn -> Ok(applyAfterAbort nowMs ctx turn abortCtx)
    | AbortConfirmed _ -> Ok(noChange StaleTurnMarker)
    | AbortHostAccepted _
    | AbortRequestFailed _ -> Ok(noChange AbortInProgress)
    | AbortDeadlineExpired tid when tid = activeTurnId turn ->
        let res = Failed(InfrastructureFailure "abort deadline expired")

        Ok(
            decided
                (Poisoned(AbortDidNotSettle tid))
                [ SessionPoisoned(ctx.SessionId, AbortDidNotSettle tid)
                  TurnFinished(tid, TurnInfrastructureFailed "abort deadline expired")
                  RunFinished(ctx.RunId, res) ]
                [ CompleteCaller(ctx.RunId, res) ]
        )
    | AbortDeadlineExpired _
    | CancelRequested
    | TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | DispatchAccepted _
    | DispatchRejected _ -> Ok(noChange StaleTurnMarker)
    | SessionClosed -> Ok(closeActive ctx (activeTurnId turn))
    | _ -> illegal "AwaitingAbortSettle" (cmdName cmd)

let private decideReconcilingAbort (nowMs: int64) ctx turn abortCtx abortDeadlineAtMs cmd =
    match cmd with
    | SessionQuiescenceResolved status ->
        let tid = activeTurnId turn

        match status with
        | Stopped -> Ok(applyAfterAbort nowMs ctx turn abortCtx)
        | StillRunning -> Ok(decided (AwaitingAbortSettle(ctx, turn, abortCtx, abortDeadlineAtMs)) [] [])
        | StopUnknown ->
            let res = Failed(InfrastructureFailure "abort did not settle")

            Ok(
                decided
                    (Poisoned(AbortDidNotSettle tid))
                    [ SessionPoisoned(ctx.SessionId, AbortDidNotSettle tid)
                      TurnFinished(tid, TurnInfrastructureFailed "abort did not settle")
                      RunFinished(ctx.RunId, res) ]
                    [ CompleteCaller(ctx.RunId, res) ]
            )
    | AbortConfirmed tid when tid = activeTurnId turn -> Ok(applyAfterAbort nowMs ctx turn abortCtx)
    | AbortConfirmed _ -> Ok(noChange StaleTurnMarker)
    | SessionClosed -> Ok(closeActive ctx (activeTurnId turn))
    | AbortDeadlineExpired tid when tid = activeTurnId turn ->
        let res = Failed(InfrastructureFailure "abort deadline expired")

        Ok(
            decided
                (Poisoned(AbortDidNotSettle tid))
                [ SessionPoisoned(ctx.SessionId, AbortDidNotSettle tid)
                  TurnFinished(tid, TurnInfrastructureFailed "abort deadline expired")
                  RunFinished(ctx.RunId, res) ]
                [ CompleteCaller(ctx.RunId, res) ]
        )
    | AbortDeadlineExpired _
    | CancelRequested
    | TurnDeadlineExpired _ -> Ok(noChange StaleTimer)
    | DispatchAccepted _
    | DispatchRejected _ -> Ok(noChange StaleTurnMarker)
    | AbortHostAccepted _
    | AbortRequestFailed _ -> Ok(noChange AbortInProgress)
    | _ -> illegal "ReconcilingAbortSettle" (cmdName cmd)

let decide (nowMs: int64) state cmd =
    match state with
    | IssuingAbort(ctx, turn, abortCtx, idleObserved, abortDeadlineAtMs) ->
        decideIssuingAbort nowMs ctx turn abortCtx idleObserved abortDeadlineAtMs cmd
    | AwaitingAbortSettle(ctx, turn, abortCtx, abortDeadlineAtMs) ->
        decideAwaitingAbort nowMs ctx turn abortCtx abortDeadlineAtMs cmd
    | ReconcilingAbortSettle(ctx, turn, abortCtx, abortDeadlineAtMs) ->
        decideReconcilingAbort nowMs ctx turn abortCtx abortDeadlineAtMs cmd
    | _ -> illegal (stateName state) (cmdName cmd)
