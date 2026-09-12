namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open Wanxiangshu.Foundation.Identity

/// Consumer-side journal reads needed by HostJoinGuard.
/// Each member derives from exactly one Journal Snapshot revision captured
/// when the port is built (see AgentJournalPortAdapter.forHostJoinGuard).
type HostJoinGuardJournalPort =
    { HasOutstandingJoinClaim: SessionId -> ProviderRunIdentity -> bool }
