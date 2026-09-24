namespace Wanxiangshu.Ablation

/// Reads the ablation manifest and the ablation environment, and nothing else.
///
/// This module is the one place that knows where a registry comes from. Every decision
/// lives in `AblationGate` against a registry the caller supplies, so a test can hold
/// its own registry without this module noticing.
[<RequireQualifiedAccess>]
module AblationSettings =
    /// Resolve a registry from the ablation environment.
    val load: unit -> Result<AblationRegistry, AblationLoadError>
