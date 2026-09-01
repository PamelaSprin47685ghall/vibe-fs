namespace Wanxiangshu.Strength.Prediction

type StrengthValueInputs =
    { P1: float
      P2: float
      SavedDeep1: float
      SavedDeep2: float
      Fast1: float
      Fast2: float
      Byte1: float
      Byte2: float
      Delay1: float
      Delay2: float
      Risk1: float
      Risk2: float }

type StrengthValueEstimate =
    { V0: float
      V1: float
      V2: float }

module StrengthCostModel =
    val estimateFrom: inputs: StrengthValueInputs -> StrengthValueEstimate

    val estimate:
        p1: float ->
        p2: float ->
        savedDeep1: float ->
        savedDeep2: float ->
        fast1: float ->
        fast2: float ->
        byte1: float ->
        byte2: float ->
        delay1: float ->
        delay2: float ->
        risk1: float ->
        risk2: float ->
        StrengthValueEstimate
