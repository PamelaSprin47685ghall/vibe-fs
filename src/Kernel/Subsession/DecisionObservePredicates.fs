module Wanxiangshu.Kernel.Subsession.DecisionObservePredicates

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.TranscriptDecision
open Wanxiangshu.Kernel.Subsession.Policy

let private delegateToHostSentinel =
    { ProviderID = ""
      ModelID = ""
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let internal makeTurnData (c: RunContext) (p: TurnPlan) : TurnData =
    { RunId = c.RunId
      TurnId = p.TurnId
      Ordinal = p.Ordinal
      Model = p.Model |> Option.defaultValue delegateToHostSentinel
      Prompt = p.Prompt
      DeadlineAtMs = 0L }

let internal nextTurnFromPolicy (ctx: RunContext) (decision: PolicyDecision) : (RunContext * TurnPlan) option =
    match decision with
    | NextTurn(policy2, model, prompt) ->
        let ordinal = ctx.NextTurnOrdinal

        let turnId =
            TurnId.create (RunId.value ctx.RunId + "-t" + string (TurnOrdinal.value ordinal))

        let plan =
            { TurnId = turnId
              Ordinal = ordinal
              Model = Some model
              Prompt = prompt }

        Some(
            { ctx with
                Policy = policy2
                NextTurnOrdinal = TurnOrdinal.next ordinal },
            plan
        )
    | StopWithFailure _ -> None

let internal activeTurnId (turn: ActiveTurn) : TurnId =
    match turn with
    | NotYetStarted p -> p.TurnId
    | Started s -> s.Plan.TurnId

let internal decided state events effects : DecisionResult =
    Decided
        { NextState = state
          Events = events
          Effects = effects }

let internal noChange reason : DecisionResult = NoChange reason

let internal illegal state cmd : Result<DecisionResult, DecisionError> = Error(IllegalTransition(state, cmd))

let internal failRun (ctx: RunContext) (failure: RunFailure) (extraEvents: SubsessionEvent list) =
    let result = Failed failure

    decided
        (Available { SessionId = ctx.SessionId })
        (extraEvents @ [ RunFinished(ctx.RunId, result) ])
        [ CompleteCaller(ctx.RunId, result) ]

let internal succeedRun (ctx: RunContext) (output: string) (turnId: TurnId) =
    let result = Succeeded output

    decided
        (Available { SessionId = ctx.SessionId })
        [ TurnFinished(turnId, TurnCompleted output); RunFinished(ctx.RunId, result) ]
        [ CompleteCaller(ctx.RunId, result) ]
