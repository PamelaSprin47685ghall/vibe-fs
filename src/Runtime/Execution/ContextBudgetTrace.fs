module Wanxiangshu.Runtime.ContextBudgetTrace

open Wanxiangshu.Kernel.ContextBudget

let actionForDecision (pressure: ContextBudgetPressure) (nudgeTrack: BudgetNudgeTrack) : string =
    match pressure, nudgeTrack with
    | Compacting, _ -> "compacting"
    | RequireTodoWriteEmergency, EmergencySignaled _ -> "nudge-injected"
    | RequireTodoWriteEmergency, Idle -> "episode-exhausted"
    | BelowThreshold, _ -> "below-threshold"
    | _ -> "no-action"

type DecisionTrace =
    { Limit: int64
      ModelKey: string
      LimitSource: string
      ObservedTokens: int64 option
      CalibrationBytes: int option
      FinalOutboundBytes: int
      EstimatedTokens: int64
      StableBytes: int
      PhaseBaseTokens: int64
      Confidence: UsageConfidence
      Pressure: ContextBudgetPressure
      Action: string }
