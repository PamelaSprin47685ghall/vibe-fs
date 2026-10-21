namespace Wanxiangshu.Execution.Fission

/// intra-participant-parallelism-013: the single product decision about
/// whether a provider request must hide the `fission` tool.
///
/// The decision is product policy, so it lives in the product lane. The host
/// adapter owns only the wire write that carries the decision into the
/// provider request, and it supplies the host-owned ablation fact.
[<RequireQualifiedAccess>]
module FissionToolVisibility =

    /// True when the request must not offer the `fission` tool.
    ///
    /// `hasPhysicalParent` — the request's origin is a physical parent session.
    /// `fissionFeatureActive` — the fission feature node is not ablated.
    val hidesFissionTool: hasPhysicalParent: bool -> fissionFeatureActive: bool -> bool

    /// The origin-narrowing half only: physical origin may only narrow the
    /// office entitlement, so a request without a physical parent must hide
    /// the tool.
    val hidesByOrigin: hasPhysicalParent: bool -> bool
