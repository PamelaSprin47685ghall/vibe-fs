namespace Wanxiangshu.Domain

/// STRENGTH-010: pure value model in one explicit cost unit.
///
/// Callers must supply measured/provider metadata converted to one comparable
/// unit. Tier names are never treated as prices. Probabilities are request-level
/// primary/control estimates, never Replica intervention labels.
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

type StrengthValueEstimate = { V0: float; V1: float; V2: float }

module StrengthCostModel =

    let private probability value = max 0.0 (min 1.0 value)
    let private cost value = max 0.0 value

    let estimateFrom (inputs: StrengthValueInputs) : StrengthValueEstimate =
        let p1 = probability inputs.P1
        let p2 = probability inputs.P2
        let savedDeep1 = cost inputs.SavedDeep1
        let savedDeep2 = cost inputs.SavedDeep2
        let fast1 = cost inputs.Fast1
        let fast2 = cost inputs.Fast2
        let byte1 = cost inputs.Byte1
        let byte2 = cost inputs.Byte2
        let delay1 = cost inputs.Delay1
        let delay2 = cost inputs.Delay2
        let risk1 = cost inputs.Risk1
        let risk2 = cost inputs.Risk2

        { V0 = 0.0
          V1 = p1 * savedDeep1 - fast1 - byte1 - delay1 - risk1
          V2 =
            p1 * savedDeep1 + p1 * p2 * savedDeep2
            - fast1
            - p1 * fast2
            - byte2
            - delay2
            - risk2 }

    /// Scalar facade kept intentionally small for Host/JS property tests. The
    /// record-taking `estimateFrom` is the production-shaped entry point.
    let estimate
        (p1: float)
        (p2: float)
        (savedDeep1: float)
        (savedDeep2: float)
        (fast1: float)
        (fast2: float)
        (byte1: float)
        (byte2: float)
        (delay1: float)
        (delay2: float)
        (risk1: float)
        (risk2: float)
        : StrengthValueEstimate =
        estimateFrom
            { P1 = p1
              P2 = p2
              SavedDeep1 = savedDeep1
              SavedDeep2 = savedDeep2
              Fast1 = fast1
              Fast2 = fast2
              Byte1 = byte1
              Byte2 = byte2
              Delay1 = delay1
              Delay2 = delay2
              Risk1 = risk1
              Risk2 = risk2 }

    /// Fuse semantics are deliberately one-way for new decisions. Durable
    /// Promoted replay is owned elsewhere and is never erased by this helper.
    let rollbackToK0 (_current: StrengthBudget) : StrengthBudget = StrengthBudget.K0
