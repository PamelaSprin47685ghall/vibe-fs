namespace Wanxiangshu.Strength

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength.Prediction

[<RequireQualifiedAccess>]
type StrengthRolloutMode =
    | Off
    | Shadow
    | DryRun
    | Treatment

/// STRENGTH-010: every non-probability term is explicit in one comparable unit.
/// Nothing here can be inferred from the words Fast/Deep or an agent name.
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

    let estimate (prediction: StrengthPrediction) (costs: StrengthExplicitCostTemplate) =
        StrengthCostModel.estimateFrom
            { P1 = prediction.P1
              P2 = prediction.P2
              SavedDeep1 = costs.SavedDeep1
              SavedDeep2 = costs.SavedDeep2
              Fast1 = costs.Fast1
              Fast2 = costs.Fast2
              Byte1 = costs.Byte1
              Byte2 = costs.Byte2
              Delay1 = costs.Delay1
              Delay2 = costs.Delay2
              Risk1 = costs.Risk1
              Risk2 = costs.Risk2 }

    let isShadow =
        function
        | StrengthRolloutMode.Shadow -> true
        | StrengthRolloutMode.Off
        | StrengthRolloutMode.DryRun
        | StrengthRolloutMode.Treatment -> false
