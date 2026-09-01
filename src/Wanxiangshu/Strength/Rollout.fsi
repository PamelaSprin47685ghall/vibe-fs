namespace Wanxiangshu.Strength

open Wanxiangshu.Strength.Prediction

[<RequireQualifiedAccess>]
type StrengthRolloutMode =
    | Off
    | Shadow
    | DryRun
    | Treatment

type StrengthExplicitCostTemplate =
    { SavedDeep1: float
      SavedDeep2: float
      Fast1: float
      Fast2: float
      Byte1: float
      Byte2: float
      Delay1: float
      Delay2: float
      Risk1: float
      Risk2: float }

type StrengthRolloutConfig =
    { Mode: StrengthRolloutMode
      PolicyVersion: string
      ControlRateBasisPoints: int
      Policy: StrengthPolicyConfig
      Costs: StrengthExplicitCostTemplate option }

[<RequireQualifiedAccess>]
module StrengthRollout =
    val estimate: prediction: StrengthPrediction -> costs: StrengthExplicitCostTemplate -> StrengthValueEstimate
