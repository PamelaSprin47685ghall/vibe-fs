module Wanxiangshu.Kernel.Subsession.Cancellation

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Policy
open Wanxiangshu.Kernel.Subsession.Rules

let decide (state: SubsessionState) (cmd: Command) : Result<DecisionResult, DecisionError> =
    match state with
    | Poisoned _ ->
        match cmd with
        | SessionClosed -> Ok(decided state [] [ DisposeActor ])
        | _ -> Ok(noChange StaleTimer)
    | Available _ ->
        match cmd with
        | CancelRequested -> Ok(noChange StaleTimer)
        | SessionClosed -> Ok(decided state [] [ DisposeActor ])
        | cmd when isStaleTimerCommand cmd -> Ok(noChange StaleTimer)
        | _ -> illegal (stateName state) (cmdName cmd)
    | Dispatching _
    | CancellingDispatch _ -> Dispatch.decide state cmd
    | ReconcilingUnknownDispatch _
    | ClosingUnknownDispatch _ -> Reconciliation.decide state cmd
    | Running _ -> Running.decide state cmd
    | Draining _ -> Draining.decide state cmd
    | IssuingAbort _
    | AwaitingAbortSettle _
    | ReconcilingAbortSettle _ -> Abort.decide state cmd
