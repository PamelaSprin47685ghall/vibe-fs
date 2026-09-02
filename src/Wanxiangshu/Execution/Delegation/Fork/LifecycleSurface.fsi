namespace Wanxiangshu.Execution.Delegation.Fork

/// JS-native lifecycle snapshots for the delegation-owned child run. The
/// completion cell and cancellation token remain opaque runtime resources.
module ForkLifecycleSurface =
    val snapshot: action: string -> runtimeCancelled: bool -> message: string -> obj
