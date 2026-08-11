namespace Wanxiangshu.Domain

/// Cost observability — ExpectedValue(K) per request, holdout-aware.
/// Pure: inputs are costs in provider-request units; no training loop.
/// Steering risk is separate from bytes/latency fallback.

type StrengthCostSample =
    { SavedDeepRequests: int
      ReplicaRequests: int
      OrchestrationOverhead: float
      InputBytes: int
      LatencyMs: int
      FallbackCount: int
      RepairCount: int }

module StrengthCostModel =

    /// ExpectedValue(K) = saved deep requests - replica requests - overhead
    /// Overhead includes orchestration + latency-normalized + bytes-normalized.
    let expectedValue (sample: StrengthCostSample) : float =
        float sample.SavedDeepRequests
        - float sample.ReplicaRequests
        - sample.OrchestrationOverhead

    /// Rollback: any budget can return to K0. K1/K2 -> K0 is the fuse.
    let rollbackToK0 (_current: StrengthBudget) : StrengthBudget = StrengthBudget.K0

    /// Report row for holdout comparison (control vs treatment).
    type StrengthReport =
        { Control: StrengthCostSample
          Treatment: StrengthCostSample
          Delta: float }

    let report (control: StrengthCostSample) (treatment: StrengthCostSample) : StrengthReport =
        let delta = expectedValue treatment - expectedValue control

        { Control = control
          Treatment = treatment
          Delta = delta }
