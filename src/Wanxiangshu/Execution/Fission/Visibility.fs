namespace Wanxiangshu.Execution.Fission

/// intra-participant-parallelism-013: the single product decision about
/// whether a provider request must hide the `fission` tool.
///
/// The decision is product policy, so it lives in the product lane. The host
/// adapter owns only the wire write that carries the decision into the
/// provider request, and it supplies the host-owned ablation fact.
[<RequireQualifiedAccess>]
module FissionToolVisibility =

    /// The origin-narrowing half only: physical origin may only narrow the
    /// office entitlement, so a request without a physical parent must hide
    /// the tool.
    let hidesByOrigin (hasPhysicalParent: bool) : bool =
        FissionRequestProjection.apply hasPhysicalParent

    /// True when the request must not offer the `fission` tool: either the
    /// fission feature is ablated, or the request's origin narrows the
    /// office entitlement.
    let hidesFissionTool (hasPhysicalParent: bool) (fissionFeatureActive: bool) : bool =
        not fissionFeatureActive || hidesByOrigin hasPhysicalParent
