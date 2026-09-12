namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open Wanxiangshu.Foundation.Identity

/// Consumer-side journal reads needed by HostJoinGuard.
/// Each member performs exactly one journal snapshot read at call time;
/// multiple fields within one member share that snapshot.
type HostJoinGuardJournalPort =
    { HasOutstandingJoinClaim: SessionId -> ProviderRunIdentity -> bool }
