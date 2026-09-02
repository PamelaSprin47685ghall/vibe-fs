namespace Wanxiangshu.Strength.OpenCode

open Wanxiangshu.Strength

/// STRENGTH-010/011: Host-owned rollout settings. Malformed or incomplete cost
/// metadata never guesses; treatment collapses to K0 through Costs=None.
[<RequireQualifiedAccess>]
module StrengthSettings =

    val HostCanaryFingerprint: string

    val hostCanaryHealthy: unit -> bool

    /// Host-canary only. DryRun never publishes Prepared or changes primary bytes,
    /// so K2 can be exercised independently of treatment/economic activation.
    /// Missing or malformed input stays on the established K1 canary path.
    val dryRunBudget: unit -> StrengthBudget

    val load: unit -> StrengthRolloutConfig
