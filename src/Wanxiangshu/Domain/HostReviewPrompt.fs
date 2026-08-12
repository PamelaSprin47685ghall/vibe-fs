namespace Wanxiangshu.Domain

/// GLORY-046 + A.6.1 + SURFACE-004: Host-owned Reviewer opening assignment path.
/// Prose lives in ProviderResources; the Manager never sees this text.
[<RequireQualifiedAccess>]
module HostReviewPrompt =

    /// GLORY-046: frozen opening assignment semantic path.
    let Opening = "lifecycle/host-review/opening"
