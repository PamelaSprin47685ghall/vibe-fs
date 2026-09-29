namespace Wanxiangshu.OpenCode

module ChatAdmissionIntentSurface =
    /// host-boundary-008: the wire evidence that marks one message HostInternal
    /// (compaction / fully synthetic parts), shared by the intent resolver and
    /// read-side Host hooks.
    val hostInternal: message: obj -> bool
    val resolve: message: obj -> durableSnapshot: obj -> obj
