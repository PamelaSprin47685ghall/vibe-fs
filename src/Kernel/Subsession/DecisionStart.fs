module Wanxiangshu.Kernel.Subsession.DecisionStart

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Policy

let private delegateToHostSentinel: FallbackModel =
    { ProviderID = ""
      ModelID = ""
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private decided state events effects : DecisionResult =
    Decided
        { NextState = state
          Events = events
          Effects = effects }

let private startDispatch
    (nowMs: int64)
    (state: SubsessionState)
    (req: StartRunRequest)
    (chainForCtx: FallbackChain)
    (modelForPlan: FallbackModel option)
    =
    let policy = initialPolicy req.FallbackConfig chainForCtx

    let ctx =
        { RunId = req.RunId
          ParentSessionId = req.ParentSessionId
          SessionId = req.SessionId
          Policy = policy
          FallbackConfig = req.FallbackConfig
          Chain = chainForCtx
          NextTurnOrdinal = TurnOrdinal.next TurnOrdinal.first }

    let plan =
        { TurnId = TurnId.create (RunId.value req.RunId + "-t0")
          Ordinal = TurnOrdinal.first
          Model = modelForPlan
          Prompt = req.Prompt }

    let turnDeadlineAtMs = nowMs + 300_000L

    let events =
        [ RunStarted
              { RunId = req.RunId
                ParentSessionId = req.ParentSessionId
                SessionId = req.SessionId }
          TurnDispatchRequested
              { RunId = ctx.RunId
                TurnId = plan.TurnId
                Ordinal = plan.Ordinal
                Model = plan.Model |> Option.defaultValue delegateToHostSentinel
                Prompt = plan.Prompt
                DeadlineAtMs = turnDeadlineAtMs } ]

    let effects = [ DispatchPrompt plan ]

    decided (Dispatching(ctx, plan, CurrentTurnEvidence.empty, PendingTerminal.empty, turnDeadlineAtMs)) events effects

let private handleAvailable (nowMs: int64) (state: SubsessionState) (req: StartRunRequest) =
    match req.Directive with
    | RetryChain [] -> decided state [] [ RejectStart NoModelAvailable ]
    | RetryChain(firstModel :: _ as chain) -> startDispatch nowMs state req chain (Some firstModel)
    | DelegateToHost -> startDispatch nowMs state req [] None

let decide (nowMs: int64) (state: SubsessionState) (req: StartRunRequest) : Result<DecisionResult, DecisionError> =
    match state with
    | Poisoned reason -> Ok(decided state [] [ RejectStart(StartRunError.SessionPoisoned reason) ])

    | Available avail when req.InitiallyCancelled ->
        let events =
            [ RunStarted
                  { RunId = req.RunId
                    ParentSessionId = req.ParentSessionId
                    SessionId = req.SessionId }
              RunFinished(req.RunId, Cancelled) ]

        let effects = [ CompleteCaller(req.RunId, Cancelled) ]
        Ok(decided (Available avail) events effects)

    | Available _ -> Ok(handleAvailable nowMs state req)

    | _ -> Ok(decided state [] [ RejectStart AlreadyRunning ])
